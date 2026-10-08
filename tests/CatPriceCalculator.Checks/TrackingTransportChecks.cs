using System.Net;
using CatPriceCalculator.Core;

public static class TrackingTransportChecks
{
    public static async Task Run(Action<bool, string> check)
    {
        string root = Path.Combine(Path.GetTempPath(), "linser-shared-http-" + Guid.NewGuid()); Directory.CreateDirectory(root);
        var now = DateTimeOffset.UtcNow;
        try
        {
            using var handler = new SharedHandler(); using var client = new HttpClient(handler);
            TrackingRequestBudget Budget(string carrier) => new(Path.Combine(root, carrier + ".json"), () => now, (d, ct) => { ct.ThrowIfCancellationRequested(); now += d; return Task.CompletedTask; }, carrier);
            var dhl = new DhlTrackingService(client, Budget("DHL")); var ups = new UpsTrackingService(client, Budget("UPS"));
            await dhl.FetchAsync(new("test-only-dhl-key"), "1234567");
            await ups.FetchAsync(new("test-only-client", "test-only-secret"), "1234567");
            await dhl.FetchAsync(new("test-only-dhl-key"), "1234567");
            check(handler.Calls == 4 && handler.HeadersIsolated, "Shared courier HttpClient keeps DHL keys and UPS Basic/Bearer isolated per request");
            check(!client.DefaultRequestHeaders.Any(), "Shared tracking transport never retains credential/default headers");
        }
        finally { Directory.Delete(root, true); }
    }
    private sealed class SharedHandler : HttpMessageHandler
    {
        public int Calls; public bool HeadersIsolated = true;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Calls++;
            string body;
            if (request.RequestUri!.Host == "api-eu.dhl.com")
            {
                HeadersIsolated &= request.Headers.Authorization == null && request.Headers.GetValues("DHL-API-Key").Single() == "test-only-dhl-key";
                body = "{\"shipments\":[{\"id\":\"1234567\",\"status\":{\"statusCode\":\"transit\"}}]}";
            }
            else
            {
                HeadersIsolated &= !request.Headers.Contains("DHL-API-Key") && request.Headers.Authorization?.Scheme == (request.Method == HttpMethod.Post ? "Basic" : "Bearer");
                body = request.Method == HttpMethod.Post ? "{\"access_token\":\"test-only-token\",\"expires_in\":\"3600\",\"token_type\":\"bearer\"}" : "{\"trackResponse\":{\"shipment\":[{\"package\":[{\"trackingNumber\":\"1234567\",\"currentStatus\":{\"type\":\"I\"}}]}]}}";
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        }
    }
}
