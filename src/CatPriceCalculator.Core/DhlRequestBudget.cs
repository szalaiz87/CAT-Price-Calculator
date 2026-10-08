using System.Text.Json;
namespace CatPriceCalculator.Core;

// Persist actual request starts so restarting the program cannot reset the local allowance.
public sealed class DhlRequestBudget(string path, Func<DateTimeOffset>? clock = null,
    Func<TimeSpan, CancellationToken, Task>? delay = null)
{
    private readonly Func<DateTimeOffset> now = clock ?? (() => DateTimeOffset.UtcNow);
    private readonly Func<TimeSpan, CancellationToken, Task> wait = delay ?? ((d, t) => Task.Delay(d, t));
    public async Task ReserveAsync(int limit, CancellationToken token)
    {
        List<DateTimeOffset> requests;
        try { requests = File.Exists(path) ? JsonSerializer.Deserialize<List<DateTimeOffset>>(File.ReadAllText(path)) ?? throw new JsonException() : []; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        { throw new DhlTrackingException(DhlError.Configuration, "A DHL lekérdezési keret nem olvasható. A lekérés nem indult el."); }
        if (limit is < 1 or > 100000 || requests.Any(t => t > now().AddSeconds(5)))
            throw new DhlTrackingException(DhlError.Configuration, "Ellenőrizd a rendszerórát és a DHL napi keretét. A lekérés nem indult el.");
        requests.RemoveAll(t => now() - t >= TimeSpan.FromHours(24));
        if (requests.Count >= limit) throw new DhlTrackingException(DhlError.Quota, "Elérted a beállított 24 órás DHL lekérdezési keretet. Próbáld később, vagy add meg a DHL által jóváhagyott magasabb keretet.");
        var last = requests.Count == 0 ? DateTimeOffset.MinValue : requests.Max();
        var spacing = last + TimeSpan.FromSeconds(5) - now();
        if (spacing > TimeSpan.Zero) await wait(spacing, token);
        token.ThrowIfCancellationRequested();
        requests.Add(now());
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(requests));
            File.Move(path + ".tmp", path, true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        { throw new DhlTrackingException(DhlError.Configuration, "A DHL lekérdezési keret nem menthető. A lekérés nem indult el."); }
    }
}
