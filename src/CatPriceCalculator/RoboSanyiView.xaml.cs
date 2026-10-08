using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CatPriceCalculator.Core;
namespace CatPriceCalculator;

public partial class RoboSanyiView : UserControl, IDisposable
{
    private sealed record CourierOption(Courier Code, string Name, ImageSource Logo);
    private sealed class ParcelRow(Parcel parcel, CourierOption courier, DateTimeOffset now, bool refreshAvailable, bool deleteAvailable)
    {
        private static readonly TimeZoneInfo HungarianZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Budapest");
        private static string Date(DateTimeOffset? value) => value.HasValue ? TimeZoneInfo.ConvertTime(value.Value, HungarianZone).ToString("yyyy.MM.dd.", PriceCalculator.Hungarian) : "Még nincs adat";
        public ImageSource Logo => courier.Logo;
        public string CourierName => courier.Name;
        public Guid Id => parcel.Id;
        public bool CanRefresh => refreshAvailable && ParcelBook.CanRefresh(parcel);
        public bool CanDelete => deleteAvailable;
        public string RefreshHint => parcel.IsSample ? "A mintacsomag nem indít API-lekérést." : parcel.Carrier is not (Courier.Dhl or Courier.Ups) ? "Ehhez a futárhoz még nincs API-kapcsolat." : CanRefresh ? "Csak ennek a csomagnak a frissítése." : "Mentett futár API-hozzáférés szükséges; aktív frissítés közben várj vagy kattints a Stop gombra.";
        public string SampleLabel => parcel.IsSample ? "MINTA" : "";
        public string TrackingNumber => parcel.TrackingNumber;
        public string Note => string.IsNullOrEmpty(parcel.Note) ? "—" : parcel.Note;
        public bool IsDelivered => parcel.State == ParcelState.Delivered;
        public string Status => parcel.State switch
        {
            ParcelState.InTransit => "Úton",
            ParcelState.Customs => "Vámkezelés",
            ParcelState.OutForDelivery => "Kézbesítés alatt",
            ParcelState.Delivered => "Megérkezett",
            ParcelState.PreTransit => "Feladás előtt",
            ParcelState.Exception => "Fennakadás",
            ParcelState.Unknown => "Ismeretlen",
            _ => "Adatra vár"
        };
        public string Location => string.IsNullOrEmpty(parcel.Location) ? "Még nincs adat" : parcel.Location;
        public bool HasError => !string.IsNullOrWhiteSpace(parcel.TrackingError);
        public string EventDate => parcel.LastEventRawTimestamp != null ? TrackingTimestamp.Date(parcel.LastEventRawTimestamp) : Date(parcel.LastEventAt);
        public string EventTime => parcel.LastEventRawTimestamp != null ? TrackingTimestamp.Time(parcel.LastEventRawTimestamp) : parcel.LastEventAt.HasValue ? TimeZoneInfo.ConvertTime(parcel.LastEventAt.Value, HungarianZone).ToString("HH:mm", PriceCalculator.Hungarian) : "";
        public string DeliveryDate => parcel.EstimatedDeliveryRawTimestamp != null ? TrackingTimestamp.Date(parcel.EstimatedDeliveryRawTimestamp) : IsDelivered && parcel.RetentionFromObservation ? "Még nincs adat" : Date(IsDelivered ? parcel.DeliveredAt : parcel.EstimatedDeliveryAt);
        public string Retention => IsDelivered && parcel.DeliveredAt.HasValue ? "Még " + Math.Max(1, (int)Math.Ceiling((parcel.DeliveredAt.Value + ParcelBook.DeliveredRetention - now).TotalHours)) + " óra" + (parcel.RetentionFromObservation ? "†" : "") : "";
        public string Details => (parcel.IsSample ? "Bemutató adat • nem valós követés\n" : parcel.LastCheckedAt.HasValue ? courier.Name + " API • lekérve: " + RateTimestamp.Format(parcel.LastCheckedAt) + "\n" : parcel.Carrier is (Courier.Dhl or Courier.Ups) ? courier.Name + " • még nincs lekért adat\n" : "Ehhez a futárhoz még nincs API-kapcsolat\n") +
            $"Futár: {CourierName}\nCsomagszám: {TrackingNumber}\nMegjegyzés: {Note}\nStátusz: {Status}\nUtolsó hely: {Location}\nUtolsó esemény: {EventDate} {EventTime}\n" +
            (IsDelivered ? $"Megérkezett: {DeliveryDate}\nTörlés: {RateTimestamp.Format(parcel.DeliveredAt + ParcelBook.DeliveredRetention)}" : $"Tervezett érkezés: {DeliveryDate}") +
            (parcel.StatusDetail != null ? "\n" + courier.Name + ": " + parcel.StatusDetail : "") +
            (HasError ? "\nLekérési hiba: " + parcel.TrackingError + "\nA korábbi adatok megmaradtak." : "") +
            (parcel.RetentionFromObservation ? "\n† Pontos kézbesítési idő nincs; a 48 órát az első igazolt észleléstől számítjuk." : "") +
            (EventTime.EndsWith('*') ? "\n* A futár nem adott meg időzónát; az eredeti helyi idő látható." : "");
    }

