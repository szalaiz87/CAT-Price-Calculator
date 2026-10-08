using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CatPriceCalculator.Core;

public static class DhlChecks
{
    private const string Key = "unit-test-only-consumer-key";
    private const string Transit = """
        {"shipments":[{"id":"ABC","service":"express","status":{"statusCode":"transit","status":"In transit","statusDetailed":"Processed","description":"Sorting complete","timestamp":"2026-10-08T12:00:00+02:00","location":{"address":{"addressLocality":"Leipzig","countryCode":"DE"}}},"estimatedTimeOfDelivery":"2026-10-09","events":[]}]}
        """;
    private static ParcelTrackingResult Parse(string json, string number = "ABC")
    { using var doc = JsonDocument.Parse(json); return DhlTrackingService.Parse(doc.RootElement, number); }
    private static async Task Error(Func<Task> action, TrackingErrorCode expected, Action<bool, string> check, string name)
    {
        try { await action(); throw new Exception("DHL error not reported: " + name); }
        catch (ParcelTrackingException e) { check(e.Code == expected && !e.Message.Contains(Key), name); }
    }
    public static async Task Run(Action<bool, string> check)
    {
        var directory = Path.Combine(Path.GetTempPath(), "linser-dhl-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        var now = new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);
        int sequence = 0;
        TrackingRequestBudget Budget() => new(Path.Combine(directory, "budget-" + sequence++ + ".json"), () => now,
            (spacing, token) => { token.ThrowIfCancellationRequested(); now += spacing; return Task.CompletedTask; });
        var settings = new DhlConnectionSettings(Key);
        try
        {
            check(settings.IsValid && settings.HasKey && settings.DailyLimit == 250, "DHL own key and conservative default limit");
            check(!settings.ToString().Contains(Key), "DHL settings diagnostic string excludes secret");
            foreach (var invalid in new[] { settings with { ApiKey = "demo-key" }, settings with { ApiKey = "line\nbreak" }, settings with { Service = "unknown" }, settings with { DailyLimit = 0 }, settings with { DailyLimit = 100001 }, settings with { RecipientPostalCode = "<bad>" } })
                check(!invalid.IsValid, "DHL invalid key/options rejected");
            using (var protector = new TestProtector())
            {
                string path = Path.Combine(directory, "key.bin");
                var store = new DhlSettingsStore(path, protector);
                check(!store.Load().Failed && !store.Load().Settings.HasKey, "DHL missing key is unconfigured");
                check(store.Save(settings) && store.Load().Settings == settings, "DHL protected settings round trip");
                check(!Encoding.UTF8.GetString(File.ReadAllBytes(path)).Contains(Key), "DHL key is absent from persisted plaintext");
                byte[] before = File.ReadAllBytes(path);
                protector.Fail = true;
                check(!store.Save(settings with { ApiKey = "replacement" }) && before.SequenceEqual(File.ReadAllBytes(path)), "DHL encryption failure preserves previous key file");
                protector.Fail = false;
                var tampered = before.ToArray(); tampered[^1] ^= 1; File.WriteAllBytes(path, tampered);
                check(store.Load().Failed && !store.Load().Settings.HasKey, "DHL corrupt encrypted key cannot silently load");
                check(store.Save(settings with { ApiKey = "" }) && !store.Load().Settings.HasKey, "DHL deleting key persists");
                string blocker = Path.Combine(directory, "blocker"); File.WriteAllText(blocker, "x");
                check(!new DhlSettingsStore(Path.Combine(blocker, "key.bin"), protector).Save(settings), "DHL unwritable protected settings reported");
            }

            check(TrackingTimestamp.Date("2026-10-08T23:30:00Z") == "2026.10.09." && TrackingTimestamp.Time("2026-10-08T23:30:00Z") == "01:30", "DHL offset timestamp converted to Hungarian day and time");
            check(TrackingTimestamp.Time("2026-01-08T12:00:00Z") == "13:00", "DHL timestamp winter offset");
            check(TrackingTimestamp.Instant("2026-10-08T12:30:00") == null && TrackingTimestamp.Time("2026-10-08T12:30:00") == "12:30*", "DHL local timestamp retains original unknown zone");
            check(TrackingTimestamp.Instant("2026-10-08T12:30:00+2:00").HasValue && TrackingTimestamp.Time("2026-10-08T12:30:00.123") == "12:30*", "DHL schema short offset and fractional local timestamp supported");
            check(TrackingTimestamp.Instant("2026-10-09") == null && TrackingTimestamp.Date("2026-10-09") == "2026.10.09." && TrackingTimestamp.Time("2026-10-09") == "", "DHL date-only ETA never invents midnight");
            check(TrackingTimestamp.Date("invalid") == "Még nincs adat" && TrackingTimestamp.Time(null) == "", "DHL invalid/missing time has no invented value");
            var result = Parse(Transit);
            check(result.State == ParcelState.InTransit && result.Location == "Leipzig, DE" && result.EventTimestamp == "2026-10-08T12:00:00+02:00", "DHL official status and location schema");
            check(result.DeliveryTimestamp == "2026-10-09" && result.StatusDetail.Contains("Processed"), "DHL official ETA and detailed status retained");
            foreach (var pair in new[] { ("delivered", ParcelState.Delivered), ("failure", ParcelState.Exception), ("pre-transit", ParcelState.PreTransit), ("unknown", ParcelState.Unknown), ("future-code", ParcelState.Unknown) })
                check(Parse(Transit.Replace("\"transit\"", "\"" + pair.Item1 + "\"")).State == pair.Item2, "DHL high-level status " + pair.Item1);
            const string fallback = """
                {"shipments":[{"id":"ABC","status":{"statusCode":"transit","timestamp":"2026-10-08T18:00:00Z"},"destination":{"address":{"addressLocality":"Budapest"}},"events":[{"timestamp":"2026-10-08T10:00:00Z","location":{"address":{"addressLocality":"Vienna"}}},{"timestamp":"2026-10-08T09:00:00Z","location":{"address":{"addressLocality":"Leipzig"}}}],"estimatedDeliveryTimeFrame":{"estimatedThrough":"2026-10-10"}}]}
                """;
            var located = Parse(fallback);
            check(located.Location == "Vienna" && located.EventTimestamp == "2026-10-08T10:00:00Z", "DHL latest located scan paired with its own timestamp");
            check(located.DeliveryTimestamp == "2026-10-10", "DHL estimated window fallback");
            var absent = Parse("""{"shipments":[{"status":{"statusCode":"transit"},"destination":{"address":{"addressLocality":"Budapest"}}}]}""");
            check(absent.Location == null && absent.EventTimestamp == null && absent.DeliveryTimestamp == null, "DHL destination not substituted for scan and missing values stay missing");
            await Error(() => Task.FromResult(Parse("{}")), TrackingErrorCode.InvalidResponse, check, "DHL missing shipment list rejected");
            await Error(() => Task.FromResult(Parse("{\"shipments\":[]}")), TrackingErrorCode.NotFound, check, "DHL empty list is not found");
            await Error(() => Task.FromResult(Parse("{\"shipments\":[{}]}")), TrackingErrorCode.InvalidResponse, check, "DHL missing current status rejected");
            const string multiple = """{"shipments":[{"id":"A","status":{"statusCode":"transit"}},{"id":"B","status":{"statusCode":"delivered"}}]}""";
            check(Parse(multiple, "B").State == ParcelState.Delivered, "DHL multiple results choose unique matching ID");
            await Error(() => Task.FromResult(Parse(multiple)), TrackingErrorCode.Ambiguous, check, "DHL ambiguous results require service selection");
            check(Parse(Transit, "piece-alias").State == ParcelState.InTransit, "DHL single parcel allows piece/reference-number lookup");

            var parcel = ParcelBook.Add("ABC", Courier.Dhl, "alkatrész", now);
            var applied = DhlTrackingService.Apply(parcel, result, now);
            string parcelPath = Path.Combine(directory, "legacy-parcels.json");
            File.WriteAllText(parcelPath, JsonSerializer.Serialize(new[] { new { parcel.Id, parcel.TrackingNumber, parcel.Carrier, parcel.Note, parcel.State, parcel.Location, parcel.LastEventAt, parcel.EstimatedDeliveryAt, parcel.DeliveredAt, parcel.AddedAt, parcel.IsSample } }));
            var parcelStore = new ParcelStore(parcelPath);
            check(!parcelStore.Load().Failed && parcelStore.Load().Parcels.Single() == parcel, "DHL upgrade reads previous beta parcel format unchanged");
            check(parcelStore.Save([applied]) && parcelStore.Load().Parcels.Single() == applied, "DHL tracking fields persist with existing parcel store");
            check(applied.Location == result.Location && applied.LastCheckedAt == now && applied.EstimatedDeliveryAt == null && applied.EstimatedDeliveryRawTimestamp == "2026-10-09", "DHL response applies real data and date-only ETA without guessed instant");
            var missing = DhlTrackingService.Apply(applied, absent, now.AddMinutes(1));
            check(missing.Location == applied.Location && missing.LastEventRawTimestamp == applied.LastEventRawTimestamp, "DHL missing scan preserves previous location and paired timestamp");
            var delivered = Parse("""
                {"shipments":[{"status":{"statusCode":"delivered","timestamp":"2026-10-08T12:00:00Z"},"events":[{"statusCode":"delivered","timestamp":"2026-10-08T11:00:00Z"},{"statusCode":"delivered","timestamp":"2026-10-08T10:00:00Z"}]}]}
                """);
            var arrival = DhlTrackingService.Apply(applied, delivered, now);
            check(arrival.DeliveredAt == now.AddHours(-2) && !arrival.RetentionFromObservation, "DHL first confirmed delivery scan starts retention");
            check(DhlTrackingService.Apply(arrival, delivered, now.AddHours(1)).DeliveredAt == arrival.DeliveredAt, "DHL repeated delivery refresh cannot extend retention");
            check(DhlTrackingService.Apply(arrival, result, now.AddHours(1)).State == ParcelState.Delivered, "DHL stale transit response cannot regress delivered parcel");
            var unknownDelivery = delivered with { DeliveryTimestamp = "2026-10-08T12:00:00" };
            var observed = DhlTrackingService.Apply(parcel, unknownDelivery, now);
            check(observed.DeliveredAt == now && observed.RetentionFromObservation && DhlTrackingService.Apply(observed, unknownDelivery, now.AddHours(1)).DeliveredAt == now, "DHL unknown delivery timezone uses marked first-confirmation retention");
            foreach (var refused in new[] { parcel with { IsSample = true }, parcel with { Carrier = Courier.Ups } })
            { bool rejected = false; try { DhlTrackingService.Apply(refused, result, now); } catch (ArgumentException) { rejected = true; } check(rejected, "DHL never updates samples or other carriers"); }

            using (var handler = new Handler((request, token) =>
            {
                check(request.RequestUri!.GetLeftPart(UriPartial.Path) == DhlTrackingService.Endpoint && request.RequestUri.Query.Contains("A%2FB%20%26") && !request.RequestUri.AbsoluteUri.Contains(Key), "DHL fixed HTTPS endpoint and escaped number; key excluded from URL");
                check(request.Headers.GetValues("DHL-API-Key").Single() == Key && request.RequestUri.Query.Contains("recipientPostalCode=1234") && request.RequestUri.Query.Contains("service=express"), "DHL Consumer Key header and optional filters");
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Transit) });
            }))
            using (var http = new HttpClient(handler))
            {
                var service = new DhlTrackingService(http, Budget(), () => now);
                await Error(() => service.FetchAsync(new(), "ABC"), TrackingErrorCode.Configuration, check, "DHL missing key sends no request");
                await Error(() => service.FetchAsync(settings with { ApiKey = "demo-key" }, "ABC"), TrackingErrorCode.Configuration, check, "DHL mock demo key refused");
                check(handler.Calls == 0, "DHL invalid configuration consumes no HTTP call");
                check((await service.FetchAsync(settings with { RecipientPostalCode = "1234", Service = "express" }, "A/B &")).State == ParcelState.InTransit && !http.DefaultRequestHeaders.Contains("DHL-API-Key"), "DHL per-request secret and successful response");
            }
            foreach (var pair in new[] { (HttpStatusCode.Unauthorized, TrackingErrorCode.Authentication), (HttpStatusCode.Forbidden, TrackingErrorCode.Authentication), (HttpStatusCode.NotFound, TrackingErrorCode.NotFound), (HttpStatusCode.ServiceUnavailable, TrackingErrorCode.Offline), (HttpStatusCode.Redirect, TrackingErrorCode.Offline) })
            {
                using var http = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(pair.Item1) { Content = new StringContent(Key) })));
                var service = new DhlTrackingService(http, Budget(), () => now);
                await Error(() => service.FetchAsync(settings, "ABC"), pair.Item2, check, "DHL HTTP " + (int)pair.Item1 + " sanitized error");
            }
            using (var handler = new Handler((_, _) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                response.Headers.RetryAfter = new(TimeSpan.FromMinutes(1)); return Task.FromResult(response);
            }))
            using (var http = new HttpClient(handler))
            {
                var service = new DhlTrackingService(http, Budget(), () => now);
                await Error(() => service.FetchAsync(settings, "ABC"), TrackingErrorCode.Quota, check, "DHL 429 reports quota");
                await Error(() => service.FetchAsync(settings, "ABC"), TrackingErrorCode.Quota, check, "DHL Retry-After prevents immediate retry");
                check(handler.Calls == 1, "DHL cooldown makes no repeated HTTP request");
            }
            using (var http = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("bad-json " + Key) }))))
                await Error(() => new DhlTrackingService(http, Budget()).FetchAsync(settings, "ABC"), TrackingErrorCode.InvalidResponse, check, "DHL malformed JSON sanitized");
            using (var http = new HttpClient(new Handler((_, _) => throw new HttpRequestException(Key))))
                await Error(() => new DhlTrackingService(http, Budget()).FetchAsync(settings, "ABC"), TrackingErrorCode.Offline, check, "DHL network error sanitized");
            using (var http = new HttpClient(new Handler(async (_, token) => { await Task.Delay(10000, token); return new(HttpStatusCode.OK); })) { Timeout = TimeSpan.FromMilliseconds(30) })
                await Error(() => new DhlTrackingService(http, Budget()).FetchAsync(settings, "ABC"), TrackingErrorCode.Offline, check, "DHL HTTP timeout handled");
            using (var cancel = new CancellationTokenSource())
            using (var http = new HttpClient(new Handler((_, _) => throw new Exception("Cancelled request reached HTTP"))))
            {
                cancel.Cancel(); bool cancelled = false;
                try { await new DhlTrackingService(http, Budget()).FetchAsync(settings, "ABC", cancel.Token); } catch (OperationCanceledException) { cancelled = true; }
                check(cancelled, "DHL user cancellation stays cancellation");
            }
            using (var cancel = new CancellationTokenSource())
            using (var http = new HttpClient(new Handler(async (_, token) => { cancel.Cancel(); await Task.Delay(10000, token); return new(HttpStatusCode.OK); })))
            {
                bool cancelled = false;
                try { await new DhlTrackingService(http, Budget()).FetchAsync(settings, "ABC", cancel.Token); } catch (OperationCanceledException) { cancelled = true; }
                check(cancelled, "DHL in-flight user cancellation stops HTTP without reporting timeout");
            }

            string budgetPath = Path.Combine(directory, "persistent-budget.json");
            var start = now;
            TrackingRequestBudget Persistent() => new(budgetPath, () => now, (spacing, token) => { token.ThrowIfCancellationRequested(); now += spacing; return Task.CompletedTask; });
            await Persistent().ReserveAsync(2, default); await Persistent().ReserveAsync(2, default);
            check(now - start == TimeSpan.FromSeconds(5), "DHL five-second spacing survives budget instances/restarts");
            await Error(() => Persistent().ReserveAsync(2, default), TrackingErrorCode.Quota, check, "DHL persisted daily allowance cannot reset on restart");
            now = start.AddHours(24); await Persistent().ReserveAsync(2, default);
            check(JsonSerializer.Deserialize<List<DateTimeOffset>>(File.ReadAllText(budgetPath))!.Count == 2, "DHL quota expires at exact elapsed 24-hour boundary");
            File.WriteAllText(budgetPath, "bad-json");
            await Error(() => Persistent().ReserveAsync(2, default), TrackingErrorCode.Configuration, check, "DHL corrupt request journal fails closed");
            File.WriteAllText(budgetPath, JsonSerializer.Serialize(new[] { now.AddMinutes(1) }));
            await Error(() => Persistent().ReserveAsync(2, default), TrackingErrorCode.Configuration, check, "DHL future journal detects clock rollback");
            await Error(() => new TrackingRequestBudget(Path.Combine(directory, "blocker", "budget.json"), () => now).ReserveAsync(2, default), TrackingErrorCode.Configuration, check, "DHL unwritable request budget prevents request");
            File.WriteAllText(budgetPath, JsonSerializer.Serialize(new[] { now }));
            byte[] unchanged = File.ReadAllBytes(budgetPath);
            var cancelBudget = new TrackingRequestBudget(budgetPath, () => now, (_, _) => throw new OperationCanceledException());
            bool interrupted = false; try { await cancelBudget.ReserveAsync(2, default); } catch (OperationCanceledException) { interrupted = true; }
            check(interrupted && unchanged.SequenceEqual(File.ReadAllBytes(budgetPath)), "DHL cancelled spacing wait consumes no reservation");
            int inFlight = 0, maxInFlight = 0;
            using (var handler = new Handler(async (_, _) => { maxInFlight = Math.Max(maxInFlight, ++inFlight); await Task.Yield(); inFlight--; return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Transit) }; }))
            using (var http = new HttpClient(handler))
            {
                var service = new DhlTrackingService(http, Budget(), () => now); var batchStart = now;
                await Task.WhenAll(service.FetchAsync(settings, "ABC"), service.FetchAsync(settings, "ABC"), service.FetchAsync(settings, "ABC"));
                check(handler.Calls == 3 && maxInFlight == 1 && now - batchStart == TimeSpan.FromSeconds(10), "DHL shared service serializes concurrent test and tracking requests");
            }
        }
        finally { Directory.Delete(directory, true); }
    }
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { Calls++; return send(request, cancellationToken); }
    }
    internal sealed class TestProtector : ISecretProtector, IDisposable
    {
        private readonly byte[] key = RandomNumberGenerator.GetBytes(32);
        public bool Fail { get; set; }
        public byte[] Protect(byte[] plain)
        {
            if (Fail) throw new CryptographicException("simulated encryption failure");
            byte[] nonce = RandomNumberGenerator.GetBytes(12), cipher = new byte[plain.Length], tag = new byte[16];
            using var aes = new AesGcm(key, 16); aes.Encrypt(nonce, plain, cipher, tag);
            return nonce.Concat(tag).Concat(cipher).ToArray();
        }
        public byte[] Unprotect(byte[] encrypted)
        {
            if (encrypted.Length < 28) throw new CryptographicException();
            byte[] plain = new byte[encrypted.Length - 28];
            using var aes = new AesGcm(key, 16); aes.Decrypt(encrypted.AsSpan(0, 12), encrypted.AsSpan(28), encrypted.AsSpan(12, 16), plain);
            return plain;
        }
        public void Dispose() => CryptographicOperations.ZeroMemory(key);
    }
}
