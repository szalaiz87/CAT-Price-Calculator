using System.Text.Json;
namespace CatPriceCalculator.Core;

// Persist actual request starts so restarting the program cannot reset the local allowance.
public sealed class TrackingRequestBudget(string path, Func<DateTimeOffset>? clock = null,
    Func<TimeSpan, CancellationToken, Task>? delay = null, string carrier = "DHL")
{
    private readonly Func<DateTimeOffset> now = clock ?? (() => DateTimeOffset.UtcNow);
    private readonly Func<TimeSpan, CancellationToken, Task> wait = delay ?? ((d, t) => Task.Delay(d, t));
    public async Task ReserveAsync(int limit, CancellationToken token)
    {
        List<DateTimeOffset> requests;
        try { requests = File.Exists(path) ? LocalJsonFile.Read<List<DateTimeOffset>>(path) ?? throw new JsonException() : []; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        { throw new ParcelTrackingException(TrackingErrorCode.Configuration, $"A {carrier} lekérdezési keret nem olvasható. A lekérés nem indult el."); }
        if (limit is < 1 or > 100000 || requests.Any(t => t > now().AddSeconds(5)))
            throw new ParcelTrackingException(TrackingErrorCode.Configuration, $"Ellenőrizd a rendszerórát és a {carrier} napi keretét. A lekérés nem indult el.");
        requests.RemoveAll(t => now() - t >= TimeSpan.FromHours(24));
        if (requests.Count >= limit) throw new ParcelTrackingException(TrackingErrorCode.Quota, $"Elérted a beállított 24 órás {carrier} lekérdezési keretet. Próbáld később, vagy add meg a {carrier} által jóváhagyott magasabb keretet.");
        var last = requests.Count == 0 ? DateTimeOffset.MinValue : requests.Max();
        var spacing = last + TimeSpan.FromSeconds(5) - now();
        if (spacing > TimeSpan.Zero) await wait(spacing, token);
        token.ThrowIfCancellationRequested();
        requests.Add(now());
        if (!LocalJsonFile.Write(path, requests))
            throw new ParcelTrackingException(TrackingErrorCode.Configuration, $"A {carrier} lekérdezési keret nem menthető. A lekérés nem indult el.");
    }
}
