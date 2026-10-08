using System.Net;
using System.Text;
using System.Text.Json;
using CatPriceCalculator.Core;

public static class UpsChecks
{
    private const string Id = "unit-test-only-client-id", Secret = "unit-test-only-secret", Bearer = "unit-test-only-bearer", Number = "1Z1234567890123456";
    private const string TokenBody = "{\"access_token\":\"unit-test-only-bearer\",\"token_type\":\"bearer\",\"expires_in\":\"3600\"}";
    private const string Transit = """
        {"trackResponse":{"shipment":[{"package":[{"trackingNumber":"1Z1234567890123456","currentStatus":{"code":"IT","type":"I","description":"On the way"},"deliveryDate":[{"type":"SDD","date":"20261009"},{"type":"RDD","date":"20261010"}],"activity":[{"date":"20261008","time":"120000","gmtDate":"20261008","gmtTime":"100000","gmtOffset":"+02:00","location":{"address":{"city":"Leipzig","country":"DE"}},"status":{"type":"I","description":"Processed"}}]}]}]}}
        """;
    private static ParcelTrackingResult Parse(string json, string number = Number)
    { using var doc = JsonDocument.Parse(json); return UpsTrackingService.Parse(doc.RootElement, number); }
    private static HttpResponseMessage Reply(string body, HttpStatusCode code = HttpStatusCode.OK) => new(code) { Content = new StringContent(body) };
    private static async Task Error(Func<Task> action, TrackingErrorCode code, Action<bool, string> check, string label)
    {
        try { await action(); throw new Exception("UPS error not raised: " + label); }
        catch (ParcelTrackingException e) { check(e.Code == code && !new[] { Id, Secret, Bearer }.Any(e.Message.Contains), label); }
    }
    public static async Task Run(Action<bool, string> check)
    {
        string directory = Path.Combine(Path.GetTempPath(), "linser-ups-" + Guid.NewGuid()); Directory.CreateDirectory(directory);
        var now = new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero); int seq = 0;
        TrackingRequestBudget Budget() => new(Path.Combine(directory, "budget-" + seq++ + ".json"), () => now,
            (d, ct) => { ct.ThrowIfCancellationRequested(); now += d; return Task.CompletedTask; }, "UPS");
        var settings = new UpsConnectionSettings(Id, Secret, "ABC123");
        try
        {
            check(settings.IsValid && settings.HasCredentials && !new UpsConnectionSettings().HasCredentials, "UPS needs both Client ID and Client Secret");
            check(!new[] { Id, Secret, "ABC123" }.Any(settings.ToString().Contains), "UPS diagnostic settings hide credentials and account");
            foreach (var invalid in new[] { settings with { ClientId = "bad:id" }, settings with { ClientSecret = "bad\nsecret" }, settings with { AccountNumber = "12345" }, settings with { AccountNumber = "ABC-12" }, settings with { DailyLimit = 0 }, settings with { ClientId = null! }, settings with { ClientSecret = new string('a', 513) } })
                check(!invalid.IsValid, "UPS invalid credentials/options rejected");
            using (var protector = new DhlChecks.TestProtector())
            {
                string path = Path.Combine(directory, "ups.bin"); var store = new UpsSettingsStore(path, protector);
                check(!store.Load().Failed && !store.Load().Settings.HasCredentials, "UPS missing encrypted settings are unconfigured");
                check(store.Save(settings) && store.Load().Settings == settings, "UPS protected credentials and options survive restart");
                check(!new[] { Id, Secret, "ABC123" }.Any(Encoding.UTF8.GetString(File.ReadAllBytes(path)).Contains), "UPS persisted file contains no plaintext credentials");
                byte[] before = File.ReadAllBytes(path); protector.Fail = true;
                check(!store.Save(settings with { ClientSecret = "replacement" }) && before.SequenceEqual(File.ReadAllBytes(path)), "UPS encryption failure preserves previous credentials without plaintext fallback");
                protector.Fail = false; before[^1] ^= 1; File.WriteAllBytes(path, before);
                check(store.Load().Failed && !store.Load().Settings.HasCredentials, "UPS corrupted ciphertext fails closed");
                check(store.Save(settings with { ClientId = "", ClientSecret = "" }) && !store.Load().Settings.HasCredentials, "UPS explicit credential removal survives restart");
            }
            var transit = Parse(Transit);
            check(transit.State == ParcelState.InTransit && transit.Location == "Leipzig, DE" && transit.EventTimestamp == "2026-10-08T10:00:00Z", "UPS official tracking/activity schema pairs scan location and UTC time");
            check(transit.DeliveryTimestamp == "2026-10-10" && TrackingTimestamp.Instant(transit.DeliveryTimestamp) == null, "UPS rescheduled ETA takes priority without inventing midnight");
            check(Parse(Transit.Replace("\"code\":\"IT\"", "\"code\":\"OT\"")).State == ParcelState.OutForDelivery, "UPS out-for-delivery OT scan is distinguished within transit type");
            foreach (var status in new[] { ("M", ParcelState.PreTransit), ("D", ParcelState.Delivered), ("O", ParcelState.OutForDelivery), ("P", ParcelState.InTransit), ("X", ParcelState.Exception), ("NEW", ParcelState.Unknown) })
                check(Parse(Transit.Replace("\"type\":\"I\"", "\"type\":\"" + status.Item1 + "\"")).State == status.Item2, "UPS activity status " + status.Item1);
            check(UpsTrackingService.Timestamp("20261008", "74700", "-05:00") == "2026-10-08T07:47:00-05:00", "UPS documented short GMT/time format and offset supported");
            check(UpsTrackingService.Timestamp("20261008", "120000") == "2026-10-08T12:00:00" && TrackingTimestamp.Time(UpsTrackingService.Timestamp("20261008", "120000")) == "12:00*", "UPS unknown zone preserves original marked local time");
            check(UpsTrackingService.Timestamp("invalid", "120000") == null && UpsTrackingService.Timestamp("20261008", "bad") == "2026-10-08", "UPS missing/invalid time never invents a precise instant");
            string missing = """{"trackResponse":{"shipment":[{"package":[{"currentStatus":{"type":"I"},"packageAddress":[{"address":{"city":"Budapest"}}],"activity":[]}]}]}}""";
            var absent = Parse(missing);
            check(absent.Location == null && absent.EventTimestamp == null && absent.DeliveryTimestamp == null, "UPS destination not substituted for last known scan");
            string olderLocated = Transit.Replace("\"activity\":[", "\"activity\":[{\"date\":\"20261009\",\"time\":\"120000\",\"status\":{\"type\":\"I\"}},");
            check(Parse(olderLocated).Location == "Leipzig, DE" && Parse(olderLocated).EventTimestamp == transit.EventTimestamp, "UPS latest scan without location does not misdate older known location");
            string fallback = Transit.Replace("\"currentStatus\":{\"code\":\"IT\",\"type\":\"I\",\"description\":\"On the way\"},", "");
            check(Parse(fallback).State == ParcelState.InTransit, "UPS latest activity supplies absent current status");
            await Error(() => Task.FromResult(Parse("{}")), TrackingErrorCode.InvalidResponse, check, "UPS missing response shape rejected");
            await Error(() => Task.FromResult(Parse("{\"trackResponse\":{\"shipment\":[]}}")), TrackingErrorCode.NotFound, check, "UPS no packages is not found");
            await Error(() => Task.FromResult(Parse("{\"trackResponse\":{\"shipment\":[{\"package\":[{}]}]}}")), TrackingErrorCode.InvalidResponse, check, "UPS absent status rejected instead of overwriting stored data");
            string multiple = Transit.Replace("\"package\":[", "\"package\":[{\"trackingNumber\":\"OTHER\",\"currentStatus\":{\"type\":\"M\"}},");
            check(Parse(multiple).State == ParcelState.InTransit, "UPS multiple packages choose only unique exact tracking number");
            await Error(() => Task.FromResult(Parse(multiple, "1234567")), TrackingErrorCode.Ambiguous, check, "UPS multiple reference matches rejected as ambiguous");
            string deliveredJson = Transit.Replace("\"type\":\"I\"", "\"type\":\"D\"");
            var parcel = ParcelBook.Add(Number, Courier.Ups, "", now);
            var delivered = UpsTrackingService.Apply(parcel, Parse(deliveredJson), now);
            check(delivered.State == ParcelState.Delivered && delivered.DeliveredAt == new DateTimeOffset(2026, 10, 8, 10, 0, 0, TimeSpan.Zero) && !delivered.RetentionFromObservation, "UPS confirmed delivery scan starts exact 48-hour retention");
            check(UpsTrackingService.Apply(delivered, Parse(deliveredJson), now.AddHours(1)).DeliveredAt == delivered.DeliveredAt && UpsTrackingService.Apply(delivered, transit, now).State == ParcelState.Delivered, "UPS delivery retention is not extended or regressed on repeat/stale updates");
            var deliveredDate = Parse(missing.Replace("\"type\":\"I\"", "\"type\":\"D\"").Replace("\"activity\":[]", "\"activity\":[],\"deliveryDate\":[{\"type\":\"DEL\",\"date\":\"20261008\"}],\"deliveryTime\":{\"type\":\"DEL\",\"endTime\":\"120000\"}"));
            check(deliveredDate.DeliveryTimestamp == "2026-10-08T12:00:00" && UpsTrackingService.Apply(parcel, deliveredDate, now).RetentionFromObservation, "UPS DEL date/time preserved without guessing a timezone or retention instant");
            var noDeliveryTime = UpsTrackingService.Apply(parcel, absent with { State = ParcelState.Delivered }, now);
            check(noDeliveryTime.DeliveredAt == now && noDeliveryTime.RetentionFromObservation, "UPS missing delivery instant uses marked first-confirmation retention");
            foreach (var invalid in new[] { parcel with { Carrier = Courier.Dhl }, parcel with { IsSample = true } })
            {
                bool refused = false; try { UpsTrackingService.Apply(invalid, transit, now); } catch (ArgumentException) { refused = true; }
                check(refused, "UPS apply cannot update DHL or sample parcels");
            }
            var kept = UpsTrackingService.Apply(parcel with { Location = "Vienna", LastEventRawTimestamp = "2026-10-07", LastEventAt = now.AddDays(-1) }, absent, now);
            check(kept.Location == "Vienna" && kept.LastEventRawTimestamp == "2026-10-07", "UPS unavailable new location preserves prior location/time pair");
            var entries = new List<ApiLogEntry>();
            int oauth = 0, tracking = 0;
            using (var handler = new Handler(async (request, ct) =>
            {
                if (request.Method == HttpMethod.Post)
                {
                    oauth++;
                    check(request.RequestUri?.AbsoluteUri == UpsTrackingService.TokenEndpoint && request.Headers.Authorization?.Scheme == "Basic" && Encoding.ASCII.GetString(Convert.FromBase64String(request.Headers.Authorization.Parameter!)) == Id + ":" + Secret, "UPS OAuth uses fixed production endpoint and HTTP Basic credentials");
                    check(request.Content?.Headers.ContentType?.MediaType == "application/x-www-form-urlencoded" && await request.Content.ReadAsStringAsync(ct) == "grant_type=client_credentials" && request.Headers.GetValues("x-merchant-id").Single() == "ABC123", "UPS OAuth required form and optional merchant header");
                    return Reply(TokenBody);
                }
                tracking++;
                check(request.RequestUri?.AbsolutePath == "/api/track/v1/details/" + Number && request.RequestUri.Query.Contains("returnSignature=false") && !new[] { Id, Secret, Bearer }.Any(request.RequestUri.AbsoluteUri.Contains), "UPS tracking uses production GET without secrets/signature in URL");
                check(request.Headers.Authorization?.Scheme == "Bearer" && request.Headers.Authorization.Parameter == Bearer && request.Headers.Contains("transId") && request.Headers.GetValues("transactionSrc").Single() == "LinserHungary", "UPS tracking uses per-request Bearer and required transaction headers");
                return Reply(Transit);
            }))
            using (var client = new HttpClient(handler))
            {
                var service = new UpsTrackingService(client, Budget(), () => now, entries.Add);
                check(handler.Calls == 0, "UPS service creation/configuration never requests OAuth or tracking");
                await Error(() => service.FetchAsync(new(), Number), TrackingErrorCode.Configuration, check, "UPS absent credentials make no network call");
                await Error(() => service.FetchAsync(settings, "bad"), TrackingErrorCode.InvalidNumber, check, "UPS invalid number is a row error before OAuth/network");
                check(handler.Calls == 0, "UPS invalid inputs consume no HTTP allowance");
                check((await service.FetchAsync(settings, Number)).State == ParcelState.InTransit, "UPS explicit manual fetch authenticates and parses response");
                await service.FetchAsync(settings, Number);
                check(oauth == 1 && tracking == 2, "UPS reuses unexpired memory token only during subsequent manual requests");
                now += TimeSpan.FromHours(1); await service.FetchAsync(settings, Number);
                check(oauth == 2 && tracking == 3, "UPS expired token refreshed within next explicit manual request");
                service.ForgetToken(); await service.FetchAsync(settings, Number);
                check(oauth == 3, "UPS deleting/changing credentials invalidates memory token");
                check(client.DefaultRequestHeaders.Authorization == null, "UPS authorization never becomes a shared default header");
            }
            string logs = JsonSerializer.Serialize(entries);
            check(entries.Any(e => e.Carrier == "UPS" && e.Stage == "OAuth" && e.HttpStatus == 200) && entries.Any(e => e.Stage == "Feldolgozás"), "UPS shared diagnostics distinguish provider, OAuth, HTTP and parser stages");
            check(!new[] { Id, Secret, Bearer, "ABC123", Number, "https://", "On the way", Convert.ToBase64String(Encoding.ASCII.GetBytes(Id + ":" + Secret)) }.Any(logs.Contains) && entries.All(e => e.TrackingReference != Number), "UPS shared log excludes credentials, token, headers, URL, full number and raw response");
            // Authentication failures are sanitized even when the upstream body echoes secret material.
            foreach (var code in new[] { HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.ServiceUnavailable, HttpStatusCode.Found })
            {
                var h = new Handler((_, _) => Task.FromResult(Reply(Secret + Bearer, code))); using var client = new HttpClient(h);
                var service = new UpsTrackingService(client, Budget(), () => now);
                await Error(() => service.FetchAsync(settings, Number), code is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden ? TrackingErrorCode.Authentication : TrackingErrorCode.Offline, check, "UPS OAuth HTTP " + (int)code + " safe failure");
                check(h.Calls == 1, "UPS failed OAuth never calls tracking or retries indefinitely");
            }
            foreach (var malformed in new[] { "{}", TokenBody.Replace("3600", "0"), TokenBody.Replace("bearer", "basic"), TokenBody.Replace("3600", "invalid"), "not json" })
            {
                using var client = new HttpClient(new Handler((_, _) => Task.FromResult(Reply(malformed))));
                await Error(() => new UpsTrackingService(client, Budget(), () => now).FetchAsync(settings, Number), TrackingErrorCode.InvalidResponse, check, "UPS malformed OAuth token rejected without exposing body");
            }
            int renewal = 0;
            var renewHandler = new Handler((r, _) => Task.FromResult(r.Method == HttpMethod.Post ? Reply(TokenBody) : Reply(Transit, ++renewal == 1 ? HttpStatusCode.Unauthorized : HttpStatusCode.OK)));
            using (var client = new HttpClient(renewHandler))
            {
                var service = new UpsTrackingService(client, Budget(), () => now);
                check((await service.FetchAsync(settings, Number)).State == ParcelState.InTransit && renewHandler.Calls == 4, "UPS invalidated Bearer gets exactly one OAuth renewal/retry in same manual action");
            }
            var unauthorized = new Handler((r, _) => Task.FromResult(r.Method == HttpMethod.Post ? Reply(TokenBody) : Reply(Secret, HttpStatusCode.Unauthorized)));
            using (var client = new HttpClient(unauthorized))
            {
                await Error(() => new UpsTrackingService(client, Budget(), () => now).FetchAsync(settings, Number), TrackingErrorCode.Authentication, check, "UPS repeated unauthorized tracking stops safely");
                check(unauthorized.Calls == 4, "UPS repeated authorization failure cannot create a renewal loop");
            }
            foreach (var code in new[] { HttpStatusCode.Forbidden, HttpStatusCode.NotFound, HttpStatusCode.BadRequest, HttpStatusCode.ServiceUnavailable })
            {
                using var client = new HttpClient(new Handler((r, _) => Task.FromResult(r.Method == HttpMethod.Post ? Reply(TokenBody) : Reply(Secret, code))));
                await Error(() => new UpsTrackingService(client, Budget(), () => now).FetchAsync(settings, Number), code == HttpStatusCode.Forbidden ? TrackingErrorCode.Authentication : code is HttpStatusCode.NotFound or HttpStatusCode.BadRequest ? TrackingErrorCode.NotFound : TrackingErrorCode.Offline, check, "UPS tracking HTTP " + (int)code + " safe failure");
            }
            var rateLimitHandler = new Handler((r, _) =>
            {
                if (r.Method == HttpMethod.Post) return Task.FromResult(Reply(TokenBody));
                var response = Reply(Secret, HttpStatusCode.TooManyRequests); response.Headers.RetryAfter = new(TimeSpan.FromMinutes(10)); return Task.FromResult(response);
            });
            using (var client = new HttpClient(rateLimitHandler))
            {
                var service = new UpsTrackingService(client, Budget(), () => now);
                await Error(() => service.FetchAsync(settings, Number), TrackingErrorCode.Quota, check, "UPS HTTP 429 starts Retry-After cooldown");
                await Error(() => service.FetchAsync(settings, Number), TrackingErrorCode.Quota, check, "UPS immediate manual retry respects cooldown");
                check(rateLimitHandler.Calls == 2, "UPS cooldown consumes no further HTTP request");
            }
            using (var client = new HttpClient(new Handler((_, _) => throw new HttpRequestException(Secret))))
                await Error(() => new UpsTrackingService(client, Budget(), () => now).FetchAsync(settings, Number), TrackingErrorCode.Offline, check, "UPS network error does not disclose credential-containing exception");
            using (var client = new HttpClient(new Handler((_, _) => throw new OperationCanceledException())))
                await Error(() => new UpsTrackingService(client, Budget(), () => now).FetchAsync(settings, Number), TrackingErrorCode.Offline, check, "UPS HTTP timeout is distinct from user cancellation");
            using (var cancelled = new CancellationTokenSource())
            using (var client = new HttpClient(new Handler((_, ct) => { ct.ThrowIfCancellationRequested(); return Task.FromResult(Reply(TokenBody)); })))
            {
                cancelled.Cancel(); bool wasCancelled = false;
                try { await new UpsTrackingService(client, Budget(), () => now).FetchAsync(settings, Number, cancelled.Token); } catch (OperationCanceledException) { wasCancelled = true; }
                check(wasCancelled, "UPS user cancellation stays cancellation before any HTTP call");
            }
            {
                var h = new Handler((r, _) => Task.FromResult(Reply(r.Method == HttpMethod.Post ? TokenBody.Replace("\"3600\"", "3600") : Transit)));
                using var valid = new HttpClient(h);
                check((await new UpsTrackingService(valid, Budget(), () => now).FetchAsync(settings, Number)).State == ParcelState.InTransit, "UPS accepts numeric and documented string token expiry");
            }
            using (var client = new HttpClient(new Handler((r, _) => Task.FromResult(Reply(r.Method == HttpMethod.Post ? TokenBody : Transit)))))
                await Error(() => new UpsTrackingService(client, Budget(), () => now).FetchAsync(settings with { DailyLimit = 1 }, Number), TrackingErrorCode.Quota, check, "UPS local allowance includes OAuth and tracking without claiming a provider quota");
            string logPath = Path.Combine(directory, "shared-log.json"); var logStore = new ApiLogStore(logPath);
            File.WriteAllText(logPath, "[{\"Timestamp\":\"2026-10-08T12:00:00Z\",\"Stage\":\"HTTP\",\"Message\":\"Old DHL entry\"}]");
            check(!logStore.Load().Failed && logStore.Load().Entries.Single().Carrier == "DHL", "Shared API log loads beta.4 entries as DHL without data migration");
            logStore.Append(new(now, "OAuth", "Safe UPS message", Carrier: "UPS"));
            check(logStore.Load().Entries.Select(e => e.Carrier).SequenceEqual(new[] { "DHL", "UPS" }), "Shared provider log persists both DHL and UPS labels");
        }
        finally { Directory.Delete(directory, true); }
    }
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { Calls++; return send(request, cancellationToken); }
    }
}
