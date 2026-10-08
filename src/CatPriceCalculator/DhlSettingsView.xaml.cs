using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using CatPriceCalculator.Core;
namespace CatPriceCalculator;

public partial class DhlSettingsView : UserControl
{
    private sealed record ServiceOption(string Code, string Name);
    private DhlSettingsStore? store;
    private DhlTrackingService? service;
    private CancellationToken token;
    private bool loading, dirty, busy;
    public DhlConnectionSettings CurrentSettings { get; private set; } = new();
    public event EventHandler? ConnectionChanged;
    public DhlSettingsView() => InitializeComponent();
    public void Configure(DhlSettingsStore settingsStore, DhlTrackingService trackingService, CancellationToken lifetime)
    {
        store = settingsStore; service = trackingService; token = lifetime;
        var result = store.Load(); CurrentSettings = result.Settings;
        var options = new List<ServiceOption> { new("", "Automatikus"), new("express", "DHL Express"), new("parcel-de", "DHL Parcel (DE)"), new("parcel-nl", "DHL Parcel (NL)"), new("ecommerce", "DHL eCommerce"), new("freight", "DHL Freight"), new("dgf", "Global Forwarding") };
        if (!options.Any(o => o.Code == CurrentSettings.Service)) options.Add(new(CurrentSettings.Service, CurrentSettings.Service));
        loading = true;
        ServiceInput.ItemsSource = options;
        ApiKeyInput.Value = CurrentSettings.ApiKey; PostalInput.Text = CurrentSettings.RecipientPostalCode;
        ServiceInput.SelectedItem = options.First(o => o.Code == CurrentSettings.Service);
        DailyLimitInput.Text = CurrentSettings.DailyLimit.ToString(CultureInfo.InvariantCulture);
        loading = false; dirty = false; ApiKeyInput.HideValue();
        ConnectionStatus.Text = result.Failed ? "A korábbi kulcs nem olvasható ezzel a Windows-felhasználóval. Add meg és mentsd újra." : CurrentSettings.HasKey ? "DHL kulcs mentve, védett tárolásban. A kapcsolat tesztelhető." : "Még nincs DHL API-kulcs. Add meg a saját Consumer Key értékét.";
        UpdateButtons();
    }
    private void Changed()
    {
        if (loading || store == null) return;
        dirty = true; ConnectionStatus.Text = "Módosított beállítások • mentés szükséges a használathoz.";
        UpdateButtons();
    }
    private void OptionsChanged(object? sender, EventArgs e) => Changed();
    private void TextOptionChanged(object sender, TextChangedEventArgs e) => Changed();
    private void ServiceChanged(object sender, SelectionChangedEventArgs e) => Changed();
    private void TestEntryChanged(object sender, TextChangedEventArgs e) => UpdateButtons();
    private void UpdateButtons()
    {
        if (TestButton == null) return;
        TestButton.IsEnabled = !busy && !dirty && service != null && CurrentSettings.HasKey && !string.IsNullOrWhiteSpace(TestNumberInput.Text);
        SaveButton.IsEnabled = DeleteKeyButton.IsEnabled = !busy && store != null;
    }
    private void SaveSettings(object sender, RoutedEventArgs e)
    {
        if (store == null || ServiceInput.SelectedItem is not ServiceOption option) return;
        if (!int.TryParse(DailyLimitInput.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int limit))
        { ConnectionStatus.Text = "A 24 órás keret egész szám legyen, 1 és 100000 között."; return; }
        var settings = new DhlConnectionSettings(ApiKeyInput.Value.Trim(), PostalInput.Text.Trim(), option.Code, limit);
        if (!settings.IsValid || !settings.HasKey) { ConnectionStatus.Text = "Adj meg saját, nem demo API-kulcsot és érvényes irányítószámot/keretet."; return; }
        if (!store.Save(settings)) { ConnectionStatus.Text = "A mentés nem sikerült. A korábbi kapcsolatbeállítás megmaradt."; return; }
        CurrentSettings = settings; dirty = false; ApiKeyInput.HideValue();
        ConnectionStatus.Text = "DHL beállítások mentve, Windows-felhasználóhoz kötött titkosítással.";
        UpdateButtons(); ConnectionChanged?.Invoke(this, EventArgs.Empty);
    }
    private void DeleteKey(object sender, RoutedEventArgs e)
    {
        if (store == null || !store.Save(CurrentSettings with { ApiKey = "" })) { ConnectionStatus.Text = "A kulcs törlése nem menthető. A korábbi beállítás megmaradt."; return; }
        CurrentSettings = CurrentSettings with { ApiKey = "" }; loading = true; ApiKeyInput.Value = "";
        PostalInput.Text = CurrentSettings.RecipientPostalCode;
        ServiceInput.SelectedItem = ServiceInput.Items.Cast<ServiceOption>().First(o => o.Code == CurrentSettings.Service);
        DailyLimitInput.Text = CurrentSettings.DailyLimit.ToString(CultureInfo.InvariantCulture);
        loading = false; dirty = false; ApiKeyInput.HideValue();
        ConnectionStatus.Text = "A mentett DHL API-kulcs törölve. Új kulcsig a követés nem indít lekérést.";
        UpdateButtons(); ConnectionChanged?.Invoke(this, EventArgs.Empty);
    }
    private async void TestConnection(object sender, RoutedEventArgs e)
    {
        if (!TestButton.IsEnabled || service == null) return;
        busy = true; UpdateButtons(); TestStatus.Text = "DHL tesztlekérés folyamatban…";
        try
        {
            var result = await service.FetchAsync(CurrentSettings, TestNumberInput.Text, token);
            if (token.IsCancellationRequested) return;
            TestStatus.Text = $"Kapcsolat rendben • {result.State switch { ParcelState.Delivered => "megérkezett", ParcelState.InTransit => "úton", ParcelState.PreTransit => "feladás előkészítve", ParcelState.Exception => "eltérés / fennakadás", _ => "státusz elérhető" }}\nUtolsó hely: {result.Location ?? "még nincs adat"}";
            TestStatus.ToolTip = result.StatusDetail;
        }
        catch (ParcelTrackingException ex) { if (!token.IsCancellationRequested) TestStatus.Text = ex.Message; }
        catch (OperationCanceledException) { if (!token.IsCancellationRequested) TestStatus.Text = "A tesztlekérés megszakadt."; }
        finally { busy = false; if (!token.IsCancellationRequested) UpdateButtons(); }
    }
    private void OpenDocumentation(object sender, RoutedEventArgs e)
    {
        try { using var browser = Process.Start(new ProcessStartInfo("https://developer.dhl.com/tracking") { UseShellExecute = true }); }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { ConnectionStatus.Text = "Nyisd meg böngészőben: https://developer.dhl.com/tracking"; }
    }
}
