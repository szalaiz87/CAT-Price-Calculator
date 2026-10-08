using System.Text.Json;
namespace CatPriceCalculator.Core;

// Diagnostic data is deliberately limited to our messages, HTTP codes and masked parcel references.
// Never store credentials, headers, request URLs or raw DHL response bodies here.
public sealed record DhlLogEntry(DateTimeOffset Timestamp, string Stage, string Message,
    int? HttpStatus = null, string? TrackingReference = null, long? ElapsedMilliseconds = null, bool IsError = false);
public sealed record DhlLogLoad(List<DhlLogEntry> Entries, bool Failed = false);
public sealed class DhlLogStore(string path)
{
    public const int MaxEntries = 200;
    private readonly object gate = new();
    public string FilePath => path;
    public bool LastWriteFailed { get; private set; }
    public DhlLogLoad Load()
    {
        lock (gate)
        {
            try
            {
                if (!File.Exists(path)) return new([]);
                var entries = JsonSerializer.Deserialize<List<DhlLogEntry>>(File.ReadAllText(path));
                if (entries == null || entries.Any(e => e == null || e.Stage == null || e.Message == null)) return new([], true);
                return new(entries.TakeLast(MaxEntries).ToList());
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { return new([], true); }
        }
    }
    public bool Append(DhlLogEntry entry)
    {
        lock (gate)
        {
            var loaded = Load();
            if (loaded.Failed) { LastWriteFailed = true; return false; }
            bool saved = Write([.. loaded.Entries.TakeLast(MaxEntries - 1), entry]);
            LastWriteFailed = !saved; return saved;
        }
    }
    public bool Clear() { lock (gate) { bool saved = Write([]); LastWriteFailed = !saved; return saved; } }
    private bool Write(List<DhlLogEntry> entries)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(entries));
            File.Move(path + ".tmp", path, true); return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return false; }
    }
}
