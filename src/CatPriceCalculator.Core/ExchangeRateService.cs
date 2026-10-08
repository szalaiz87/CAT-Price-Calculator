using System.Globalization;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
namespace CatPriceCalculator.Core;
public sealed record RateQuote(decimal Rate, DateOnly Date, string Source, DateTimeOffset? RetrievedAt = null);
public sealed class ExchangeRateService(HttpClient client, Action<string, Exception>? onFailure = null)
{
    private readonly object ecbLock = new();
    private Task<string>? ecbInFlight;
    private CancellationToken ecbToken;
    // Share only overlapping requests with the same cancellation scope. No completed-data cache.
    private Task<string> DownloadEcbAsync(CancellationToken token)
    {
        lock (ecbLock)
        {
            if (ecbInFlight is { IsCompleted: false } && ecbToken == token) return ecbInFlight;
            ecbToken = token;
            return ecbInFlight = DownloadTextAsync(EcbEndpoint, token);
        }
    }
    private async Task<string> DownloadTextAsync(string url, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        var deadline = TimeSpan.FromSeconds(4);
        if (client.Timeout != Timeout.InfiniteTimeSpan && client.Timeout < deadline) deadline = client.Timeout;
        timeout.CancelAfter(deadline);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd(BuildInfo.UserAgent);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(timeout.Token);
    }
    public const string Endpoint = "https://api.frankfurter.dev/v1/latest?base=DKK&symbols=HUF";
    public const string EuroEndpoint = "https://api.frankfurter.dev/v1/latest?base=DKK&symbols=EUR";
    public const string EcbEndpoint = "https://www.ecb.europa.eu/stats/eurofxref/eurofxref-daily.xml";
    public Task<RateQuote> FetchAsync(CancellationToken cancellationToken = default) => FetchCurrencyAsync("HUF", Endpoint, cancellationToken);
    public Task<RateQuote> FetchEuroAsync(CancellationToken cancellationToken = default) => FetchCurrencyAsync("EUR", EuroEndpoint, cancellationToken);
    public const string EuroHufEndpoint = "https://api.frankfurter.dev/v1/latest?base=EUR&symbols=HUF";
    public Task<RateQuote> FetchEuroHufAsync(CancellationToken cancellationToken = default) => FetchCurrencyAsync("HUF", EuroHufEndpoint, cancellationToken, "EUR");
    private async Task<RateQuote> FetchCurrencyAsync(string currency, string endpoint, CancellationToken cancellationToken, string baseCurrency = "DKK")
    {
        List<Exception> failures = [];
        // Prefer the direct central-bank publication; try an independent host if it fails.
        foreach (var url in new[] { EcbEndpoint, endpoint })
        {
            try
            {
                var text = url == EcbEndpoint ? await DownloadEcbAsync(cancellationToken) : await DownloadTextAsync(url, cancellationToken);
                var quote = url == EcbEndpoint ? ParseEcb(text, currency, baseCurrency) : ParseFrankfurter(text, currency, baseCurrency);
                return quote with { RetrievedAt = DateTimeOffset.UtcNow };
            }
            catch (Exception e) when (e is HttpRequestException or OperationCanceledException or JsonException or XmlException or InvalidDataException or FormatException or InvalidOperationException or KeyNotFoundException or OverflowException)
            {
                cancellationToken.ThrowIfCancellationRequested();
                failures.Add(e);
                onFailure?.Invoke(url, e);
            }
        }
        throw new HttpRequestException("Neither exchange-rate source is available.", new AggregateException(failures));
    }
    private static RateQuote ParseFrankfurter(string text, string currency, string baseCurrency)
    {
        using var json = JsonDocument.Parse(text);
        var root = json.RootElement;
        if (root.GetProperty("base").GetString() != baseCurrency || root.GetProperty("amount").GetDecimal() != 1m) throw new InvalidDataException("Invalid currency base.");
        var rate = root.GetProperty("rates").GetProperty(currency).GetDecimal();
        if (rate <= 0) throw new InvalidDataException("Invalid exchange rate.");
        return new(rate, DateOnly.ParseExact(root.GetProperty("date").GetString()!, "yyyy-MM-dd"), "Frankfurter / ECB");
    }
    private static RateQuote ParseEcb(string text, string currency, string baseCurrency)
    {
        using var reader = XmlReader.Create(new StringReader(text), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        var document = XDocument.Load(reader);
        XNamespace ns = "http://www.ecb.int/vocabulary/2002-08-01/eurofxref";
        var day = document.Descendants(ns + "Cube").FirstOrDefault(e => e.Attribute("time") != null) ?? throw new InvalidDataException("Missing ECB date.");
        decimal GetRate(string code)
        {
            var raw = day.Elements(ns + "Cube").FirstOrDefault(e => (string?)e.Attribute("currency") == code)?.Attribute("rate")?.Value;
            if (!decimal.TryParse(raw, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value) || value <= 0) throw new InvalidDataException("Invalid ECB currency rate.");
            return value;
        }
        var dkkPerEuro = baseCurrency == "EUR" ? 1m : GetRate("DKK");
        var rate = (currency == "EUR" ? 1m : GetRate(currency)) / dkkPerEuro;
        return new(rate, DateOnly.ParseExact(day.Attribute("time")!.Value, "yyyy-MM-dd"), "Európai Központi Bank (EKB)");
    }
}
