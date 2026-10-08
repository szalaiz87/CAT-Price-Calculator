using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CatPriceCalculator.Core;
public static class CarrierApiChecks
{
    private const string Number = "123456789012", Id = "test-only-fedex-id", Secret = "test-only-fedex-secret", Bearer = "test-only-fedex-bearer", User = "test-only@example.invalid", Password = " árvíztűrő test-only password ";
    public const string FedExBody = """
    {"output":{"completeTrackResults":[{"trackingNumber":"123456789012","trackResults":[{"trackingNumberInfo":{"trackingNumber":"123456789012"},"latestStatusDetail":{"derivedCode":"IT","description":"In transit"},"scanEvents":[{"date":"2026-10-08T10:00:00+02:00","eventType":"IT","scanLocation":{"city":"Vienna","countryCode":"AT"}}],"dateAndTimes":[{"type":"ESTIMATED_DELIVERY","dateTime":"2026-10-09T18:00:00+02:00"}]}]}]}}
    """;
    public const string GlsBody = """
    {"ParcelNumber":123456789012,"GetParcelStatusErrors":[],"DeliveryCountryCode":"HU","DeliveryZipCode":"1234","ParcelStatusList":[{"StatusCode":"3","StatusDate":"/Date(1791446400000+0200)/","DepotCity":"Budapest","DepotNumber":"01","StatusDescription":"Arrived at depot"}]}
    """;
    private const string TokenBody = "{\"access_token\":\"test-only-fedex-bearer\",\"token_type\":\"bearer\",\"expires_in\":3600}";
    private static HttpResponseMessage Reply(string body, HttpStatusCode status = HttpStatusCode.OK) => new(status) { Content = new StringContent(body) };
    private static ParcelTrackingResult FedEx(string body) { using var doc = JsonDocument.Parse(body); return FedExTrackingService.Parse(doc.RootElement, Number); }
    private static ParcelTrackingResult Gls(string body) { using var doc = JsonDocument.Parse(body); return GlsTrackingService.Parse(doc.RootElement, Number); }
    private static async Task Error(Func<Task> call, TrackingErrorCode code, Action<bool, string> check, string label)
    {
        try { await call(); throw new Exception("Expected tracking error: " + label); }
        catch (ParcelTrackingException e) { check(e.Code == code && !new[] { Id, Secret, Bearer, User, Password }.Any(e.Message.Contains), label); }
    }
    public static async Task Run(Action<bool, string> check)
    {
        string root = Path.Combine(Path.GetTempPath(), "linser-four-carriers-" + Guid.NewGuid()); Directory.CreateDirectory(root);
        var now = new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero); int seq = 0;
        TrackingRequestBudget Budget(string carrier) => new(Path.Combine(root, "budget-" + seq++ + ".json"), () => now, (delay, ct) => { ct.ThrowIfCancellationRequested(); now += delay; return Task.CompletedTask; }, carrier);
        var fedexSettings = new FedExConnectionSettings(Id, Secret);
        var glsSettings = new GlsConnectionSettings(User, Password, "123456");
        try
        {
            check(fedexSettings.IsValid && fedexSettings.HasCredentials && !new FedExConnectionSettings().HasCredentials, "FedEx requires both production project credentials");
            check(glsSettings.IsValid && glsSettings.HasCredentials && !new GlsConnectionSettings().HasCredentials, "GLS accepts Unicode password with exact whitespace and optional approved client number");
            check(!(fedexSettings with { ClientSecret = "bad\nsecret" }).IsValid && !(glsSettings with { ClientNumber = "-1" }).IsValid && !(glsSettings with { Username = "bad user" }).IsValid, "FedEx/GLS invalid credentials/options rejected");
            check(!new[] { Id, Secret, User, Password, "123456" }.Any(v => fedexSettings.ToString().Contains(v) || glsSettings.ToString().Contains(v)), "FedEx/GLS settings diagnostics redact all credentials/account");
            using (var protector = new DhlChecks.TestProtector())
            {
                var f = new FedExSettingsStore(Path.Combine(root, "f.bin"), protector); var g = new GlsSettingsStore(Path.Combine(root, "g.bin"), protector);
                check(f.Save(fedexSettings) && f.Load().Settings == fedexSettings && g.Save(glsSettings) && g.Load().Settings == glsSettings, "Both new protected settings formats survive restart with exact password");
                check(!Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(root, "g.bin"))).Contains(User), "GLS persisted settings are ciphertext");
                protector.Fail = true; check(!f.Save(fedexSettings with { ClientId = "changed" }) && !g.Save(glsSettings with { Password = "changed" }), "Both new settings fail closed without plaintext fallback"); protector.Fail = false;
                check(f.Load().Settings == fedexSettings && g.Load().Settings == glsSettings, "Failed protection leaves both previous settings intact");
                var bytes = File.ReadAllBytes(Path.Combine(root, "g.bin")); bytes[^1] ^= 1; File.WriteAllBytes(Path.Combine(root, "g.bin"), bytes);
                check(g.Load().Failed && !g.Load().Settings.HasCredentials, "Tampered GLS encrypted data rejected");
            }
            var fedex = FedEx(FedExBody); var gls = Gls(GlsBody);
            check(fedex.State == ParcelState.InTransit && fedex.Location == "Vienna, AT" && fedex.EventTimestamp == "2026-10-08T10:00:00+02:00" && TrackingTimestamp.Time(fedex.EventTimestamp) == "10:00", "FedEx official scan pairs last location/time and converts known zone to Hungarian time");
            check(fedex.DeliveryTimestamp == "2026-10-09T18:00:00+02:00", "FedEx structured ETA is preserved");
            check(gls.State == ParcelState.InTransit && gls.Location == "Budapest" && TrackingTimestamp.Instant(gls.EventTimestamp) == DateTimeOffset.FromUnixTimeMilliseconds(1791446400000), "GLS WCF date is Unix UTC instant without double-adding offset");
            check(gls.DeliveryTimestamp == null, "MyGLS API never invents unsupported ETA from destination/status text");
            foreach (var status in new[] { ("OD", ParcelState.OutForDelivery), ("OC", ParcelState.PreTransit), ("CP", ParcelState.Customs), ("DE", ParcelState.Exception), ("DL", ParcelState.Delivered), ("NEW", ParcelState.Unknown) })
                check(FedEx(FedExBody.Replace("\"derivedCode\":\"IT\"", "\"derivedCode\":\"" + status.Item1 + "\"")).State == status.Item2, "FedEx state mapping " + status.Item1);
            foreach (var status in new[] { ("4", ParcelState.OutForDelivery), ("51", ParcelState.PreTransit), ("60", ParcelState.Customs), ("23", ParcelState.Exception), ("40", ParcelState.Exception), ("5", ParcelState.Delivered), ("54", ParcelState.Delivered), ("55", ParcelState.Delivered), ("58", ParcelState.Delivered), ("NEW", ParcelState.Unknown) })
                check(Gls(GlsBody.Replace("\"StatusCode\":\"3\"", "\"StatusCode\":\"" + status.Item1 + "\"")).State == status.Item2, "GLS official status mapping " + status.Item1);
            var olderFedEx = FedEx(FedExBody.Replace("\"scanEvents\":[", "\"scanEvents\":[{\"date\":\"2026-10-09T12:00:00Z\",\"eventType\":\"IT\"},"));
            check(olderFedEx.Location == fedex.Location && olderFedEx.EventTimestamp == fedex.EventTimestamp, "FedEx newer unlocated scan does not redate previous location");
            var olderGls = Gls(GlsBody.Replace("\"ParcelStatusList\":[", "\"ParcelStatusList\":[{\"StatusCode\":\"4\",\"StatusDate\":\"/Date(1791532800000)/\"},"));
            check(olderGls.State == ParcelState.OutForDelivery && olderGls.Location == gls.Location && olderGls.EventTimestamp == gls.EventTimestamp, "GLS latest status and last located event keep their own timestamps");
            check(Gls(GlsBody.Replace("\"DepotCity\":\"Budapest\",\"DepotNumber\":\"01\",", "")).Location == null, "GLS destination country/postcode never substitutes last observed depot");
            foreach (var date in new[] { "/Date()/", "/Date(abc)/", "/Date(99999999999999999)/", "/Date(123+abc)/", "0001-01-01T00:00:00" })
                check(Gls(GlsBody.Replace("/Date(1791446400000+0200)/", date)).EventTimestamp == null, "GLS malformed/default timestamps have no invented instant: " + date);
            check(TrackingTimestamp.Time(Gls(GlsBody.Replace("/Date(1791446400000+0200)/", "2026-10-08T12:30:00")).EventTimestamp) == "12:30*", "GLS ISO unknown-zone time retains marked original value");
            await Error(() => Task.FromResult(FedEx("{}")), TrackingErrorCode.InvalidResponse, check, "FedEx malformed success rejected");
            await Error(() => Task.FromResult(FedEx(FedExBody.Replace("\"trackResults\":[", "\"trackResults\":[{},"))), TrackingErrorCode.Ambiguous, check, "FedEx recycled/multiple shipments never guessed");
            await Error(() => Task.FromResult(Gls(GlsBody.Replace("123456789012", "987654321012"))), TrackingErrorCode.Ambiguous, check, "GLS response must match requested parcel number");
            await Error(() => Task.FromResult(Gls("{}")), TrackingErrorCode.Ambiguous, check, "GLS malformed success never replaces old record");
            await Error(() => Task.FromResult(Gls(GlsBody.Replace("\"StatusCode\":\"3\",", ""))), TrackingErrorCode.InvalidResponse, check, "GLS absent status rejected");
            foreach (var pair in new[] { (5, TrackingErrorCode.PermissionDenied), (15, TrackingErrorCode.PermissionDenied), (14, TrackingErrorCode.Authentication), (27, TrackingErrorCode.Authentication), (9, TrackingErrorCode.NotFound), (10, TrackingErrorCode.NotFound), (26, TrackingErrorCode.NotFound), (31, TrackingErrorCode.Quota), (1000, TrackingErrorCode.Offline) })
                await Error(() => Task.FromResult(Gls("{\"GetParcelStatusErrors\":[{\"ErrorCode\":" + pair.Item1 + ",\"ErrorDescription\":\"test-only-fedex-secret\"}]}")), pair.Item2, check, "GLS safe provider error " + pair.Item1);
            check(!new ParcelTrackingException(TrackingErrorCode.PermissionDenied, "").StopsBatch, "GLS incoming-parcel permission denial does not block other GLS rows");
            foreach (var carrier in new[] { Courier.FedEx, Courier.Gls })
            {
                var parcel = ParcelBook.Add(Number, carrier, "", now);
                var result = carrier == Courier.FedEx ? fedex with { State = ParcelState.Delivered, DeliveryTimestamp = "2026-10-08T10:00:00Z" } : Gls(GlsBody.Replace("\"StatusCode\":\"3\"", "\"StatusCode\":\"5\""));
                var delivered = ParcelTracking.Apply(parcel, result, now, carrier);
                check(delivered.DeliveredAt == TrackingTimestamp.Instant(result.DeliveryTimestamp) && !delivered.RetentionFromObservation, "New carrier confirmed delivery starts actual 48-hour retention: " + carrier);
                check(ParcelTracking.Apply(delivered, result, now.AddHours(1), carrier).DeliveredAt == delivered.DeliveredAt && ParcelTracking.Apply(delivered, result with { State = ParcelState.InTransit }, now, carrier).State == ParcelState.Delivered, "New carrier repeated/stale delivery never resets retention: " + carrier);
                check(ParcelTracking.Apply(parcel, result with { DeliveryTimestamp = null }, now, carrier).RetentionFromObservation, "Missing new-carrier delivery time marks first observation: " + carrier);
                bool refused = false; try { ParcelTracking.Apply(parcel with { IsSample = true }, result, now, carrier); } catch (ArgumentException) { refused = true; }
                check(refused, "New carrier sample cannot be updated by real API: " + carrier);
            }
            var log = new List<ApiLogEntry>(); int oauth = 0, tracking = 0;
            using (var handler = new Handler(async (request, ct) =>
            {
                if (request.RequestUri!.AbsoluteUri == FedExTrackingService.TokenEndpoint)
                {
                    oauth++; string body = await request.Content!.ReadAsStringAsync(ct);
                    check(request.Headers.Authorization == null && body.Contains("client_id=" + Id) && body.Contains("client_secret=" + Secret) && body.Contains("grant_type=client_credentials"), "FedEx OAuth uses production POST/form project credentials without UPS Basic header"); return Reply(TokenBody);
                }
                tracking++; using var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
                check(request.Method == HttpMethod.Post && request.RequestUri.AbsoluteUri == FedExTrackingService.TrackingEndpoint && request.Headers.Authorization?.Scheme == "Bearer" && request.Headers.Authorization.Parameter == Bearer, "FedEx manual tracking uses production POST with isolated Bearer");
                check(payload.RootElement.GetProperty("includeDetailedScans").GetBoolean() && payload.RootElement.GetProperty("trackingInfo")[0].GetProperty("trackingNumberInfo").GetProperty("trackingNumber").GetString() == Number, "FedEx official request shape requests actual scans for exact number"); return Reply(FedExBody);
            }))
            using (var client = new HttpClient(handler))
            {
                var service = new FedExTrackingService(client, Budget("FedEx"), () => now, log.Add);
                check(handler.Calls == 0, "FedEx construction never performs OAuth or tracking");
                await Error(() => service.FetchAsync(new(), Number), TrackingErrorCode.Configuration, check, "FedEx missing credentials never contact network");
                await Error(() => service.FetchAsync(fedexSettings, "bad"), TrackingErrorCode.InvalidNumber, check, "FedEx invalid row rejected before OAuth");
                check(handler.Calls == 0, "FedEx invalid inputs consume no allowance");
                await service.FetchAsync(fedexSettings, Number); await service.FetchAsync(fedexSettings, Number);
                check(oauth == 1 && tracking == 2, "FedEx memory token reused only in explicit manual requests");
                now = now.AddHours(1); await service.FetchAsync(fedexSettings, Number); check(oauth == 2, "FedEx expiration only authenticates on next manual request");
                service.ForgetToken(); await service.FetchAsync(fedexSettings, Number); check(oauth == 3 && !client.DefaultRequestHeaders.Any(), "FedEx forget token and per-request header isolation preserved");
            }
            int attempts = 0;
            using (var client = new HttpClient(new Handler((r, _) => Task.FromResult(r.RequestUri!.AbsoluteUri == FedExTrackingService.TokenEndpoint ? Reply(TokenBody) : ++attempts == 1 ? Reply(Secret, HttpStatusCode.Unauthorized) : Reply(FedExBody)))))
                check((await new FedExTrackingService(client, Budget("FedEx"), () => now).FetchAsync(fedexSettings, Number)).State == ParcelState.InTransit && attempts == 2, "FedEx 401 permits one renewal within same manual action");
            attempts = 0;
            using (var client = new HttpClient(new Handler((r, _) => { attempts++; return Task.FromResult(Reply(r.RequestUri!.AbsoluteUri == FedExTrackingService.TokenEndpoint ? TokenBody : Secret, r.RequestUri.AbsoluteUri == FedExTrackingService.TokenEndpoint ? HttpStatusCode.OK : HttpStatusCode.Unauthorized)); })))
                await Error(() => new FedExTrackingService(client, Budget("FedEx"), () => now).FetchAsync(fedexSettings, Number), TrackingErrorCode.Authentication, check, "FedEx repeated 401 terminates without authentication loop");
            check(attempts == 4, "FedEx repeated 401 bounded to two OAuth and two tracking requests");
            using (var handler = new Handler(async (request, ct) =>
            {
                using var doc = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct)); var body = doc.RootElement;
                check(request.Method == HttpMethod.Post && request.RequestUri!.AbsoluteUri == GlsTrackingService.TrackingEndpoint && request.Headers.Authorization == null, "GLS production JSON tracking has no unrelated OAuth header");
                int[] password = body.GetProperty("Password").EnumerateArray().Select(x => x.GetInt32()).ToArray();
                check(password.Length == 64 && password.SequenceEqual(SHA512.HashData(Encoding.UTF8.GetBytes(Password)).Select(x => (int)x)), "MyGLS SHA512 password is numeric byte array, not base64, preserving Unicode/whitespace");
                check(body.GetProperty("Username").GetString() == User && body.GetProperty("ClientNumberList")[0].GetInt32() == 123456 && body.GetProperty("ParcelNumber").GetInt64() == 123456789012 && !body.GetProperty("ReturnPOD").GetBoolean() && body.GetProperty("LanguageIsoCode").GetString() == "HU", "MyGLS exact PascalCase official schema, approved client, no POD and Hungarian language");
                check(!body.GetRawText().Contains(Password), "GLS request never transmits plaintext password"); return Reply(GlsBody);
            }))
            using (var client = new HttpClient(handler))
            {
                var service = new GlsTrackingService(client, Budget("GLS"), () => now, log.Add);
                check(handler.Calls == 0, "GLS construction does not request tracking");
                await Error(() => service.FetchAsync(new(), Number), TrackingErrorCode.Configuration, check, "GLS unconfigured never contacts API");
                await Error(() => service.FetchAsync(glsSettings, "12ABC"), TrackingErrorCode.InvalidNumber, check, "GLS number must be positive Int64 before request");
                check(handler.Calls == 0, "GLS invalid inputs consume no local HTTP allowance");
                check((await service.FetchAsync(glsSettings, Number)).State == ParcelState.InTransit && handler.Calls == 1 && !client.DefaultRequestHeaders.Any(), "GLS single explicit call tracks without separate authentication and retains no headers");
            }
            string quota = "{\"GetParcelStatusErrors\":[{\"ErrorCode\":31,\"ErrorDescription\":\"secret\"}]}";
            using (var handler = new Handler((_, _) => Task.FromResult(Reply(quota))))
            using (var client = new HttpClient(handler))
            {
                var service = new GlsTrackingService(client, Budget("GLS"), () => now, log.Add);
                await Error(() => service.FetchAsync(glsSettings, Number), TrackingErrorCode.Quota, check, "GLS structured rate error starts five-minute cooldown");
                await Error(() => service.FetchAsync(glsSettings, Number), TrackingErrorCode.Quota, check, "GLS manual retry during cooldown is rejected locally");
                check(handler.Calls == 1, "GLS cooldown sends no extra HTTP request");
            }
            foreach (var carrier in new[] { "FedEx", "GLS" })
            {
                using var handler = new Handler((_, _) => Task.FromResult(Reply(Secret, HttpStatusCode.TooManyRequests)));
                using var client = new HttpClient(handler);
                Func<Task> call = carrier == "FedEx" ? () => new FedExTrackingService(client, Budget(carrier), () => now, log.Add).FetchAsync(fedexSettings, Number) : () => new GlsTrackingService(client, Budget(carrier), () => now, log.Add).FetchAsync(glsSettings, Number);
                await Error(call, TrackingErrorCode.Quota, check, carrier + " HTTP 429 safe failure");
            }
            using (var client = new HttpClient(new Handler((_, _) => throw new HttpRequestException(Secret + User))))
                await Error(() => new GlsTrackingService(client, Budget("GLS"), () => now, log.Add).FetchAsync(glsSettings, Number), TrackingErrorCode.Offline, check, "GLS network exceptions never expose credential detail");
            using (var client = new HttpClient(new Handler((_, _) => Task.FromResult(Reply("not json")))))
                await Error(() => new GlsTrackingService(client, Budget("GLS"), () => now, log.Add).FetchAsync(glsSettings, Number), TrackingErrorCode.InvalidResponse, check, "GLS malformed HTTP 200 does not overwrite stored data");
            string logs = JsonSerializer.Serialize(log);
            check(!new[] { Id, Secret, Bearer, User, Password, Number, "https://", "Arrived at depot" }.Any(logs.Contains) && log.All(e => e.TrackingReference.StartsWith("…") && e.TrackingReference.Length <= 5), "Both new carriers redact credentials, full tracking number, URLs and response from logs");
            var store = new ApiLogStore(Path.Combine(root, "log.json")); check(log.All(store.Append), "Shared log persists every FedEx/GLS diagnostic entry");
            check(store.Load().Entries.Any(e => e.Carrier == "FedEx" && e.Stage == "OAuth") && store.Load().Entries.Any(e => e.Carrier == "GLS" && e.Stage == "Feldolgozás"), "Persisted four-carrier diagnostics identify OAuth and processing without migration");
        }
        finally { Directory.Delete(root, true); }
    }
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) { Calls++; return send(request, token); }
    }
}
