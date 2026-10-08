using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CatPriceCalculator.Core;
namespace CatPriceCalculator;

public partial class CarrierSettingsView : UserControl
{
    private sealed record ServiceOption(string Code, string Name);
    private static readonly ServiceOption[] Services = DhlConnectionSettings.Services.Select(c => new ServiceOption(c, c switch { "" => "Automatikus", "express" => "DHL Express", "parcel-de" => "DHL Parcel (DE)", "freight" => "DHL Freight", "dgf" => "Global Forwarding", _ => c })).ToArray();
    private sealed class Draft(CarrierBinding binding)
    {
        public CarrierInput Input = binding.Current;
        public string Limit = binding.Current.DailyLimit.ToString(CultureInfo.InvariantCulture), Number = "", Test = "Mentsd a hozzáférést, majd adj meg egy saját csomagszámot.";
        public string Status = binding.LoadFailed ? "A mentett hozzáférés nem olvasható. Add meg és mentsd újra." : binding.HasCredentials ? "Hozzáférés mentve, védett tárolásban. A kapcsolat tesztelhető." : "Még nincs mentett hozzáférés. Kövesd az alábbi útmutatót.";
        public string? Detail;
        public bool Dirty, Busy;
    }
    private readonly Dictionary<Courier, Draft> drafts = [];
    private CarrierBinding? binding;
    private Draft? draft;
    private CancellationToken lifetime;
    private bool loading;
    public event EventHandler? ConnectionChanged;
    public CarrierSettingsView() => InitializeComponent();
    public void Configure(CancellationToken token) => lifetime = token;
    public void Show(CarrierBinding next)
    {
        if (ReferenceEquals(binding, next)) return;
        Capture(); HideCredentials(); binding = next;
        if (!drafts.TryGetValue(next.Carrier, out draft)) drafts[next.Carrier] = draft = new(next);
        loading = true;
        CarrierTitle.Text = next.Name + " kapcsolat";
        CarrierLogo.Source = (ImageSource)FindResource(next.Carrier switch { Courier.Dhl => "DhlLogo", Courier.Ups => "UpsLogo", Courier.FedEx => "FedExLogo", _ => "GlsLogo" });
        bool dhl = next.Carrier == Courier.Dhl, fedex = next.Carrier == Courier.FedEx, gls = next.Carrier == Courier.Gls;
        PrimaryInput.Caption = next.Carrier switch { Courier.Dhl => "API-kulcs (Consumer Key)", Courier.Ups => "Client ID", Courier.FedEx => "API-kulcs (Client ID)", _ => "API-felhasználó (MyGLS)" };
        SecondaryInput.Caption = gls ? "API-jelszó (MyGLS)" : "Titkos kulcs (Client Secret)";
        SecondaryInput.Visibility = dhl ? Visibility.Collapsed : Visibility.Visible;
        SingleKeyNote.Visibility = dhl ? Visibility.Visible : Visibility.Collapsed;
        SingleKeyNote.Text = "A DHL Unified API-hoz csak a Consumer Key szükséges.\nConsumer Secret és OAuth-token megadása nem kell.";
        AccountPanel.Visibility = dhl || fedex ? Visibility.Collapsed : Visibility.Visible;
        AccountNote.Visibility = fedex ? Visibility.Visible : Visibility.Collapsed;
        AccountNote.Text = "A céges FedEx ügyfélszámot a fejlesztői projektedhez társítsd. Itt nem szükséges megadni.";
        AccountLabel.Text = gls ? "GLS ügyfélszám (opcionális)" : "UPS ügyfélszám (opcionális)";
        AccountInput.MaxLength = gls ? 10 : 6;
        AccountInput.ToolTip = gls ? "A MyGLS API-felhasználóhoz engedélyezett pozitív ügyfélszám. Üresen is hagyható." : "A projekthez társított hat betű/szám; opcionális x-merchant-id fejléc.";
        DhlOptions.Visibility = dhl ? Visibility.Visible : Visibility.Collapsed;
        CarrierSubtitle.Text = next.Carrier switch { Courier.Dhl => "Shipment Tracking – Unified • éles hozzáférés", Courier.Ups => "Tracking API • OAuth • éles hozzáférés", Courier.FedEx => "Basic Integrated Visibility • OAuth • éles hozzáférés", _ => "Magyar MyGLS • GetParcelStatuses • éles hozzáférés" };
        ServiceInput.ItemsSource = Services;
        PrimaryInput.Value = draft.Input.Primary; SecondaryInput.Value = draft.Input.Secondary;
        AccountInput.Text = draft.Input.Account; PostalInput.Text = draft.Input.Postal; ServiceInput.SelectedValue = draft.Input.Service;
        DailyLimitInput.Text = draft.Limit; TestNumberInput.Text = draft.Number;
        DailyLimitInput.ToolTip = "Helyi védelmi korlát: alapból 250 HTTP-kérés, a hitelesítést is számoljuk. Nem a futár hivatalos kvótája. Csak engedélyezett hozzáférés alapján növeld.";
        string[] guide = next.Carrier switch
        {
            Courier.Dhl => ["1. DHL Developer Portal → saját alkalmazás.", "2. Shipment Tracking – Unified → Production (Europe).", "3. Jóváhagyás után: Consumer Key (API Key).", "Kezdő DHL-keret: 250/nap. Ellenőrizd az alkalmazás engedélyét."],
            Courier.Ups => ["1. UPS Developer Portal → My Apps → saját alkalmazás.", "2. Céges ügyfélszám társítása; Tracking API engedélyezése.", "3. Az adatlapról: Client ID és Client Secret.", "Éles projektkulcsok kellenek. OAuth-tokent nem kell megadni."],
            Courier.FedEx => ["1. FedEx Developer Portal → saját céges projekt.", "2. Basic Integrated Visibility / Tracking API engedélyezése.", "3. Production API Key és Secret Key a Project Overview oldalon.", "Éles kulcsok és társított céges ügyfélszám kellenek; token nem kell."],
            _ => ["1. Magyar MyGLS-fiók és céges GLS-szerződés.", "2. A GLS-től kérj API-jogot a MyGLS-felhasználóhoz.", "3. API-felhasználó és jelszó; ügyfélszám opcionális.", "Beérkező csomagokhoz külön jogosultság kellhet. Az API nem ad ETA-t."]
        };
        GuideOne.Text = guide[0]; GuideTwo.Text = guide[1]; GuideThree.Text = guide[2]; GuideNote.Text = guide[3];
        loading = false; HideCredentials(); Feedback();
    }
    private void Capture()
    {
        if (loading || draft == null) return;
        draft.Input = draft.Input with { Primary = PrimaryInput.Value, Secondary = SecondaryInput.Value, Account = AccountInput.Text, Postal = PostalInput.Text, Service = ServiceInput.SelectedValue as string ?? "" };
        draft.Limit = DailyLimitInput.Text; draft.Number = TestNumberInput.Text;
    }
    private void HideCredentials() { PrimaryInput.HideValue(); SecondaryInput.HideValue(); }
    private void Changed()
    {
        if (loading || draft == null) return;
        Capture(); draft.Dirty = true; draft.Status = "Módosított beállítások • mentés szükséges a használathoz."; Feedback();
    }
    private void CredentialChanged(object? sender, EventArgs e) => Changed();
    private void OptionChanged(object sender, TextChangedEventArgs e) => Changed();
    private void ServiceChanged(object sender, SelectionChangedEventArgs e) => Changed();
    private void TestEntryChanged(object sender, TextChangedEventArgs e) { if (!loading) { Capture(); Feedback(); } }
    private void Feedback()
    {
        if (draft == null || binding == null) return;
        ConnectionStatus.Text = draft.Status; TestStatus.Text = draft.Test; TestStatus.ToolTip = draft.Detail;
        TestButton.IsEnabled = !draft.Busy && !draft.Dirty && binding.HasCredentials && !string.IsNullOrWhiteSpace(TestNumberInput.Text);
        SaveButton.IsEnabled = DeleteButton.IsEnabled = !draft.Busy;
        PrimaryInput.IsEnabled = SecondaryInput.IsEnabled = AccountInput.IsEnabled = PostalInput.IsEnabled = ServiceInput.IsEnabled = DailyLimitInput.IsEnabled = TestNumberInput.IsEnabled = !draft.Busy;
    }
    private void SaveSettings(object sender, RoutedEventArgs e)
    {
        if (binding == null || draft == null || draft.Busy) return;
        Capture();
        if (!int.TryParse(draft.Limit.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int limit) || limit is < 1 or > 100000)
        { draft.Status = "A helyi keret egész szám legyen, 1 és 100000 között."; Feedback(); return; }
        var input = draft.Input with { Primary = draft.Input.Primary.Trim(), Secondary = binding.Carrier == Courier.Gls ? draft.Input.Secondary : draft.Input.Secondary.Trim(), Account = draft.Input.Account.Trim(), Postal = draft.Input.Postal.Trim(), DailyLimit = limit };
        if (!binding.IsValid(input)) { draft.Status = "Hiányzó vagy hibás hozzáférés. Ellenőrizd a kulcsokat, jelszót és opcionális mezőket."; Feedback(); return; }
        if (!binding.Save(input)) { draft.Status = "A mentés nem sikerült. A korábbi kapcsolatbeállítás megmaradt."; Feedback(); return; }
        draft.Input = input; draft.Dirty = false; HideCredentials(); draft.Status = "Hozzáférés mentve, Windows-felhasználóhoz kötött titkosítással.";
        Feedback(); ConnectionChanged?.Invoke(this, EventArgs.Empty);
    }
    private void DeleteCredentials(object sender, RoutedEventArgs e)
    {
        if (binding == null || draft == null || draft.Busy) return;
        var input = binding.Current with { Primary = "", Secondary = "" };
        if (!binding.Save(input)) { draft.Status = "A hozzáférés törlése nem menthető. A korábbi beállítás megmaradt."; Feedback(); return; }
        drafts.Remove(binding.Carrier); var current = binding; binding = null; loading = true; Show(current); loading = false;
        draft!.Status = "A mentett hozzáférés törölve. Új adatokig nincs lekérés."; Feedback(); ConnectionChanged?.Invoke(this, EventArgs.Empty);
    }
    private async void TestConnection(object sender, RoutedEventArgs e)
    {
        if (!TestButton.IsEnabled || binding == null || draft == null) return;
        var selected = binding; var state = draft; string number = TestNumberInput.Text;
        state.Busy = true; state.Test = selected.Name + " tesztlekérés folyamatban…"; state.Detail = null; Feedback();
        try
        {
            var result = await selected.FetchAsync(selected.Current, number, lifetime);
            if (lifetime.IsCancellationRequested) return;
            state.Test = $"Kapcsolat rendben • {result.State switch { ParcelState.Delivered => "megérkezett", ParcelState.InTransit => "úton", ParcelState.OutForDelivery => "kézbesítés alatt", ParcelState.PreTransit => "feladás előkészítve", ParcelState.Customs => "vámkezelés", ParcelState.Exception => "fennakadás", _ => "státusz elérhető" }}\nUtolsó hely: {result.Location ?? "még nincs adat"}";
            state.Detail = result.StatusDetail;
        }
        catch (ParcelTrackingException ex) { if (!lifetime.IsCancellationRequested) state.Test = ex.Message; }
        catch (OperationCanceledException) { if (!lifetime.IsCancellationRequested) state.Test = "A tesztlekérés megszakadt."; }
        finally { state.Busy = false; if (!lifetime.IsCancellationRequested && ReferenceEquals(draft, state)) Feedback(); }
    }
    private void OpenDocumentation(object sender, RoutedEventArgs e)
    {
        string url = binding?.Carrier switch { Courier.Dhl => "https://developer.dhl.com/tracking", Courier.Ups => "https://developer.ups.com/", Courier.FedEx => "https://developer.fedex.com/api/en-us/catalog/track/v1/docs.html", _ => "https://api.mygls.hu/" };
        try { using var browser = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { if (draft != null) { draft.Status = "Nyisd meg böngészőben: " + url; Feedback(); } }
    }
}
