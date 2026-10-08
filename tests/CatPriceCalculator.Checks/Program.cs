using System.Net;
using CatPriceCalculator.Core;
var passed = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception(name); Console.WriteLine("PASS " + name); passed++; }
foreach (var text in new[] { "1250,50", "1 250,50", "1250.50", "1\u00a0250,50" }) Check(PriceCalculator.TryPositive(text, out var v) && v == 1250.50m, "Parse " + text);
foreach (var text in new[] { "0", "-1", "abc", "", "1,2.3" }) Check(!PriceCalculator.TryPositive(text, out _), "Reject " + text);
var result = PriceCalculator.Calculate(1250m, 53.5m, 1.4m);
Check(result == (1375m, 73562.50m, 102988m), "Required calculation");
Check(PriceCalculator.Forints(result.Selling) == "102 988 Ft", "Hungarian formatting");
foreach (var multiplier in PriceCalculator.SellingMultipliers) Check(PriceCalculator.Calculate(100m, 50m, multiplier).Selling == decimal.Round(5500m * multiplier, 0, MidpointRounding.AwayFromZero), "Multiplier " + multiplier);
try { PriceCalculator.Calculate(decimal.MaxValue, 50m, 1.4m); throw new Exception("Overflow was ignored"); } catch (OverflowException) { Check(true, "Overflow detected"); }
Check(PriceCalculator.ConvertToEuro(1250m, 0.134m) == 167.5m, "EUR uses raw CAT price without dealer markup");
Check(PriceCalculator.TryPositive("0,134025", out var euroParsed) && euroParsed == 0.134025m, "EUR rate precision and Hungarian input");
Check(PriceCalculator.EuroSellingMultipliers.Count == 15 && PriceCalculator.EuroSellingMultipliers.First() == 1.30m && PriceCalculator.EuroSellingMultipliers.Last() == 2m, "EUR multipliers 1.30 to 2.00");
foreach (var m in PriceCalculator.EuroSellingMultipliers) {
var eurResult = PriceCalculator.CalculateEuro(100m, 400m, m);
Check(eurResult.Dealer == 100m && eurResult.Cost == 40000m && eurResult.Selling == decimal.Round(40000m * m, 0, MidpointRounding.AwayFromZero), "EUR without dealer surcharge " + m);
}
Check(ReferenceEquals(PriceCalculator.SellingMultipliers, PriceCalculator.EuroSellingMultipliers), "CAT and EUR share the exact multiplier list");
Check(PriceCalculator.Calculate(100m, 50m, 2m).Selling == 11000m, "CAT accepts multiplier 2.00 with unchanged dealer surcharge");
var eurProfit = PriceCalculator.Profit(40000m, 56000m);
Check(eurProfit.Amount == 16000m && decimal.Round(eurProfit.MarginPercent!.Value, 2) == 28.57m, "EUR profit and margin");
Check(PriceCalculator.CalculateEuro(1m, 1m, 1.50m).Selling == 2m, "EUR whole-forint midpoint rounding");
foreach (var value in new[] { 0m, -1m }) {
try { PriceCalculator.CalculateEuro(value, 400m, 1.4m); throw new Exception("Invalid EUR accepted"); } catch (ArgumentOutOfRangeException) { Check(true, "Reject invalid EUR input"); }
}
using (var client = new HttpClient(new FakeHandler("{\"amount\":1,\"base\":\"EUR\",\"date\":\"2026-10-07\",\"rates\":{\"HUF\":400.25}}", expectedEndpoint: ExchangeRateService.EuroHufEndpoint))) {
Check((await new ExchangeRateService(client).FetchEuroHufAsync()).Rate == 400.25m, "EUR HUF fallback and direction");
}
using (var client = new HttpClient(new FakeHandler(null, expectedEndpoint: ExchangeRateService.EuroHufEndpoint))) {
try { await new ExchangeRateService(client).FetchEuroHufAsync(); throw new Exception("Offline ignored"); } catch (HttpRequestException) { Check(true, "EUR offline failure handled"); }
}
Check(RateTimestamp.Format(new DateTimeOffset(2026, 10, 7, 12, 34, 56, TimeSpan.Zero)) == "2026.10.07. 14:34:56 (magyar idő)", "Retrieval timestamp Hungarian summer time");
Check(RateTimestamp.Format(new DateTimeOffset(2026, 1, 7, 12, 34, 56, TimeSpan.Zero)) == "2026.01.07. 13:34:56 (magyar idő)", "Retrieval timestamp Hungarian winter time");
Check(RateTimestamp.Format(null) == "korábban nem rögzített", "Legacy timestamp not invented");
var appearanceDirectory = Path.Combine(Path.GetTempPath(), "linser-appearance-" + Guid.NewGuid());
try {
var preferencePath = Path.Combine(appearanceDirectory, "appearance.json");
var appearanceStore = new AppearanceStore(preferencePath);
Check(appearanceStore.Load() == Appearance.Dark, "Default appearance is dark");
Check(appearanceStore.Save(Appearance.Light) && new AppearanceStore(preferencePath).Load() == Appearance.Light, "Light appearance persists across store instances");
Check(appearanceStore.Save(Appearance.Dark) && appearanceStore.Load() == Appearance.Dark, "Switch back to dark persists");
File.WriteAllText(preferencePath, "bad json");
Check(appearanceStore.Load() == Appearance.Dark, "Corrupt appearance settings fall back safely");
File.WriteAllText(preferencePath, "{\"Theme\":\"Unknown\"}");
Check(appearanceStore.Load() == Appearance.Dark, "Unknown appearance falls back safely");
var blocker = Path.Combine(appearanceDirectory, "blocked"); File.WriteAllText(blocker, "not a directory");
Check(!new AppearanceStore(Path.Combine(blocker, "appearance.json")).Save(Appearance.Light), "Appearance save failure returned without crash");
} finally { if (Directory.Exists(appearanceDirectory)) Directory.Delete(appearanceDirectory, true); }
var startupDirectory = Path.Combine(Path.GetTempPath(), "linser-startup-" + Guid.NewGuid());
try {
var preferencePath = Path.Combine(startupDirectory, "startup.json");
var store = new StartupPreferencesStore(preferencePath);
Check(store.Load() == new StartupPreferences(false, false), "Startup defaults preserve saved rates and normal window");
foreach (var topmost in new[] {false, true}) foreach (var refresh in new[] {false, true}) {
var value = new StartupPreferences(topmost, refresh);
Check(store.Save(value) && new StartupPreferencesStore(preferencePath).Load() == value, "Independent startup options persist " + value);
}
File.WriteAllText(preferencePath, "bad json");
Check(store.Load() == new StartupPreferences(), "Corrupt startup preferences fall back safely");
File.WriteAllText(preferencePath, "{}");
Check(store.Load() == new StartupPreferences(), "Missing preference properties have safe defaults");
var blocker = Path.Combine(startupDirectory, "blocked"); File.WriteAllText(blocker, "not a directory");
Check(!new StartupPreferencesStore(Path.Combine(blocker, "startup.json")).Save(new(true,true)), "Startup preference save failure does not crash");
} finally { if (Directory.Exists(startupDirectory)) Directory.Delete(startupDirectory, true); }
var directory = Path.Combine(Path.GetTempPath(), "cat-tests-" + Guid.NewGuid());
try {
var store = new SettingsStore(Path.Combine(directory, "settings.json"));
Check(store.Load() is null, "Missing settings");
var saved = new RateSettings(53.5m, "Frankfurter / ECB", new DateOnly(2026, 10, 7));
Check(store.Save(saved) && store.Load() == saved, "Rate and provenance persist");
var euroStore = new SettingsStore(Path.Combine(directory, "euro-settings.json"));
var euroSaved = new RateSettings(0.134025m, "Frankfurter / ECB", new DateOnly(2026,10,7));
Check(euroStore.Save(euroSaved) && euroStore.Load() == euroSaved && store.Load() == saved, "Independent EUR and legacy HUF settings");
var timestamp = new DateTimeOffset(2026, 10, 7, 12, 34, 56, TimeSpan.Zero);
Check(store.Save(saved with { RetrievedAt = timestamp }) && store.Load()?.RetrievedAt == timestamp, "Retrieval timestamp survives settings roundtrip");
File.WriteAllText(Path.Combine(directory, "settings.json"), "{\"Rate\":53.5,\"Source\":\"ECB\",\"Date\":\"2026-10-07\"}");
Check(store.Load()?.Rate == 53.5m && store.Load()?.RetrievedAt == null, "Legacy settings remain readable without timestamp");
File.WriteAllText(Path.Combine(directory, "settings.json"), "invalid");
Check(store.Load() is null, "Corrupt settings tolerated");
} finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
using (var http = new HttpClient(new FakeHandler("{\"amount\":1,\"base\":\"DKK\",\"date\":\"2026-10-07\",\"rates\":{\"HUF\":53.42}}"))) {
var quote = await new ExchangeRateService(http).FetchAsync();
Check(quote.RetrievedAt.HasValue && (DateTimeOffset.UtcNow - quote.RetrievedAt.Value).TotalSeconds < 5, "Successful retrieval records actual time");
Check(quote.Rate == 53.42m && quote.Date == new DateOnly(2026,10,7), "API response and direction"); }
using (var http = new HttpClient(new FakeHandler("{\"amount\":1,\"base\":\"DKK\",\"date\":\"2026-10-07\",\"rates\":{\"EUR\":0.134025}}", expectedEndpoint: ExchangeRateService.EuroEndpoint))) {
var quote = await new ExchangeRateService(http).FetchEuroAsync();
Check(quote.Rate == 0.134025m && quote.Source == "Frankfurter / ECB", "EUR endpoint, direction and decimal precision"); }
foreach (var payload in new[] { "{}", "{\"amount\":1,\"base\":\"HUF\"}", "bad json" }) {
using var http = new HttpClient(new FakeHandler(payload));
try { await new ExchangeRateService(http).FetchAsync(); throw new Exception("Invalid API accepted"); }
catch (Exception e) when (e is HttpRequestException) { Check(true, "Invalid response rejected"); }
}
using (var http = new HttpClient(new FakeHandler(null))) {
try { await new ExchangeRateService(http).FetchAsync(); throw new Exception("Offline accepted"); } catch (HttpRequestException) { Check(true, "Offline failure propagated to UI handler"); } }
using (var http = new HttpClient(new FakeHandler("{}", HttpStatusCode.ServiceUnavailable))) {
try { await new ExchangeRateService(http).FetchAsync(); throw new Exception("503 accepted"); } catch (HttpRequestException) { Check(true, "API unavailable"); } }
using (var http = new HttpClient(new SlowHandler()) { Timeout = TimeSpan.FromMilliseconds(50) }) {
try { await new ExchangeRateService(http).FetchAsync(); throw new Exception("Timeout ignored"); } catch (HttpRequestException e) { Check(e.InnerException is AggregateException a && a.InnerExceptions.Count == 2 && a.InnerExceptions.All(f => f is OperationCanceledException), "Both source timeouts bounded"); } }
const string ecbXml = "<Envelope xmlns='http://www.ecb.int/vocabulary/2002-08-01/eurofxref'><Cube><Cube time='2026-10-07'><Cube currency='DKK' rate='7.5'/><Cube currency='HUF' rate='400'/></Cube></Cube></Envelope>";
using (var http = new HttpClient(new EcbHandler(ecbXml))) {
var service = new ExchangeRateService(http);
var huf = await service.FetchAsync(); var eur = await service.FetchEuroAsync();
Check(huf.Rate == 400m / 7.5m && eur.Rate == 1m / 7.5m, "ECB cross-rates: HUF/DKK and EUR/DKK direction");
Check(huf.Source.Contains("EKB") && huf.Date == new DateOnly(2026,10,7), "ECB provenance and date"); }
using (var http = new HttpClient(new EcbHandler("<invalid/>"))) {
try { await new ExchangeRateService(http).FetchAsync(); throw new Exception("Malformed ECB accepted"); }
catch (HttpRequestException) { Check(true, "Malformed ECB with failed fallback rejected"); } }
using (var http = new HttpClient(new FakeHandler("{\"amount\":1,\"base\":\"DKK\",\"date\":\"2026-10-07\",\"rates\":{\"HUF\":53.42}}"))) {
int logged=0;
var quote = await new ExchangeRateService(http, (_,_)=>logged++).FetchAsync();
Check(logged == 1 && quote.Rate == 53.42m, "Primary unavailable: fallback works and failure reported"); }
using (var http = new HttpClient(new EcbHandler(ecbXml))) {
using var cancelled=new CancellationTokenSource(); cancelled.Cancel();
try { await new ExchangeRateService(http).FetchAsync(cancelled.Token); throw new Exception("Cancellation ignored"); }
catch (OperationCanceledException) { Check(true, "Closing application cancels without retry"); } }
Check(!RateDeviation.IsSignificant(100m, 100m), "Same rate: no warning");
Check(!RateDeviation.IsSignificant(103m, 100m) && !RateDeviation.IsSignificant(97m, 100m), "Exactly 3 percent: no warning in either direction");
Check(RateDeviation.IsSignificant(103.0001m, 100m) && RateDeviation.IsSignificant(96.9999m, 100m), "Above 3 percent: warning in either direction");
Check(!RateDeviation.IsSignificant(102.9999m, 100m), "Below 3 percent: no warning");
Check(RateDeviation.IsSignificant(100m, 97m), "Current rate is the comparison denominator");
Check(!RateDeviation.IsSignificant(0.134m, 0.134m) && RateDeviation.IsSignificant(0.14m, 0.134m), "Decimal EUR comparison");
Check(RateDeviation.IsSignificant(decimal.MaxValue, 1m), "Large input comparison does not overflow");
Check(!RateDeviation.IsSignificant(1m, 1.03m), "Manual change toward reference clears warning");
var readOnlyDirectory = Path.Combine(Path.GetTempPath(), "cat-readonly-"+Guid.NewGuid());
try {
var store = new SettingsStore(Path.Combine(readOnlyDirectory,"settings.json"));
var original = new RateSettings(60m,"manual"); store.Save(original);
var bytesBefore=File.ReadAllBytes(Path.Combine(readOnlyDirectory,"settings.json"));
using var http = new HttpClient(new EcbHandler(ecbXml));
var reference=await new ExchangeRateService(http).FetchAsync();
Check(RateDeviation.IsSignificant(original.Rate,reference.Rate) && store.Load()==original && bytesBefore.SequenceEqual(File.ReadAllBytes(Path.Combine(readOnlyDirectory,"settings.json"))), "Reference fetch and comparison preserve stored rate and provenance");
} finally { if (Directory.Exists(readOnlyDirectory)) Directory.Delete(readOnlyDirectory,true); }
using (var client = new HttpClient(new EcbHandler(ecbXml))) {
var eurQuote = await new ExchangeRateService(client).FetchEuroHufAsync();
Check(eurQuote.Rate == 400m, "ECB direct EUR HUF without DKK conversion");
}
await UpdateChecks.Run(Check);
await OptimizationChecks.Run(Check);
await UpdateChannelChecks.Run(Check);
ParcelChecks.Run(Check);
await DhlChecks.Run(Check);
Console.WriteLine($"{passed} checks passed.");
if (args.Contains("--live")) {
using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
var quote = await new ExchangeRateService(http).FetchAsync();
Check(quote.Rate > 0 && quote.Date <= DateOnly.FromDateTime(DateTime.UtcNow), "Live DKK/HUF");
Console.WriteLine($"1 DKK = {quote.Rate} HUF; {quote.Source}; {quote.Date}");
var eurHuf = await new ExchangeRateService(http).FetchEuroHufAsync();
Check(eurHuf.Rate > 0, "Live EUR/HUF");
Console.WriteLine($"1 EUR = {eurHuf.Rate} HUF; {eurHuf.Source}; {eurHuf.Date}");
var euro = await new ExchangeRateService(http).FetchEuroAsync();
Check(euro.Rate > 0 && euro.Rate < 1 && euro.Date <= DateOnly.FromDateTime(DateTime.UtcNow), "Live DKK/EUR");
Console.WriteLine($"1 DKK = {euro.Rate} EUR; {euro.Source}; {euro.Date}");
}
if (args.Contains("--live-update") || args.Contains("--live-beta-update")) {
using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
var service = new UpdateService(http);
bool betaFeed = args.Contains("--live-beta-update");
var release = await service.CheckAsync(new ReleaseVersion(new Version(0,6,0)), betaFeed) ?? throw new Exception("No release found");
if (betaFeed) Check(release.IsBeta && release.DisplayVersion == BuildInfo.Current.ToString(), "Public beta feed selects the just-published beta build");
var liveUpdateDirectory=Path.Combine(Path.GetTempPath(),"cat-live-update-"+Guid.NewGuid());
try {
var exe=await service.PrepareAsync(release,liveUpdateDirectory);
using var file=File.OpenRead(exe);
Check(file.ReadByte()==0x4d && file.ReadByte()==0x5a,"Live public update metadata, download, integrity and Windows executable");
Console.WriteLine("Verified update: v"+release.DisplayVersion);
} finally { if (Directory.Exists(liveUpdateDirectory)) Directory.Delete(liveUpdateDirectory,true); }
}
sealed class FakeHandler(string? body, HttpStatusCode status = HttpStatusCode.OK, string expectedEndpoint = ExchangeRateService.Endpoint) : HttpMessageHandler {
protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) {
if (request.RequestUri?.AbsoluteUri == ExchangeRateService.EcbEndpoint) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
if (request.RequestUri?.AbsoluteUri != expectedEndpoint) throw new Exception("Wrong endpoint");
if (body is null) throw new HttpRequestException("Simulated offline");
return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
}}
sealed class SlowHandler : HttpMessageHandler {
protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) { await Task.Delay(10000, token); return new(HttpStatusCode.OK); }
}

sealed class EcbHandler(string body) : HttpMessageHandler {
protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) {
token.ThrowIfCancellationRequested();
return Task.FromResult(new HttpResponseMessage(request.RequestUri?.AbsoluteUri == ExchangeRateService.EcbEndpoint ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable) { Content=new StringContent(body) });
}}
