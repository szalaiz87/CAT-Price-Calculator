using System.Net.Http;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CatPriceCalculator.Core;
namespace CatPriceCalculator;
public partial class MainWindow : Window
{
    private GoogleSyncCoordinator? google;
    private string ServiceSummaryText()=>ServicePage.SummaryText+(google==null?"":"\n\n"+google.Summary);
    private UserSession session;
    private bool Can(string permission)=>session.Can(permission);
    private void ApplyAccess(){
        SessionName.Text=session.Name;
        foreach(var (button,permission) in new[]{(CatMenu,"menu.cat"),(EurMenu,"menu.eur"),(ServiceMenu,"menu.service"),(RoboMenu,"menu.robo"),(UpdateButton,"menu.updates"),(GeneralSettingsButton,UserAccess.General),(DhlSettingsButton,"settings.dhl"),(UpsSettingsButton,"settings.ups"),(FedExSettingsButton,"settings.fedex"),(GlsSettingsButton,"settings.gls"),(ApiLogButton,"settings.log"),(ServiceSettingsButton,"settings.service"),(UsersSettingsButton,UserAccess.Users),(GoogleSettingsButton,"settings.google")})button.Visibility=Can(permission)?Visibility.Visible:Visibility.Collapsed;
        BetaUpdatesSwitch.IsEnabled=!updateBusy && Can("menu.updates");
        RestoreStableButton.IsEnabled=!updateBusy && AppVersion.IsBeta && Can("menu.updates");
        if(!Can(UserAccess.ForSettings(settingsSection)))settingsSection="General";
    }
    private void LogoutClicked(object sender,RoutedEventArgs e){UsersPage.HideSecrets();((App)Application.Current).Logout();}
    // Visual selection is handled by shared XAML templates in both appearance modes.
    private static readonly object Selected = true, Unselected = false;
    private static void SetSelected(Button button, bool selected)
    {
        var property = System.Windows.Controls.Primitives.Selector.IsSelectedProperty;
        if ((bool)button.GetValue(property) != selected) button.SetValue(property, selected ? Selected : Unselected);
    }
    private readonly ExchangeRateService rateService;
    private readonly SettingsStore settings = new(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CAT-Price-Calculator", "settings.json"));
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(8) };
    private readonly CancellationTokenSource lifetime = new();
    private readonly List<Button> buttons = [];
    private bool initialized, settingOnline, settingEuroOnline;
    private int euroRateRevision;
    private readonly SettingsStore euroSettings = new(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CAT-Price-Calculator", "euro-settings.json"));
    private int rateRevision;
    private decimal? multiplier, selling;
    private readonly HttpClient updateHttp = new() { Timeout = TimeSpan.FromSeconds(60) };
    private readonly UpdateService updateService;
    private readonly HttpClient trackingHttp = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(20), MaxResponseContentBufferSize = 2 * 1024 * 1024 };
    private readonly Dictionary<Courier, CarrierBinding> carrierBindings = [];
    private string settingsSection = "General";
    private UpdateRelease? availableUpdate;
    private bool updateBusy;
    private static readonly ReleaseVersion AppVersion = BuildInfo.Current;
    private RateQuote? currentHufQuote, currentEuroQuote;
    private RateSettings? sellingEuroRate;
    private RateQuote? currentEurHufQuote;
    private Task<RateQuote>? eurHufInFlight;
    private int eurHufRevision;
    private Task<RateQuote> FetchSharedEuroHufAsync()
    {
        if (eurHufInFlight is { IsCompleted: false }) return eurHufInFlight;
        return eurHufInFlight = rateService.FetchEuroHufAsync(lifetime.Token);
    }
    private readonly StartupPreferencesStore startupPreferencesStore = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CAT-Price-Calculator", "startup-preferences.json"));
    private bool startupPreferencesReady, startupRefreshing, startupRefreshFailed;
    private bool allowBetaUpdates, suppressBetaChanges;
    private bool IsSlideSwitch(System.Windows.Controls.Primitives.ToggleButton toggle) => toggle == AlwaysOnTopSwitch || toggle == StartupTopmostSwitch || toggle == StartupRateSwitch || toggle == BetaUpdatesSwitch;
    private void StartupPreferencesChanged(object sender, RoutedEventArgs e)
    {
        if (!startupPreferencesReady) return;
        var preferences = new StartupPreferences(StartupTopmostSwitch.IsChecked == true, StartupRateSwitch.IsChecked == true, allowBetaUpdates);
        StartupPreferenceStatus.Text = startupPreferencesStore.Save(preferences) ? "Az indítási beállítások mentve. A következő indításkor lépnek életbe." : "A beállítások nem menthetők. A korábbi indítási beállítások maradnak érvényben.";
    }
    private async Task StartupRatesAsync(bool refresh)
    {
        if (!refresh) { await Task.WhenAll(CheckStartupCurrency(false), CheckStartupCurrency(true), RefreshStartupEuroHuf(false)); return; }
        startupRefreshing = true; startupRefreshFailed = false;
        pendingFetches++; CatMenu.IsEnabled = EurMenu.IsEnabled = false;
        try
        {
            await Task.WhenAll(FetchCurrency(false), FetchCurrency(true), RefreshStartupEuroHuf());
            if (!lifetime.IsCancellationRequested)
                StatusText.Text = startupRefreshFailed ? "Nem minden árfolyam frissült. A sikertelen lekérések korábbi értékei megmaradtak." : "Az induláskori árfolyamfrissítés elkészült. A közben módosított kézi értékek megmaradtak.";
        }
        finally {
            startupRefreshing = false; pendingFetches--;
            if (!lifetime.IsCancellationRequested) CatMenu.IsEnabled = EurMenu.IsEnabled = pendingFetches == 0;
        }
    }
    private async Task RefreshStartupEuroHuf(bool save = true)
    {
        var revision = eurHufRevision;
        try {
            var quote = await FetchSharedEuroHufAsync();
            if (lifetime.IsCancellationRequested || revision != eurHufRevision) return;
            currentEurHufQuote = quote;
            sellingEuroRate = new(decimal.Round(quote.Rate, 8, MidpointRounding.AwayFromZero), quote.Source, quote.Date, quote.RetrievedAt);
            if (save)
            {
                if (!eurHufSettings.Save(sellingEuroRate)) startupRefreshFailed = true;
                if (pages.TryGetValue(true, out var page))
                    pages[true] = page with { Rate = sellingEuroRate.Rate.ToString("0.########", PriceCalculator.Hungarian), Info = EuroHufInfo(sellingEuroRate), Quote = quote, Warning = "", WarningVisibility = Visibility.Collapsed };
            }
            UpdateSellingEuro();
        } catch (Exception e) when (e is HttpRequestException or OperationCanceledException) { startupRefreshFailed = true; if (!lifetime.IsCancellationRequested) UpdateSellingEuro(); }
    }
    private static string EuroHufInfo(RateSettings rate) =>
        $"1 EUR = {rate.Rate.ToString("0.########", PriceCalculator.Hungarian)} HUF • {rate.Source ?? "Manuális árfolyam"}\nÁrfolyamnap: {rate.Date:yyyy.MM.dd.} • Lekérve: {RateTimestamp.Format(rate.RetrievedAt)}";
    private void UpdateSellingEuro()
    {
        SellingEuroText.Text = "— EUR";
        SellingEuroText.ToolTip = sellingEuroRate is null ? "Az EUR/HUF árfolyam nem érhető el. Frissítse az árfolyamokat." : EuroHufInfo(sellingEuroRate);
        if (eurMode || selling is not decimal huf || sellingEuroRate is not { Rate: > 0 } rate) return;
        try
        {
            SellingEuroText.Text = PriceCalculator.ConvertForintsToEuro(huf, rate.Rate).ToString("N2", PriceCalculator.Hungarian).Replace('\u00a0', ' ').Replace('\u202f', ' ') + " EUR";
            SellingEuroText.ToolTip = SellingEuroText.Text + "\n" + EuroHufInfo(rate);
        }
        catch (OverflowException) { SellingEuroText.ToolTip = "Az euróösszeg túl nagy. Adjon meg kisebb értéket."; }
    }
    private double switchDrag;
    private void DragTopmostSwitch(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        if (e.OriginalSource is not System.Windows.Controls.Primitives.Thumb thumb || thumb.TemplatedParent is not System.Windows.Controls.Primitives.ToggleButton toggle || !IsSlideSwitch(toggle)) return;
        switchDrag += e.HorizontalChange;
        thumb.RenderTransform = new TranslateTransform(Math.Clamp(switchDrag, toggle.IsChecked == true ? -18 : 0, toggle.IsChecked == true ? 0 : 18), 0);
        e.Handled = true;
    }
    private void FinishTopmostSwitch(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        if (e.OriginalSource is not System.Windows.Controls.Primitives.Thumb thumb || thumb.TemplatedParent is not System.Windows.Controls.Primitives.ToggleButton toggle || !IsSlideSwitch(toggle)) return;
        thumb.RenderTransform = Transform.Identity;
        if (!e.Canceled) toggle.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, Math.Abs(switchDrag) < 4 ? toggle.IsChecked != true : switchDrag > 0);
        switchDrag = 0; e.Handled = true;
    }
    private bool settingsOpen, roboOpen, lightTheme, serviceOpen;
    private readonly AppearanceStore appearanceStore = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CAT-Price-Calculator", "appearance.json"));
    private void CloseServicePage() { serviceOpen = false; ServicePage.Visibility = Visibility.Collapsed; }
    private void OpenServiceCalendar(object sender, RoutedEventArgs e) {
        if(!Can("menu.service"))return;
        settingsOpen = false; roboOpen = false; serviceOpen = true;
        CalculatorPage.Visibility = Visibility.Hidden; SettingsPage.Visibility = Visibility.Collapsed; RoboSanyiPage.Visibility = Visibility.Collapsed;
        ServicePage.Visibility = Visibility.Visible; ApplyMenuTheme();
    }
    private void OpenSettings(object sender, RoutedEventArgs e)
    {
        CloseServicePage(); settingsOpen = true; roboOpen = false; RoboSanyiPage.Visibility = Visibility.Collapsed; CalculatorPage.Visibility = Visibility.Hidden;
        SettingsPage.Visibility = Visibility.Visible; ApplyMenuTheme();
    }
    private void ShowCalculator()
    {
        if(!Can(eurMode?"menu.eur":"menu.cat")){settingsSection="General";OpenSettings(this,new RoutedEventArgs());return;}
        CloseServicePage(); settingsOpen = false; roboOpen = false; RoboSanyiPage.Visibility = Visibility.Collapsed; SettingsPage.Visibility = Visibility.Collapsed;
        CalculatorPage.Visibility = Visibility.Visible; ApplyMenuTheme();
    }
    private void ApplyMenuTheme()
    {
        ApplyAccess();
        ApplyModeLabels();
        SetSelected(GoogleSettingsButton,settingsSection=="Google");
        GoogleSettingsPage.Visibility=settingsSection=="Google" && Can("settings.google")?Visibility.Visible:Visibility.Collapsed;
        SetSelected(UsersSettingsButton,settingsSection=="Users");
        UsersPage.Visibility=settingsSection=="Users" && session.IsAdministrator?Visibility.Visible:Visibility.Collapsed;
        ServiceSidebar.Visibility=serviceOpen?Visibility.Visible:Visibility.Collapsed;
        if(serviceOpen)ServiceSummary.Text=ServiceSummaryText();
        SetSelected(ServiceMenu, serviceOpen);
        SetSelected(SettingsMenu, settingsOpen);
        SetSelected(RoboMenu, roboOpen);
        CalculatorSidebar.Visibility = (roboOpen || serviceOpen || (!Can("menu.cat") && !Can("menu.eur"))) ? Visibility.Collapsed : Visibility.Visible;
        RoboSidebar.Visibility = roboOpen ? Visibility.Visible : Visibility.Collapsed;
        SetSelected(ServiceSettingsButton, settingsSection == "Service");
        ServiceImportPage.Visibility = settingsSection == "Service" ? Visibility.Visible : Visibility.Collapsed;
        SetSelected(GeneralSettingsButton, settingsSection == "General");
        SetSelected(DhlSettingsButton, settingsSection == "DHL");
        SetSelected(UpsSettingsButton, settingsSection == "UPS");
        SetSelected(FedExSettingsButton, settingsSection == "FedEx");
        SetSelected(GlsSettingsButton, settingsSection == "GLS");
        SetSelected(ApiLogButton, settingsSection == "Log");
        GeneralSettingsPanel.Visibility = settingsSection == "General" ? Visibility.Visible : Visibility.Collapsed;
        var selectedCarrier = carrierBindings.Values.FirstOrDefault(c => c.Name == settingsSection);
        if (selectedCarrier != null) CarrierSettingsPage.Show(selectedCarrier);
        CarrierSettingsPage.Visibility = selectedCarrier != null ? Visibility.Visible : Visibility.Collapsed;
        ApiLogPage.Visibility = settingsSection == "Log" ? Visibility.Visible : Visibility.Collapsed;
    }
    private void ChooseSettingsSection(object sender, RoutedEventArgs e)
    {
        var section=(string)((Button)sender).Tag;if(!Can(UserAccess.ForSettings(section)))return;settingsSection=section;ApplyMenuTheme();
    }

    private void OpenRoboSanyi(object sender, RoutedEventArgs e)
    {
        if(!Can("menu.robo"))return;
        CloseServicePage(); settingsOpen = false; roboOpen = true;
        CalculatorPage.Visibility = Visibility.Hidden; SettingsPage.Visibility = Visibility.Collapsed;
        RoboSanyiPage.Visibility = Visibility.Visible;
        ApplyMenuTheme(); RoboSanyiPage.Open();
    }
    private void ApplyAppearance()
    {
        ThemePalette.Apply(lightTheme); ServicePage.ApplyTheme(lightTheme); ApplyMenuTheme();
        if (multiplier.HasValue) SelectMultiplier(multiplier.Value);
        SetSelected(DarkThemeButton, !lightTheme);
        SetSelected(LightThemeButton, lightTheme);
        ThemeStatus.Text = lightTheme ? "Aktív megjelenés: világos" : "Aktív megjelenés: sötét";
        if (new System.Windows.Interop.WindowInteropHelper(this).Handle != 0) WindowTheme.Apply(this, lightTheme);
    }
    private void ChooseTheme(object sender, RoutedEventArgs e)
    {
        lightTheme = (string)((Button)sender).Tag == "Light"; ApplyAppearance();
        if (!appearanceStore.Save(lightTheme ? Appearance.Light : Appearance.Dark)) ThemeStatus.Text += "\nA beállítás nem menthető; ebben a munkamenetben használható.";
    }
    private bool eurMode;
    private int pendingFetches;
    private readonly SettingsStore eurHufSettings = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CAT-Price-Calculator", "eur-huf-settings.json"));
    private SettingsStore ActiveSettings => eurMode ? eurHufSettings : settings;
    private sealed record PageState(string Price, string Rate, string Info, decimal Multiplier, RateQuote? Quote, string Warning, Visibility WarningVisibility);
    private readonly Dictionary<bool, PageState> pages = [];
    private void BuildMultipliers()
    {
        if (buttons.Count > 0) return;
        foreach (var value in eurMode ? PriceCalculator.EuroSellingMultipliers : PriceCalculator.SellingMultipliers)
        {
            var button = new Button { Content = "× " + value.ToString("F2", PriceCalculator.Hungarian), Tag = value, Style = (Style)FindResource("MultiplierButton"), ToolTip = buttons.Count < 7 ? "Ctrl+" + (buttons.Count + 1) : null };
            button.Click += (_, _) => SelectMultiplier(value);
            buttons.Add(button); MultiplierPanel.Children.Add(button);
        }
    }
    private void ApplyModeLabels()
    {
        PageTitle.Text = eurMode ? "EUR ÁRKALKULÁTOR" : "CAT ÁRKALKULÁTOR";
        PriceLabel.Text = eurMode ? "Beszerzési ár (EUR)" : "CAT ár (DKK)";
        RateLabel.Text = eurMode ? "EUR/HUF árfolyam" : "DKK/HUF árfolyam";
        DkkEuroSection.Visibility = eurMode ? Visibility.Hidden : Visibility.Visible;
        EurHelp.Visibility = eurMode ? Visibility.Visible : Visibility.Collapsed;
        EuroLabel.Text = eurMode ? "Beszerzési ár euróban" : "CAT ár euróban";
        DealerLabel.Text = eurMode ? "Alapár (felár nélkül)" : "Dealer ár +10%";
        SellingEuroLabel.Visibility = SellingEuroText.Visibility = eurMode ? Visibility.Collapsed : Visibility.Visible;
        Grid.SetColumnSpan(SellingText, eurMode ? 2 : 1);
        SetSelected(CatMenu, !settingsOpen && !roboOpen && !serviceOpen && !eurMode);
        SetSelected(EurMenu, !settingsOpen && !roboOpen && !serviceOpen && eurMode);
    }
    private void UpdateSidebar()
    {
        string Summary(string pair,string input,string info){
            var rate=PriceCalculator.TryPositive(input,out var value)?value.ToString("0.########",PriceCalculator.Hungarian):"—";
            if(rate.Length>18)rate=value.ToString("G6",PriceCalculator.Hungarian);
            var heading=pair+" · "+rate;
            if(info.StartsWith("Manuális"))return heading+"\nManuális árfolyam";
            var parts=info.Split(" • ");
            var source=parts.Length>1?parts[1].Split('\n')[0].Replace("Európai Központi Bank (EKB)","EKB"):"Forrás: —";
            if(source.Length>12)source=source[..11]+"…";
            var day=System.Text.RegularExpressions.Regex.Match(info,@"Árfolyamnap: (\d{4}\.\d{2}\.\d{2})");
            var fetched=System.Text.RegularExpressions.Regex.Match(info,@"Lekérve: (\d{4}\.\d{2}\.\d{2}\.) (\d{2}:\d{2}:\d{2})");
            return heading+"\n"+source+" · "+(day.Success?day.Groups[1].Value:"—")+"\n"+(fetched.Success?"Lekérve: "+fetched.Groups[1].Value+"\n"+fetched.Groups[2].Value+" · magyar idő":"Lekérve: nincs adat");
        }
        SidebarRate.Text=Summary(eurMode?"EUR/HUF":"DKK/HUF",RateInput.Text,RateInfo.Text)+(eurMode?"":"\n\n"+Summary("DKK/EUR",EuroRateInput.Text,EuroRateInfo.Text));
        SidebarRate.ToolTip = RateInfo.Text + (eurMode ? "" : "\n\n" + EuroRateInfo.Text);

    }
    private async void SwitchCalculator(object sender, RoutedEventArgs e)
    {
        if (pendingFetches > 0) return;
        var target = (string)((Button)sender).Tag == "EUR";
        if(!Can(target?"menu.eur":"menu.cat"))return;
        var previousMode=eurMode;eurMode=target;ShowCalculator();eurMode=previousMode;
        if (target == eurMode || pendingFetches > 0) return;
        pages[eurMode] = new(PriceInput.Text, RateInput.Text, RateInfo.Text, multiplier ?? PriceCalculator.DefaultSellingMultiplier, currentHufQuote, RateWarning.Text, RateWarning.Visibility);
        initialized = false; eurMode = target; rateRevision++;
        if (!pages.TryGetValue(target, out var page))
        {
            var saved = ActiveSettings.Load();
            page = new("", (saved?.Rate ?? 400m).ToString("0.########", PriceCalculator.Hungarian), saved?.Source != null ? QuoteInfo(saved.Rate, saved.Source, saved.Date, retrievedAt: saved.RetrievedAt) + " (mentett)" : "Manuális árfolyam", PriceCalculator.DefaultSellingMultiplier, null, "", Visibility.Collapsed);
        }
        PriceInput.Text = page.Price; RateInput.Text = page.Rate; RateInfo.Text = page.Info; currentHufQuote = page.Quote;
        RateWarning.Text = page.Warning; RateWarning.Visibility = page.WarningVisibility;
        BuildMultipliers(); ApplyModeLabels(); initialized = true; SelectMultiplier(page.Multiplier); UpdateSidebar(); PriceInput.Focus();
        if (currentHufQuote == null) await CheckStartupCurrency(false);
    }
    private async void RefreshAll(object sender, RoutedEventArgs e)
    {
        if (pendingFetches > 0) return;
        RefreshAllButton.IsEnabled = false;
        try { await (eurMode ? FetchCurrency(false) : Task.WhenAll(FetchCurrency(false), FetchCurrency(true), RefreshStartupEuroHuf())); }
        finally { if (!lifetime.IsCancellationRequested) RefreshAllButton.IsEnabled = true; }
    }
    public MainWindow(UserAccountStore accounts,UserSession session)
    {
        this.session=session;
        rateService = new ExchangeRateService(http, LogRateFailure);
        updateService = new UpdateService(updateHttp);
        var apiLog = new ApiLogStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CAT-Price-Calculator", "dhl-api-log.json"));
        var dhlService = new DhlTrackingService(trackingHttp, new TrackingRequestBudget(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CAT-Price-Calculator", "dhl-request-budget.json")), log: entry => { apiLog.Append(entry); });
        var upsService = new UpsTrackingService(trackingHttp, new TrackingRequestBudget(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CAT-Price-Calculator", "ups-request-budget.json"), carrier: "UPS"), log: entry => { apiLog.Append(entry); });
        InitializeComponent();
        UsersPage.Configure(accounts,session);
        UsersPage.AccountChanged+=updated=>{this.session=updated;ServicePage.SetRecorder(updated.Name);ApplyMenuTheme();};
        ServicePage.SetRecorder(session.Name);
        ServicePage.SummaryChanged+=()=>ServiceSummary.Text=ServiceSummaryText();
        ServiceImportPage.Configure(ServicePage.ImportCatalogAsync,ServicePage.OpenImportFolder);
        ServicePage.EntryRequested+=()=>{OpenServiceCalendar(this,new RoutedEventArgs());if(WindowState==WindowState.Minimized)WindowState=WindowState.Normal;Activate();};
        ApiLogPage.Configure(apiLog);
        string data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CAT-Price-Calculator");
        google=new GoogleSyncCoordinator(data,()=>Can("menu.service"),lifetime.Token);
        GoogleSettingsPage.Configure(google,()=>Can("settings.google"));
        google.Changed+=()=>ServiceSummary.Text=ServiceSummaryText();
        ServicePage.CalendarChanged+=async()=>await google.MarkChangedAsync();
        var dhlStore = new DhlSettingsStore(Path.Combine(data, "dhl-connection.bin"), new WindowsSecretProtector());
        var upsStore = new UpsSettingsStore(Path.Combine(data, "ups-connection.bin"), new WindowsSecretProtector("UPS"));
        var fedexStore = new FedExSettingsStore(Path.Combine(data, "fedex-connection.bin"), new WindowsSecretProtector("FedEx"));
        var glsStore = new GlsSettingsStore(Path.Combine(data, "gls-connection.bin"), new WindowsSecretProtector("GLS"));
        var fedexService = new FedExTrackingService(trackingHttp, new TrackingRequestBudget(Path.Combine(data, "fedex-request-budget.json"), carrier: "FedEx"), log: e => apiLog.Append(e));
        var glsService = new GlsTrackingService(trackingHttp, new TrackingRequestBudget(Path.Combine(data, "gls-request-budget.json"), carrier: "GLS"), log: e => apiLog.Append(e));
        carrierBindings[Courier.Dhl] = CarrierBinding.Create(Courier.Dhl, "DHL", dhlStore.Load(),
            s => new(s.ApiKey, Postal: s.RecipientPostalCode, Service: s.Service, DailyLimit: s.DailyLimit), i => new DhlConnectionSettings(i.Primary, i.Postal, i.Service, i.DailyLimit),
            s => s.IsValid, s => s.HasKey, dhlStore.Save, dhlService.FetchAsync);
        carrierBindings[Courier.Ups] = CarrierBinding.Create(Courier.Ups, "UPS", upsStore.Load(),
            s => new(s.ClientId, s.ClientSecret, s.AccountNumber, DailyLimit: s.DailyLimit), i => new UpsConnectionSettings(i.Primary, i.Secondary, i.Account, i.DailyLimit),
            s => s.IsValid, s => s.HasCredentials, upsStore.Save, upsService.FetchAsync, upsService.ForgetToken);
        carrierBindings[Courier.FedEx] = CarrierBinding.Create(Courier.FedEx, "FedEx", fedexStore.Load(),
            s => new(s.ClientId, s.ClientSecret, DailyLimit: s.DailyLimit), i => new FedExConnectionSettings(i.Primary, i.Secondary, i.DailyLimit),
            s => s.IsValid, s => s.HasCredentials, fedexStore.Save, fedexService.FetchAsync, fedexService.ForgetToken);
        carrierBindings[Courier.Gls] = CarrierBinding.Create(Courier.Gls, "GLS", glsStore.Load(),
            s => new(s.Username, s.Password, s.ClientNumber, DailyLimit: s.DailyLimit), i => new GlsConnectionSettings(i.Primary, i.Secondary, i.Account, i.DailyLimit),
            s => s.IsValid, s => s.HasCredentials, glsStore.Save, glsService.FetchAsync);
        CarrierSettingsPage.Configure(lifetime.Token);
        RoboSanyiPage.Configure(carrierBindings, lifetime.Token);
        CarrierSettingsPage.ConnectionChanged += (_, _) => RoboSanyiPage.ConnectionChanged();
        RoboSanyiPage.SettingsRequested += (_, carrier) => { var section=carrierBindings[carrier].Name;if(!Can(UserAccess.ForSettings(section)))return;settingsSection=section;OpenSettings(this,new RoutedEventArgs()); };
        RoboSanyiPage.SummaryChanged += (_, _) => { RoboTransitCount.Text = RoboSanyiPage.TransitCount.ToString(); RoboDeliveredCount.Text = RoboSanyiPage.DeliveredCount.ToString(); };
        AddHandler(System.Windows.Controls.Primitives.Thumb.DragDeltaEvent, new System.Windows.Controls.Primitives.DragDeltaEventHandler(DragTopmostSwitch));
        AddHandler(System.Windows.Controls.Primitives.Thumb.DragCompletedEvent, new System.Windows.Controls.Primitives.DragCompletedEventHandler(FinishTopmostSwitch));
        var startupPreferences = startupPreferencesStore.Load();
        Topmost = startupPreferences.AlwaysOnTop;
        StartupTopmostSwitch.IsChecked = startupPreferences.AlwaysOnTop;
        StartupRateSwitch.IsChecked = startupPreferences.RefreshRates;
        allowBetaUpdates = startupPreferences.AllowBetaUpdates;
        BetaUpdatesSwitch.IsChecked = allowBetaUpdates;
        RestoreStableButton.Visibility = AppVersion.IsBeta ? Visibility.Visible : Visibility.Collapsed;
        CurrentVersionText.Text = "v" + AppVersion;
        ReleaseNotesTitle.Text = "Legújabb verzió • v" + AppVersion;
        UpdateBetaStatus();
        startupPreferencesReady = true;
        lightTheme = appearanceStore.Load() == Appearance.Light;
        ApplyAppearance();
        Width = Math.Min(Width, SystemParameters.WorkArea.Width - 24);
        Height = Math.Min(Height, SystemParameters.WorkArea.Height - 24);
        Width = Math.Min(Width, (Height - 39) * 890 / 940 + 22);
        BuildMultipliers();
        var saved = settings.Load();
        sellingEuroRate = eurHufSettings.Load();
        RateInput.Text = (saved?.Rate ?? 53.50m).ToString("0.########", PriceCalculator.Hungarian);
        RateInfo.Text = saved?.Source is not null ? QuoteInfo(saved.Rate, saved.Source, saved.Date, retrievedAt: saved.RetrievedAt) + " (mentett)" : "Manuális árfolyam";
        var savedEuro = euroSettings.Load();
        EuroRateInput.Text = (savedEuro?.Rate ?? 0.134m).ToString("0.########", PriceCalculator.Hungarian);
        EuroRateInfo.Text = savedEuro?.Source is not null ? QuoteInfo(savedEuro.Rate, savedEuro.Source, savedEuro.Date, "EUR", savedEuro.RetrievedAt) + " (mentett)" : "Manuális árfolyam";
        initialized = true;
        SelectMultiplier(PriceCalculator.DefaultSellingMultiplier);
        ApplyModeLabels();
        UpdateSidebar();
        if(!Can("menu.cat")){
            if(Can("menu.eur"))SwitchCalculator(EurMenu,new RoutedEventArgs());
            else if(Can("menu.service"))OpenServiceCalendar(this,new RoutedEventArgs());
            else if(Can("menu.robo"))OpenRoboSanyi(this,new RoutedEventArgs());
            else OpenSettings(this,new RoutedEventArgs());
        }
        SourceInitialized += (_, _) => WindowTheme.Apply(this, lightTheme);
        Loaded += async (_, _) =>
        {
            if(Can("menu.robo"))RoboSanyiPage.InitializeData();
            var googleStartup=Can("menu.service")?google.AutoSyncAsync():Task.CompletedTask;
            PriceInput.Focus();
            await Task.WhenAll(Can("menu.cat") || Can("menu.eur")?StartupRatesAsync(startupPreferences.RefreshRates):Task.CompletedTask, CheckUpdatesAsync(false),googleStartup);
        };
        Deactivated+=(_,_)=>UsersPage.HideSecrets();
        Closed += (_, _) => { google?.Dispose();UsersPage.HideSecrets();ServicePage.ClosePopups(); lifetime.Cancel(); RoboSanyiPage.Dispose(); trackingHttp.Dispose(); upsService.ForgetToken(); http.Dispose(); updateHttp.Dispose(); lifetime.Dispose(); };
    }
    private string QuoteInfo(decimal rate, string source, DateOnly? date, string currency = "HUF", DateTimeOffset? retrievedAt = null)
    {
        var fetched = RateTimestamp.Format(retrievedAt);
        return $"1 {(eurMode && currency == "HUF" ? "EUR" : "DKK")} = {rate.ToString("0.########", PriceCalculator.Hungarian)} {currency} • {source}\nÁrfolyamnap: {date:yyyy.MM.dd.} • Lekérve: {fetched}";
    }
    private void ClearPrice(object sender, RoutedEventArgs e)
    {
        PriceInput.Clear();
        PriceInput.Focus();
    }
    private void InputsChanged(object sender, TextChangedEventArgs e) { if (initialized) Recalculate(); }
    private void RateChanged(object sender, TextChangedEventArgs e)
    {
        if (!initialized) return;
        rateRevision++;
        if (eurMode) eurHufRevision++;
        if (settingOnline) return;
        if (eurMode) sellingEuroRate = PriceCalculator.TryPositive(RateInput.Text, out var eurHuf) ? new(eurHuf) : null;
        UpdateRateWarning(false);
        RateInfo.Text = "Manuális árfolyam";
        Recalculate();
        UpdateSidebar();
        if (PriceCalculator.TryPositive(RateInput.Text, out var rate) && !ActiveSettings.Save(new(rate)))
            StatusText.Text = "Az árfolyam nem menthető. Ebben a munkamenetben használható.";
    }
    private void EuroRateChanged(object sender, TextChangedEventArgs e)
    {
        if (!initialized) return;
        euroRateRevision++;
        if (settingEuroOnline) return;
        UpdateRateWarning(true);
        EuroRateInfo.Text = "Manuális árfolyam";
        Recalculate();
        UpdateSidebar();
        if (PriceCalculator.TryPositive(EuroRateInput.Text, out var rate) && !euroSettings.Save(new(rate)))
            StatusText.Text = "Az euróárfolyam nem menthető. Ebben a munkamenetben használható.";
    }
    private void SelectMultiplier(decimal value)
    {
        multiplier = value;
        foreach (var button in buttons)
        {
            bool selected = (decimal)button.Tag == value;
            SetSelected(button, selected);
        }
        SelectedText.Text = "Kiválasztott szorzó: × " + value.ToString("F2", PriceCalculator.Hungarian);
        Recalculate();
    }
    private void Recalculate()
    {
        selling = null; CopyButton.IsEnabled = false;
        UpdateSellingEuro();
        EuroText.Text = DealerText.Text = CostText.Text = SellingText.Text = "—";
        ProfitText.Text = "Haszon: —";
        StatusText.Text = "";
        if (!PriceCalculator.TryPositive(PriceInput.Text, out var price))
        {
            if (PriceInput.Text.Length > 0) StatusText.Text = eurMode ? "Adjon meg nullánál nagyobb EUR árat." : "Adjon meg nullánál nagyobb CAT árat.";
            return;
        }
        // The EUR display is independent of dealer markup, HUF rate and selling multiplier.
        if (eurMode) EuroText.Text = price.ToString("N2", PriceCalculator.Hungarian) + " EUR";
        else if (PriceCalculator.TryPositive(EuroRateInput.Text, out var euroRate))
        {
            try { EuroText.Text = PriceCalculator.ConvertToEuro(price, euroRate).ToString("N2", PriceCalculator.Hungarian) + " EUR"; }
            catch (OverflowException) { StatusText.Text = "Az euróösszeg túl nagy. Adjon meg kisebb értéket."; }
        }
        else StatusText.Text = "Adjon meg érvényes, pozitív DKK/EUR árfolyamot.";
        try
        {
            DealerText.Text = (price * (eurMode ? 1m : PriceCalculator.DealerMultiplier)).ToString("N2", PriceCalculator.Hungarian) + (eurMode ? " EUR" : " DKK");
            if (!PriceCalculator.TryPositive(RateInput.Text, out var rate))
            {
                StatusText.Text = eurMode ? "Adjon meg érvényes, pozitív EUR/HUF árfolyamot." : "Adjon meg érvényes, pozitív DKK/HUF árfolyamot.";
                return;
            }
            var result = eurMode ? PriceCalculator.CalculateEuro(price, rate, multiplier ?? PriceCalculator.DefaultSellingMultiplier) : PriceCalculator.Calculate(price, rate, multiplier ?? PriceCalculator.DefaultSellingMultiplier);
            CostText.Text = result.Cost.ToString("N2", PriceCalculator.Hungarian).Replace('\u00a0', ' ').Replace('\u202f', ' ') + " Ft";
            if (multiplier.HasValue)
            {
                selling = result.Selling; SellingText.Text = PriceCalculator.Forints(result.Selling); CopyButton.IsEnabled = true;
                UpdateSellingEuro();
                var profit = PriceCalculator.Profit(result.Cost, result.Selling);
                var margin = profit.MarginPercent?.ToString("0.00", PriceCalculator.Hungarian) + (profit.MarginPercent.HasValue ? "%" : "—");
                ProfitText.Text = $"Haszon: {profit.Amount.ToString("N2", PriceCalculator.Hungarian)} Ft • Árrés: {margin}";
            }
        }
        catch (OverflowException) { StatusText.Text = "A megadott összeg túl nagy. Adjon meg kisebb értéket."; }
    }
    private async void FetchRate(object sender, RoutedEventArgs e) => await FetchCurrency(false);
    private async void FetchEuroRate(object sender, RoutedEventArgs e) => await FetchCurrency(true);
    private async Task FetchCurrency(bool euro)
    {
        var button = euro ? EuroFetchButton : FetchButton;
        CatMenu.IsEnabled = EurMenu.IsEnabled = false;
        pendingFetches++;
        button.IsEnabled = false; StatusText.Text = "Árfolyam lekérése…";
        var revision = euro ? euroRateRevision : rateRevision;
        try
        {
            var service = rateService;
            var quote = euro ? await service.FetchEuroAsync(lifetime.Token) : eurMode ? await FetchSharedEuroHufAsync() : await service.FetchAsync(lifetime.Token);
            if (lifetime.IsCancellationRequested) return;
            if (revision != (euro ? euroRateRevision : rateRevision)) { StatusText.Text = "A közben módosított manuális árfolyam megmaradt. Új lekéréshez nyomja meg a gombot."; return; }
            if (euro) currentEuroQuote = quote; else currentHufQuote = quote;
            var appliedRate = decimal.Round(quote.Rate, 8, MidpointRounding.AwayFromZero);
            if (!euro && eurMode)
            {
                currentEurHufQuote = quote;
                sellingEuroRate = new(appliedRate, quote.Source, quote.Date, quote.RetrievedAt);
            }
            if (euro) settingEuroOnline = true; else settingOnline = true;
            try { (euro ? EuroRateInput : RateInput).Text = appliedRate.ToString("0.########", PriceCalculator.Hungarian); }
            finally { if (euro) settingEuroOnline = false; else settingOnline = false; }
            (euro ? EuroRateInfo : RateInfo).Text = QuoteInfo(appliedRate, quote.Source, quote.Date, euro ? "EUR" : "HUF", quote.RetrievedAt);
            Recalculate();
            UpdateRateWarning(euro);
            if (!(euro ? euroSettings : ActiveSettings).Save(new(appliedRate, quote.Source, quote.Date, quote.RetrievedAt))) { if (startupRefreshing) startupRefreshFailed = true; StatusText.Text = "Az árfolyam nem menthető. Ebben a munkamenetben használható."; }
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException or System.IO.InvalidDataException or FormatException or InvalidOperationException or KeyNotFoundException)
        {
            if (startupRefreshing) startupRefreshFailed = true;
            if (!lifetime.IsCancellationRequested) StatusText.Text = "Az aktuális árfolyam nem kérhető le. A korábbi érték megmaradt. Ellenőrizze az internetkapcsolatot. Hibanapló: arfolyam-hiba.log.";
        }
        finally { pendingFetches--; if (!lifetime.IsCancellationRequested) { button.IsEnabled = true; CatMenu.IsEnabled = EurMenu.IsEnabled = pendingFetches == 0; UpdateSidebar(); } }
    }
    private async Task CheckStartupCurrency(bool euro)
    {
        var modeAtStart = eurMode;
        var warning = euro ? EuroRateWarning : RateWarning;
        warning.Text = "Induláskori árfolyam-ellenőrzés…";
        warning.SetResourceReference(TextBlock.ForegroundProperty, "Palette9CADC3");
        warning.Visibility = Visibility.Visible;
        try
        {
            var service = rateService;
            var quote = euro ? await service.FetchEuroAsync(lifetime.Token) : eurMode ? currentEurHufQuote ?? await FetchSharedEuroHufAsync() : await service.FetchAsync(lifetime.Token);
            if (lifetime.IsCancellationRequested) return;
            if (modeAtStart != eurMode) return;
            // Read-only: startup never writes a rate input, provenance label or settings file.
            if (euro) currentEuroQuote ??= quote; else currentHufQuote ??= quote;
            UpdateRateWarning(euro);
        }
        catch (Exception error) when (error is HttpRequestException or OperationCanceledException)
        {
            if (lifetime.IsCancellationRequested) return;
            if (modeAtStart != eurMode) return;
            if ((euro ? currentEuroQuote : currentHufQuote) != null) { UpdateRateWarning(euro); return; }
            warning.Text = "Az induláskori ellenőrzés nem sikerült. A beállított árfolyam továbbra is használható.";
            warning.SetResourceReference(TextBlock.ForegroundProperty, "Palette9CADC3");
            warning.Visibility = Visibility.Visible;
        }
    }
    private void UpdateRateWarning(bool euro)
    {
        var quote = euro ? currentEuroQuote : currentHufQuote;
        if (quote == null) return;
        var warning = euro ? EuroRateWarning : RateWarning;
        var input = euro ? EuroRateInput : RateInput;
        if (!PriceCalculator.TryPositive(input.Text, out var configuredRate) || !RateDeviation.IsSignificant(configuredRate, quote.Rate))
        {
            warning.Text = "";
            warning.Visibility = Visibility.Collapsed;
            return;
        }
        var currency = euro ? "EUR" : "HUF";
        var baseCurrency = eurMode && !euro ? "EUR" : "DKK";
        warning.ToolTip = $"A beállított árfolyam több mint 3%-kal eltér az aktuálistól. 1 {baseCurrency} = {quote.Rate.ToString("0.########", PriceCalculator.Hungarian)} {currency} ({quote.Source}, {quote.Date:yyyy.MM.dd.}).";
        warning.Text = $"Eltérés >3% • Aktuális: 1 {baseCurrency} = {quote.Rate.ToString("0.########", PriceCalculator.Hungarian)} {currency}";
        warning.SetResourceReference(TextBlock.ForegroundProperty, "Palette65B5FF");
        warning.Visibility = Visibility.Visible;
    }
    private static void LogRateFailure(string endpoint, Exception error)
    {
        try
        {
            var directory = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CAT-Price-Calculator");
            System.IO.Directory.CreateDirectory(directory);
            var path = System.IO.Path.Combine(directory, "arfolyam-hiba.log");
            if (System.IO.File.Exists(path) && new System.IO.FileInfo(path).Length > 128_000) System.IO.File.Delete(path);
            System.IO.File.AppendAllText(path, $"{DateTimeOffset.Now:O} | {endpoint} | {error.GetType().Name}: {error.Message} | {error.InnerException?.Message}\n");
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException) { }
    }
    private void CopyPrice(object sender, RoutedEventArgs e)
    {
        if (selling is not decimal value) return;
        try { Clipboard.SetText(PriceCalculator.Forints(value)); StatusText.Text = "Az eladási ár a vágólapra került."; }
        catch (System.Runtime.InteropServices.ExternalException) { StatusText.Text = "A vágólap most nem érhető el. Próbálja újra."; }
    }
    private void NewCalculation(object? sender, RoutedEventArgs? e)
    {
        PriceInput.Clear(); SelectMultiplier(PriceCalculator.DefaultSellingMultiplier); PriceInput.Focus();
    }
    private void SetUpdateBusy(bool busy)
    {
        updateBusy = busy;
        UpdateButton.IsEnabled = BetaUpdatesSwitch.IsEnabled = !busy && Can("menu.updates");
        RestoreStableButton.IsEnabled = !busy && AppVersion.IsBeta && Can("menu.updates");
    }
    private void UpdateBetaStatus(string? message = null)
    {
        BetaUpdateStatus.Text = $"Telepített verzió: v{AppVersion} • {(AppVersion.IsBeta ? "béta" : "stabil")}\n" +
            (message ?? (allowBetaUpdates ? "A stabil és béta frissítések engedélyezve." : "Csak stabil frissítések engedélyezve."));
    }
    private async void BetaUpdatesChanged(object sender, RoutedEventArgs e)
    {
        if (!startupPreferencesReady || suppressBetaChanges || !Can("menu.updates")) return;
        bool requested = BetaUpdatesSwitch.IsChecked == true;
        var preferences = new StartupPreferences(StartupTopmostSwitch.IsChecked == true, StartupRateSwitch.IsChecked == true, requested);
        if (!startupPreferencesStore.Save(preferences))
        {
            suppressBetaChanges = true;
            try { BetaUpdatesSwitch.IsChecked = allowBetaUpdates; }
            finally { suppressBetaChanges = false; }
            UpdateBetaStatus("A választás nem menthető. A korábbi frissítési csatorna maradt érvényben.");
            return;
        }
        allowBetaUpdates = requested;
        availableUpdate = null;
        UpdateBetaStatus();
        await CheckUpdatesAsync(true);
    }
    private async Task CheckUpdatesAsync(bool explicitCheck)
    {
        if (updateBusy || !Can("menu.updates")) return;
        SetUpdateBusy(true);
        try
        {
            availableUpdate = await updateService.CheckAsync(AppVersion, allowBetaUpdates, lifetime.Token);
            if (lifetime.IsCancellationRequested) return;
            UpdateInfo.Text = availableUpdate != null ? $"Új {(availableUpdate.IsBeta ? "béta" : "stabil")} verzió: v{availableUpdate.DisplayVersion}. A frissítéshez kattintson a gombra." : explicitCheck ? "Ezen a csatornán nincs újabb verzió." : "";
            if (UpdateButton.Content is StackPanel content && content.Children[1] is TextBlock label) label.Text = availableUpdate != null ? "Frissítés" : "Frissítések";
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException or InvalidDataException or InvalidOperationException or KeyNotFoundException)
        {
            if (!lifetime.IsCancellationRequested) UpdateInfo.Text = explicitCheck ? "A frissítések most nem ellenőrizhetők. Az alkalmazás továbbra is használható." : "";
        }
        finally { if (!lifetime.IsCancellationRequested) SetUpdateBusy(false); }
    }
    private async void UpdatesClicked(object sender, RoutedEventArgs e)
    {
        if (updateBusy || !Can("menu.updates")) return;
        if (availableUpdate == null) { await CheckUpdatesAsync(true); return; }
        await InstallReleaseAsync(availableUpdate, false);
    }
    private async void RestoreStableClicked(object sender, RoutedEventArgs e)
    {
        if (updateBusy || !AppVersion.IsBeta || !Can("menu.updates")) return;
        SetUpdateBusy(true);
        try
        {
            var stable = await updateService.GetLatestStableAsync(lifetime.Token);
            if (lifetime.IsCancellationRequested) return;
            if (stable == null) { UpdateBetaStatus("Nem található stabil kiadás. A béta verzió továbbra is használható."); return; }
            await InstallReleaseAsync(stable, true);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException or InvalidDataException or InvalidOperationException or KeyNotFoundException)
        {
            if (!lifetime.IsCancellationRequested) UpdateBetaStatus("A stabil kiadás most nem kérhető le. Próbáld újra később.");
        }
        finally { if (!lifetime.IsCancellationRequested) SetUpdateBusy(false); }
    }
    private async Task InstallReleaseAsync(UpdateRelease release, bool restoreStable)
    {
        if(!Can("menu.updates"))return;
        string question = restoreStable
            ? $"Visszatér a legfrissebb stabil kiadásra (v{release.DisplayVersion})? A béta frissítések kikapcsolnak. A program újraindul; a mentett beállítások és árfolyamok megmaradnak."
            : $"Letölti és telepíti a v{release.DisplayVersion} {(release.IsBeta ? "béta" : "stabil")} frissítést? A program újraindul; a mentett beállítások és árfolyamok megmaradnak.";
        if (MessageBox.Show(question, "Linser Hungary - frissítés", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        SetUpdateBusy(true);
        bool betaDisabled = false, updateStarted = false;
        try
        {
            var stage = Path.Combine(Path.GetTempPath(), "LS-CAT-update-" + Guid.NewGuid().ToString("N"));
            var progress = new Progress<int>(value =>
            {
                UpdateInfo.Text = $"Frissítés letöltése: {value}%";
                if (restoreStable) UpdateBetaStatus($"Stabil kiadás letöltése: {value}%");
            });
            var exe = await updateService.PrepareAsync(release, stage, progress, lifetime.Token);
            if (lifetime.IsCancellationRequested) return;
            if (restoreStable)
            {
                var preferences = new StartupPreferences(StartupTopmostSwitch.IsChecked == true, StartupRateSwitch.IsChecked == true, false);
                if (!startupPreferencesStore.Save(preferences)) throw new IOException("Cannot save stable update preference.");
                betaDisabled = true;
            }
            var start = new ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = stage };
            start.ArgumentList.Add("--apply-update"); start.ArgumentList.Add(Environment.ProcessPath!); start.ArgumentList.Add(Environment.ProcessId.ToString());
            using var updater = Process.Start(start) ?? throw new InvalidOperationException("Update process not started.");
            updateStarted = true;
            Close();
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or IOException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            if (betaDisabled && !updateStarted) startupPreferencesStore.Save(new(StartupTopmostSwitch.IsChecked == true, StartupRateSwitch.IsChecked == true, allowBetaUpdates));
            if (!lifetime.IsCancellationRequested)
            {
                UpdateInfo.Text = "A frissítés nem sikerült. A jelenlegi verzió továbbra is használható.";
                if (restoreStable) UpdateBetaStatus("A visszaállítás nem sikerült. A béta verzió továbbra is használható.");
            }
        }
        finally { if (!lifetime.IsCancellationRequested) SetUpdateBusy(false); }
    }
    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if(!Can(eurMode?"menu.eur":"menu.cat") && !settingsOpen)return;
        if (serviceOpen) return;
        if (roboOpen) return;
        if (settingsOpen) { if (e.Key == Key.Escape) { ShowCalculator(); e.Handled = true; } return; }
        if (e.Key == Key.Escape || (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.N)) { NewCalculation(null, null); e.Handled = true; }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key >= Key.D1 && e.Key <= Key.D7) { SelectMultiplier((eurMode ? PriceCalculator.EuroSellingMultipliers : PriceCalculator.SellingMultipliers)[(int)e.Key - (int)Key.D1]); e.Handled = true; }
        else if (e.Key == Key.Enter && e.OriginalSource is TextBox box) { box.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)); e.Handled = true; }
    }
}
