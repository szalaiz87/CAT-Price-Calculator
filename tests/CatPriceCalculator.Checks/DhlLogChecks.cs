using System.Net;
using System.Text.Json;
using CatPriceCalculator.Core;

public static class DhlLogChecks
{
    private const string Key = "private-log-test-key-do-not-expose";
    private const string Number = "PRIVATE-TRACKING-1234567890";
    public static async Task Run(Action<bool, string> check)
    {
        string directory = Path.Combine(Path.GetTempPath(), "linser-dhl-log-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        var now = new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);
        int sequence = 0;
        TrackingRequestBudget Budget() => new(Path.Combine(directory, "budget-" + sequence++ + ".json"), () => now,
            (spacing, token) => { token.ThrowIfCancellationRequested(); now += spacing; return Task.CompletedTask; });
        try
        {
            string path = Path.Combine(directory, "log.json"); var store = new ApiLogStore(path);
            var entry = new ApiLogEntry(now, "HTTP", "A DHL nem engedélyezte a hozzáférést.", 403, "…7890", 123, true);
            check(!store.Load().Failed && store.Load().Entries.Count == 0, "Missing API log starts empty without a network request");
            check(store.Append(entry) && new ApiLogStore(path).Load().Entries.Single() == entry, "API diagnostic fields and timestamps survive restart");
            for (int i = 0; i < ApiLogStore.MaxEntries + 4; i++) store.Append(entry with { Message = i.ToString() });
            var bounded = store.Load().Entries;
            check(bounded.Count == ApiLogStore.MaxEntries && bounded[0].Message == "4" && bounded[^1].Message == "203", "API log keeps exactly the most recent 200 entries");
            check(store.Clear() && store.Load().Entries.Count == 0 && !store.LastWriteFailed, "Manual API log clear persists an empty log");
            File.WriteAllText(path, "corrupt-json");
            check(store.Load().Failed && !store.Append(entry) && store.LastWriteFailed && File.ReadAllText(path) == "corrupt-json", "Corrupt API log is reported and preserved until explicit clear");
            check(store.Clear() && !store.Load().Failed && !store.LastWriteFailed, "Explicit API log clear repairs only its own corrupt file");
            string blocked = Path.Combine(directory, "blocked"); File.WriteAllText(blocked, "not a folder");
            var unwritable = new ApiLogStore(Path.Combine(blocked, "log.json"));
            check(!unwritable.Append(entry) && unwritable.LastWriteFailed, "API log write failure reported without throwing");

            var settings = new DhlConnectionSettings(Key);
            const string response = """{"shipments":[{"id":"PRIVATE-TRACKING-1234567890","status":{"statusCode":"transit","status":"private-log-test-key-do-not-expose"}}]}""";
            var logs = new List<ApiLogEntry>();
            using (var http = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response) }))))
            {
                var result = await new DhlTrackingService(http, Budget(), () => now, logs.Add).FetchAsync(settings, Number);
                check(result.State == ParcelState.InTransit && logs.Select(e => e.Stage).SequenceEqual(new[] { "Beállítás", "HTTP", "HTTP", "Feldolgozás" }), "API log traces successful request through configuration, HTTP and parsing");
                check(logs.Any(e => e.HttpStatus == 200) && logs.All(e => !e.IsError && e.ElapsedMilliseconds >= 0 && e.TrackingReference == "…7890"), "API log captures status/duration and masks tracking number");
                string serialized = JsonSerializer.Serialize(logs);
                check(!serialized.Contains(Key) && !serialized.Contains(Number) && !serialized.Contains("DHL-API-Key") && !serialized.Contains("trackingNumber="), "API key, headers, full tracking number, URLs and secret response text never enter diagnostic log");
            }
            foreach (var status in new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.NotFound, HttpStatusCode.TooManyRequests, HttpStatusCode.ServiceUnavailable })
            {
                logs.Clear();
                using var http = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(Key) })));
                try { await new DhlTrackingService(http, Budget(), () => now, logs.Add).FetchAsync(settings, Number); } catch (ParcelTrackingException) { }
                check(logs.Any(e => e.HttpStatus == (int)status && e.IsError) && logs.Last().Stage == "HTTP" && logs.Last().IsError && !JsonSerializer.Serialize(logs).Contains(Key), "API log identifies sanitized HTTP failure " + (int)status);
            }
            logs.Clear();
            using (var http = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("invalid-json " + Key) }))))
            {
                try { await new DhlTrackingService(http, Budget(), () => now, logs.Add).FetchAsync(settings, Number); } catch (ParcelTrackingException) { }
                check(logs.Last().Stage == "Feldolgozás" && logs.Last().IsError && !JsonSerializer.Serialize(logs).Contains(Key), "API log locates JSON failure without dumping raw response");
            }
            logs.Clear();
            using (var http = new HttpClient(new Handler((_, _) => throw new HttpRequestException(Key))))
            {
                try { await new DhlTrackingService(http, Budget(), () => now, logs.Add).FetchAsync(settings, Number); } catch (ParcelTrackingException) { }
                check(logs.Last().Stage == "HTTP" && logs.Last().IsError && !JsonSerializer.Serialize(logs).Contains(Key), "API log sanitizes network exception detail");
            }
            logs.Clear();
            using (var http = new HttpClient(new Handler((_, _) => throw new Exception("Unexpected network"))))
            {
                try { await new DhlTrackingService(http, Budget(), () => now, logs.Add).FetchAsync(new(), Number); } catch (ParcelTrackingException) { }
                check(logs.Last().Stage == "Beállítás" && logs.Last().IsError, "API log identifies missing/invalid key before network");
            }
            logs.Clear();
            using (var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response) })))
            using (var http = new HttpClient(handler))
            {
                var service = new DhlTrackingService(http, Budget(), () => now, logs.Add);
                await service.FetchAsync(settings with { DailyLimit = 1 }, Number);
                try { await service.FetchAsync(settings with { DailyLimit = 1 }, Number); } catch (ParcelTrackingException) { }
                check(logs.Last().Stage == "Keret" && logs.Last().IsError && handler.Calls == 1, "API log distinguishes local quota from HTTP error");
            }
            logs.Clear();
            using (var cancel = new CancellationTokenSource())
            using (var http = new HttpClient(new Handler((_, _) => throw new Exception("Unexpected network"))))
            {
                cancel.Cancel();
                try { await new DhlTrackingService(http, Budget(), () => now, logs.Add).FetchAsync(settings, Number, cancel.Token); } catch (OperationCanceledException) { }
                check(logs.Last().Stage == "Várakozás" && !logs.Last().IsError && logs.Last().Message.Contains("megszakítva"), "API log reports user cancellation without false network failure");
            }
            using (var http = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response) }))))
            {
                var service = new DhlTrackingService(http, Budget(), () => now, _ => throw new IOException("Diagnostic storage unavailable"));
                check((await service.FetchAsync(settings, Number)).State == ParcelState.InTransit, "API diagnostic write failure cannot interrupt tracking");
            }
        }
        finally { Directory.Delete(directory, true); }
    }
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) { Calls++; return send(request, cancellationToken); }
    }
}