    private readonly ParcelStore store = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CAT-Price-Calculator", "robo-sanyi-preview.json"));
    private readonly DispatcherTimer retentionTimer = new() { Interval = TimeSpan.FromMinutes(1) };
    private readonly CourierOption[] couriers;
    private List<Parcel> parcels = [];
    private bool loaded, loadFailed, pendingSave;
    private int transitPage, deliveredPage;
    private DhlTrackingService? dhlService;
    private UpsTrackingService? upsService;
    private Func<UpsConnectionSettings>? upsConnection;
    private Func<DhlConnectionSettings>? connection;
    private CancellationToken appLifetime;
    private CancellationTokenSource? refreshLifetime;
    private bool refreshing;
    public event EventHandler<Courier>? SettingsRequested;
    public event EventHandler? SummaryChanged;
    public int TransitCount => parcels.Count(p => p.State != ParcelState.Delivered);
    public int DeliveredCount => parcels.Count(p => p.State == ParcelState.Delivered);
    public void ConfigureDhl(DhlTrackingService service, Func<DhlConnectionSettings> settings, CancellationToken lifetime)
    { dhlService = service; connection = settings; appLifetime = lifetime; UpdateEntryState(); }
    public void ConfigureUps(UpsTrackingService service, Func<UpsConnectionSettings> settings)
    { upsService = service; upsConnection = settings; UpdateEntryState(); }
    private bool HasConnection(Courier carrier) => carrier switch { Courier.Dhl => connection?.Invoke().HasKey == true, Courier.Ups => upsConnection?.Invoke().HasCredentials == true, _ => false };
    public void ConnectionChanged()
    {
        refreshLifetime?.Cancel(); Render();
        ParcelStatusText.Text = "Kapcsolatbeállítások módosítva • a saját DHL / UPS csomagok kézzel frissíthetők.";
    }

