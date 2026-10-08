using System.Text.Json;
namespace CatPriceCalculator.Core;

// Diagnostic data is deliberately limited to our messages, HTTP codes and masked parcel references.
// Never store credentials, headers, request URLs or raw provider response bodies here.
public sealed record ApiLogEntry(DateTimeOffset Timestamp, string Stage, string Message,
    int? HttpStatus = null, string? TrackingReference = null, long? ElapsedMilliseconds = null, bool IsError = false, string Carrier = "DHL");
public sealed record ApiLogLoad(List<ApiLogEntry> Entries, bool Failed = false);
public sealed class ApiLogStore(string path)
{
    public const int MaxEntries = 200;
    private readonly object gate = new();
    private readonly record struct FileStamp(bool Exists, long Length, DateTime LastWrite, DateTime Created);
    private List<ApiLogEntry>? cached;
    private FileStamp cachedStamp;
    public string FilePath => path;
    public bool LastWriteFailed { get; private set; }
    private FileStamp Stamp()
    {
        var file = new FileInfo(path);
        return file.Exists ? new(true, file.Length, file.LastWriteTimeUtc, file.CreationTimeUtc) : default;
    }
    private List<ApiLogEntry>? ReadCurrent()
    {
        try
        {
            var stamp = Stamp();
            if (cached != null && stamp == cachedStamp) return cached;
            var entries = stamp.Exists ? LocalJsonFile.Read<List<ApiLogEntry>>(path) : [];
            if (entries == null || entries.Any(e => e == null || e.Stage == null || e.Message == null || e.Carrier is not ("DHL" or "UPS")))
            { cached = null; return null; }
            if (entries.Count > MaxEntries) entries.RemoveRange(0, entries.Count - MaxEntries);
            cached = entries; cachedStamp = stamp;
            return entries;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        { cached = null; return null; }
    }
    public ApiLogLoad Load()
    {
        lock (gate)
        {
            var entries = ReadCurrent();
            // A caller can modify its snapshot without corrupting our bounded cache.
            return entries == null ? new([], true) : new(new(entries));
        }
    }
    public bool Append(ApiLogEntry entry)
    {
        lock (gate)
        {
            var entries = ReadCurrent();
            if (entries == null) { LastWriteFailed = true; return false; }
            List<ApiLogEntry> next = new(MaxEntries);
            next.AddRange(entries.Count == MaxEntries ? entries.Skip(1) : entries);
            next.Add(entry);
            bool saved = Write(next); LastWriteFailed = !saved; return saved;
        }
    }
    public bool Clear() { lock (gate) { bool saved = Write([]); LastWriteFailed = !saved; return saved; } }
    private bool Write(List<ApiLogEntry> entries)
    {
        if (!LocalJsonFile.Write(path, entries)) return false;
        cached = null;
        // Cache only after a successful atomic write. Every diagnostic is still persisted immediately.
        try { cachedStamp = Stamp(); cached = entries; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        return true;
    }
}
