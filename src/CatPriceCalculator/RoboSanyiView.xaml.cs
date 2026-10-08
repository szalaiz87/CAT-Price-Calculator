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
    private sealed class ParcelRow(Parcel parcel, CourierOption courier, DateTimeOffset now)
    {
        private static readonly TimeZoneInfo HungarianZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Budapest");
        private static string Date(DateTimeOffset? value) => value.HasValue ? TimeZoneInfo.ConvertTime(value.Value, HungarianZone).ToString("yyyy.MM.dd.", PriceCalculator.Hungarian) : "Még nincs adat";
        public ImageSource Logo => courier.Logo;
        public string CourierName => courier.Name;
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
            _ => "Adatra vár"
        };
        public string Location => string.IsNullOrEmpty(parcel.Location) ? "Még nincs adat" : parcel.Location;
        public string EventDate => Date(parcel.LastEventAt);
        public string EventTime => parcel.LastEventAt.HasValue ? TimeZoneInfo.ConvertTime(parcel.LastEventAt.Value, HungarianZone).ToString("HH:mm", PriceCalculator.Hungarian) : "";
        public string DeliveryDate => Date(IsDelivered ? parcel.DeliveredAt : parcel.EstimatedDeliveryAt);
        public string Retention => IsDelivered && parcel.DeliveredAt.HasValue ? "Még " + Math.Max(1, (int)Math.Ceiling((parcel.DeliveredAt.Value + ParcelBook.DeliveredRetention - now).TotalHours)) + " óra" : "";
        public string Details => (parcel.IsSample ? "Bemutató adat • nem valós követés\n" : "Helyben rögzített csomag • API-kapcsolat még nincs\n") +
            $"Futár: {CourierName}\nCsomagszám: {TrackingNumber}\nMegjegyzés: {Note}\nStátusz: {Status}\nUtolsó hely: {Location}\nUtolsó esemény: {EventDate} {EventTime}\n" +
            (IsDelivered ? $"Megérkezett: {DeliveryDate}\nTörlés: {RateTimestamp.Format(parcel.DeliveredAt + ParcelBook.DeliveredRetention)}" : $"Tervezett érkezés: {DeliveryDate}");
    }

    private readonly ParcelStore store = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CAT-Price-Calculator", "robo-sanyi-preview.json"));
    private readonly DispatcherTimer retentionTimer = new() { Interval = TimeSpan.FromMinutes(1) };
    private readonly CourierOption[] couriers;
    private List<Parcel> parcels = [];
    private bool loaded, loadFailed, pendingSave;
    private int transitPage, deliveredPage;
    public event EventHandler? SummaryChanged;
    public int TransitCount => parcels.Count(p => p.State != ParcelState.Delivered);
    public int DeliveredCount => parcels.Count(p => p.State == ParcelState.Delivered);

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
            ParcelStatusText.Text = pendingSave ? "A csomaglista most nem menthető; a mentést újra megpróbáljuk." : removed > 0 ? $"{removed} lejárt csomag automatikusan törölve." : "A csomaglista helyben mentve. API-kapcsolat még nincs.";
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
    }
    private void RenderTable(bool delivered, DateTimeOffset now)
    {
        var items = parcels.Where(p => (p.State == ParcelState.Delivered) == delivered)
            .OrderByDescending(p => delivered ? p.DeliveredAt : p.AddedAt).ToArray();
        int pageCount = Math.Max(1, (items.Length + ParcelBook.PageSize - 1) / ParcelBook.PageSize);
        int page = Math.Clamp(delivered ? deliveredPage : transitPage, 0, pageCount - 1);
        if (delivered) deliveredPage = page; else transitPage = page;
        (delivered ? DeliveredRows : TransitRows).ItemsSource = items.Skip(page * ParcelBook.PageSize).Take(ParcelBook.PageSize)
            .Select(p => new ParcelRow(p, couriers.First(c => c.Code == p.Carrier), now)).ToArray();
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
        if (AddButton != null) AddButton.IsEnabled = loaded && !loadFailed && !string.IsNullOrWhiteSpace(TrackingInput.Text) && CourierInput.SelectedItem is CourierOption;
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
        ParcelStatusText.Text = "Csomag mentve • a követési adatokra az API-kapcsolat után vár.";
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
    public void Dispose() { retentionTimer.Stop(); retentionTimer.Tick -= RetentionTick; }
}