    public RoboSanyiView()
    {
        InitializeComponent();
        couriers = [
            new(Courier.Dhl, "DHL", (ImageSource)FindResource("DhlLogo")),
            new(Courier.FedEx, "FedEx", (ImageSource)FindResource("FedExLogo")),
            new(Courier.Ups, "UPS", (ImageSource)FindResource("UpsLogo")),
            new(Courier.Gls, "GLS", (ImageSource)FindResource("GlsLogo"))
        ];
        CourierInput.ItemsSource = couriers;
        CourierInput.SelectedIndex = 0;
        retentionTimer.Tick += RetentionTick;
    }
    public void InitializeData()
    {
        if (!loaded)
        {
            var result = store.Load();
            loaded = true; loadFailed = result.Failed;
            parcels = result.IsNew ? [.. ParcelBook.Samples(DateTimeOffset.UtcNow)] : result.Parcels;
            if (result.IsNew) pendingSave = !store.Save(parcels);
            if (loadFailed)
            {
                ParcelStatusText.Text = "A csomaglista nem olvasható. A mentett fájlt nem írtuk felül.";
                RemoveSamplesButton.IsEnabled = RestoreSamplesButton.IsEnabled = false;
            }
            else retentionTimer.Start();
        }
        PruneExpired();
        Render(); UpdateEntryState();
    }
    public void Open()
    {
        InitializeData();
        TrackingInput.Focus();
    }
    private void PruneExpired()
    {
        if (loadFailed) return;
        int removed = parcels.RemoveAll(p => ParcelBook.IsExpired(p, DateTimeOffset.UtcNow));
        if (removed > 0 || pendingSave)
        {
            pendingSave = !store.Save(parcels);
            ParcelStatusText.Text = pendingSave ? "A csomaglista most nem menthető; a mentést újra megpróbáljuk." : removed > 0 ? $"{removed} lejárt csomag automatikusan törölve." : "A csomaglista helyben mentve.";
        }
    }
    private void RetentionTick(object? sender, EventArgs e) { PruneExpired(); Render(); }
    private void Render()
    {
        var now = DateTimeOffset.UtcNow;
        RenderTable(false, now); RenderTable(true, now);
        TransitTitle.Text = $"Úton lévő csomagok • {TransitCount}";
        DeliveredTitle.Text = $"Megérkezett csomagok • {DeliveredCount}";
        SummaryChanged?.Invoke(this, EventArgs.Empty);
        UpdateEntryState();
    }
    private void RenderTable(bool delivered, DateTimeOffset now)
    {
        var items = parcels.Where(p => (p.State == ParcelState.Delivered) == delivered)
            .OrderByDescending(p => delivered ? p.DeliveredAt : p.AddedAt).ToArray();
        int pageCount = Math.Max(1, (items.Length + ParcelBook.PageSize - 1) / ParcelBook.PageSize);
        int page = Math.Clamp(delivered ? deliveredPage : transitPage, 0, pageCount - 1);
        if (delivered) deliveredPage = page; else transitPage = page;
        (delivered ? DeliveredRows : TransitRows).ItemsSource = items.Skip(page * ParcelBook.PageSize).Take(ParcelBook.PageSize)
            .Select(p => new ParcelRow(p, couriers.First(c => c.Code == p.Carrier), now,
                !refreshing && HasConnection(p.Carrier), !loadFailed)).ToArray();
        (delivered ? DeliveredEmpty : TransitEmpty).Visibility = items.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        (delivered ? DeliveredPager : TransitPager).Text = items.Length == 0 ? "0 csomag" : $"{page + 1} / {pageCount} oldal • {items.Length} csomag";
        (delivered ? DeliveredPrev : TransitPrev).IsEnabled = page > 0;
        (delivered ? DeliveredNext : TransitNext).IsEnabled = page + 1 < pageCount;
    }
    private void PageClicked(object sender, RoutedEventArgs e)
    {
        string tag = (string)((Button)sender).Tag;
        if (tag.StartsWith("Delivered")) deliveredPage += tag.EndsWith("Next") ? 1 : -1;
        else transitPage += tag.EndsWith("Next") ? 1 : -1;
        Render();
    }
    private void UpdateEntryState()
    {
        if (AddButton == null || RefreshDhlButton == null) return;
        AddButton.IsEnabled = loaded && !loadFailed && !refreshing && !string.IsNullOrWhiteSpace(TrackingInput.Text) && CourierInput.SelectedItem is CourierOption;
        RefreshDhlButton.IsEnabled = loaded && !loadFailed && !refreshing && parcels.Any(p => ParcelBook.CanRefresh(p) && HasConnection(p.Carrier));
        CancelDhlButton.IsEnabled = refreshing;
    }
    private void EntryChanged(object sender, TextChangedEventArgs e) => UpdateEntryState();
    private void CourierChanged(object sender, SelectionChangedEventArgs e) => UpdateEntryState();
    private void AddParcel(object? sender, RoutedEventArgs? e)
    {
        if (!AddButton.IsEnabled || CourierInput.SelectedItem is not CourierOption courier) return;
        PruneExpired();
        if (ParcelBook.HasDuplicate(parcels, courier.Code, TrackingInput.Text))
        {
            ParcelStatusText.Text = "Ez a csomagszám ennél a futárnál már szerepel.";
            TrackingInput.Focus(); return;
        }
        var next = new List<Parcel>(parcels) { ParcelBook.Add(TrackingInput.Text, courier.Code, NoteInput.Text, DateTimeOffset.UtcNow) };
        if (!Commit(next)) return;
        TrackingInput.Clear(); NoteInput.Clear(); transitPage = 0;
        var added = next[^1];
        ParcelStatusText.Text = added.Carrier is (Courier.Dhl or Courier.Ups) ? "Csomag mentve • a sor frissítésével kérheted le az adatokat." : "Csomag mentve • ehhez a futárhoz még nincs API-kapcsolat.";
        Render(); TrackingInput.Focus();
    }
    private bool Commit(List<Parcel> next)
    {
        if (!store.Save(next)) { ParcelStatusText.Text = "A lista nem menthető. A bevitt mezők és a korábbi csomagok megmaradtak."; return false; }
        parcels = next; pendingSave = false; return true;
    }
    private void RemoveSamples(object sender, RoutedEventArgs e)
    {
        if (loadFailed || !Commit(parcels.Where(p => !p.IsSample).ToList())) return;
        ParcelStatusText.Text = "A mintasorok törölve; a saját csomagok megmaradtak.";
        Render();
    }
    private void RestoreSamples(object sender, RoutedEventArgs e)
    {
        if (loadFailed) return;
        PruneExpired();
        if (!Commit([.. parcels.Where(p => !p.IsSample), .. ParcelBook.Samples(DateTimeOffset.UtcNow)])) return;
        transitPage = deliveredPage = 0;
        ParcelStatusText.Text = "A bemutató minták visszaállítva; a saját csomagok megmaradtak.";
        Render();
    }
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && e.OriginalSource is TextBox)
        {
            if (e.OriginalSource == NoteInput || Keyboard.Modifiers == ModifierKeys.Control) AddParcel(null, null);
            else ((TextBox)e.OriginalSource).MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            e.Handled = true;
        }
    }
    private async void RefreshDhl(object sender, RoutedEventArgs e) => await RefreshDhlAsync();
    private async void RefreshParcel(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: ParcelRow row } && row.CanRefresh) await RefreshDhlAsync(row.Id);
    }
    private void DeleteParcel(object sender, RoutedEventArgs e)
    {
        if (loadFailed || sender is not Button { DataContext: ParcelRow row }) return;
        var next = parcels.Where(p => p.Id != row.Id).ToList();
        if (next.Count == parcels.Count || !Commit(next)) return;
        ParcelStatusText.Text = "A csomag törölve a mentett listából.";
        ParcelStatusText.ToolTip = null;
        Render();
    }
    private void CancelDhl(object sender, RoutedEventArgs e) => refreshLifetime?.Cancel();
    private void OpenDhlSettings(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke(this, (CourierInput.SelectedItem as CourierOption)?.Code == Courier.Ups ? Courier.Ups : Courier.Dhl);
    private async Task RefreshDhlAsync(Guid? only = null)
    {
        if (refreshing || loadFailed || connection == null || upsConnection == null || dhlService == null || upsService == null) return;
        PruneExpired();
        var targets = ParcelBook.RefreshTargets(parcels.Where(p => HasConnection(p.Carrier)), only);
        if (targets.Length == 0) return;
        var settings = connection(); var upsSettings = upsConnection();
        var blockedCarriers = new HashSet<Courier>();
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(appLifetime);
        refreshLifetime = cancel; refreshing = true; Render();
        ParcelStatusText.ToolTip = null;
        int updated = 0, failed = 0;
        bool stopped = false;
        try
        {
            for (int n = 0; n < targets.Length; n++)
            {
                cancel.Token.ThrowIfCancellationRequested();
                var parcel = parcels.FirstOrDefault(p => p.Id == targets[n]);
                if (parcel == null || !ParcelBook.CanRefresh(parcel) || blockedCarriers.Contains(parcel.Carrier)) continue;
                ParcelStatusText.Text = $"{(parcel.Carrier == Courier.Ups ? "UPS" : "DHL")} frissítés • {n + 1}/{targets.Length} csomag…";
                try
                {
                    var result = parcel.Carrier == Courier.Ups
                        ? await upsService.FetchAsync(upsSettings, parcel.TrackingNumber, cancel.Token)
                        : await dhlService.FetchAsync(settings, parcel.TrackingNumber, cancel.Token);
                    cancel.Token.ThrowIfCancellationRequested();
                    var next = new List<Parcel>(parcels); int index = next.FindIndex(p => p.Id == parcel.Id);
                    if (index < 0) continue;
                    next[index] = ParcelTracking.Apply(next[index], result, DateTimeOffset.UtcNow, parcel.Carrier);
                    if (!Commit(next)) { stopped = true; break; }
                    updated++;
                }
                catch (ParcelTrackingException ex)
                {
                    // A row deleted during an in-flight request must stay deleted, even on failure.
                    if (ex.StopsBatch) blockedCarriers.Add(parcel.Carrier);
                    if (!parcels.Any(p => p.Id == parcel.Id)) continue;
                    failed++;
                    var next = parcels.Select(p => p.Id == parcel.Id ? p with { TrackingError = ex.Message } : p).ToList();
                    if (!Commit(next)) { stopped = true; break; }
                    ParcelStatusText.Text = ex.StopsBatch ? "Ennél a futárnál a frissítés leállt. A részleteket a csomagsor mutatja." : "Ehhez a csomaghoz nincs egyértelmű találat.";
                    ParcelStatusText.ToolTip = ex.Message;
                    // An authentication/quota error stops this provider only; the other can continue.
                }
                PruneExpired(); Render();
            }
            if (!stopped) ParcelStatusText.Text = $"{updated} frissítve, {failed} nem frissült." + (blockedCarriers.Count > 0 ? " Futárkorlátozás: további sorok kimaradtak." : "");
        }
        catch (OperationCanceledException) { if (!appLifetime.IsCancellationRequested) ParcelStatusText.Text = "Csomagfrissítés megszakítva; a már mentett adatok megmaradtak."; }
        finally
        {
            refreshing = false; refreshLifetime = null;
            if (!appLifetime.IsCancellationRequested) { PruneExpired(); Render(); }
        }
    }
    public void Dispose() { refreshLifetime?.Cancel(); retentionTimer.Stop(); retentionTimer.Tick -= RetentionTick; }
}
