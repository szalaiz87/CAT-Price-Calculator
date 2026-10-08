using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using CatPriceCalculator.Core;
namespace CatPriceCalculator;

public partial class UpsSettingsView : UserControl
{
    private UpsSettingsStore? store;
    private UpsTrackingService? service;
    private CancellationToken token;
    private bool loading, dirty, busy;
    public UpsConnectionSettings CurrentSettings { get; private set; } = new();
    public event EventHandler? ConnectionChanged;
    public UpsSettingsView() => InitializeComponent();
    public void Configure(UpsSettingsStore settingsStore, UpsTrackingService trackingService, CancellationToken lifetime)
    {
        store = settingsStore; service = trackingService; token = lifetime;
        var result = store.Load(); CurrentSettings = result.Settings; Fill();
        ConnectionStatus.Text = result.Failed ? "A mentett hozzáférés nem olvasható. Add meg és mentsd újra." : CurrentSettings.HasCredentials ? "UPS hozzáférés mentve, védett tárolásban. A kapcsolat tesztelhető." : "Még nincs UPS hozzáférés. Client ID és Client Secret szükséges.";
    }
    private void Fill()
    {
        loading = true;
        ClientIdInput.Value = CurrentSettings.ClientId; ClientSecretInput.Value = CurrentSettings.ClientSecret;
        AccountInput.Text = CurrentSettings.AccountNumber; DailyLimitInput.Text = CurrentSettings.DailyLimit.ToString(CultureInfo.InvariantCulture);
        loading = false; dirty = false; HideCredentials(); UpdateButtons();
    }
    private void HideCredentials() { ClientIdInput.HideValue(); ClientSecretInput.HideValue(); }
    private void Changed()
    {
        if (loading || store == null) return;
        dirty = true; ConnectionStatus.Text = "Módosított beállítások • mentés szükséges a használathoz."; UpdateButtons();
    }
    private void CredentialChanged(object? sender, EventArgs e) => Changed();
    private void OptionChanged(object sender, TextChangedEventArgs e) => Changed();
    private void TestEntryChanged(object sender, TextChangedEventArgs e) => UpdateButtons();
    private void UpdateButtons()
    {
        if (TestButton == null) return;
        TestButton.IsEnabled = !busy && !dirty && service != null && CurrentSettings.HasCredentials && !string.IsNullOrWhiteSpace(TestNumberInput.Text);
        SaveButton.IsEnabled = DeleteButton.IsEnabled = !busy && store != null;
    }
    private void SaveSettings(object sender, RoutedEventArgs e)
    {
        if (store == null) return;
        if (!int.TryParse(DailyLimitInput.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int limit))
        { ConnectionStatus.Text = "A helyi keret egész szám legyen, 1 és 100000 között."; return; }
        var settings = new UpsConnectionSettings(ClientIdInput.Value.Trim(), ClientSecretInput.Value.Trim(), AccountInput.Text.Trim(), limit);
        if (!settings.IsValid || !settings.HasCredentials) { ConnectionStatus.Text = "Client ID / Secret szükséges; az ügyfélszám üres vagy 6 betű/szám legyen."; return; }
        if (!store.Save(settings)) { ConnectionStatus.Text = "A mentés nem sikerült. A korábbi kapcsolatbeállítás megmaradt."; return; }
        CurrentSettings = settings; dirty = false; HideCredentials(); service?.ForgetToken();
        ConnectionStatus.Text = "UPS beállítások mentve, Windows-felhasználóhoz kötött titkosítással.";
        UpdateButtons(); ConnectionChanged?.Invoke(this, EventArgs.Empty);
    }
    private void DeleteCredentials(object sender, RoutedEventArgs e)
    {
        var settings = CurrentSettings with { ClientId = "", ClientSecret = "" };
        if (store?.Save(settings) != true) { ConnectionStatus.Text = "A hozzáférés törlése nem menthető. A korábbi beállítás megmaradt."; return; }
        CurrentSettings = settings; Fill(); service?.ForgetToken();
        ConnectionStatus.Text = "A mentett UPS hozzáférés törölve. Új adatokig nincs UPS lekérés.";
        ConnectionChanged?.Invoke(this, EventArgs.Empty);
    }
    private async void TestConnection(object sender, RoutedEventArgs e)
    {
        if (!TestButton.IsEnabled || service == null) return;
        busy = true; UpdateButtons(); TestStatus.Text = "UPS tesztlekérés folyamatban…"; TestStatus.ToolTip = null;
        try
        {
            var result = await service.FetchAsync(CurrentSettings, TestNumberInput.Text, token);
            if (token.IsCancellationRequested) return;
            TestStatus.Text = $"Kapcsolat rendben • {result.State switch { ParcelState.Delivered => "megérkezett", ParcelState.InTransit => "úton", ParcelState.OutForDelivery => "kézbesítés alatt", ParcelState.PreTransit => "feladás előkészítve", ParcelState.Exception => "fennakadás", _ => "státusz elérhető" }}\nUtolsó hely: {result.Location ?? "még nincs adat"}";
            TestStatus.ToolTip = result.StatusDetail;
        }
        catch (ParcelTrackingException ex) { if (!token.IsCancellationRequested) TestStatus.Text = ex.Message; }
        catch (OperationCanceledException) { if (!token.IsCancellationRequested) TestStatus.Text = "A tesztlekérés megszakadt."; }
        finally { busy = false; if (!token.IsCancellationRequested) UpdateButtons(); }
    }
    private void OpenDocumentation(object sender, RoutedEventArgs e)
    {
        try { using var browser = Process.Start(new ProcessStartInfo("https://developer.ups.com/") { UseShellExecute = true }); }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { ConnectionStatus.Text = "Nyisd meg böngészőben: https://developer.ups.com/"; }
    }
}
