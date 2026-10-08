using System.Text;
using System.Text.Json;
using CatPriceCalculator.Core;

public static class StorageOptimizationChecks
{
    public static void Run(Action<bool, string> check)
    {
        string root = Path.Combine(Path.GetTempPath(), "linser-storage-opt-" + Guid.NewGuid()); Directory.CreateDirectory(root);
        var now = new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);
        try
        {
            string path = Path.Combine(root, "log.json"); var store = new ApiLogStore(path);
            var first = new ApiLogEntry(now, "HTTP", "Első biztonságos üzenet", Carrier: "UPS");
            store.Append(first);
            var snapshot = store.Load(); snapshot.Entries.Clear(); store.Append(first with { Message = "Második" });
            check(store.Load().Entries.Count == 2, "Log snapshots cannot mutate cached or persisted history");
            var external = new ApiLogEntry(now, "Külső", "Külső módosítás", Carrier: "DHL");
            File.WriteAllText(path, JsonSerializer.Serialize(new[] { external })); File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(1));
            check(store.Load().Entries.SequenceEqual(new[] { external }), "Log cache detects external file replacement");
            store.Append(first);
            check(store.Load().Entries.SequenceEqual(new[] { external, first }), "Append after external change retains externally replaced data");
            byte[] prior = File.ReadAllBytes(path); Directory.CreateDirectory(path + ".tmp");
            check(!store.Append(first with { Message = "Must not appear" }) && store.LastWriteFailed && prior.SequenceEqual(File.ReadAllBytes(path)), "Atomic streamed write failure preserves existing log bytes");
            check(store.Load().Entries.SequenceEqual(new[] { external, first }), "Failed append does not pollute log cache");
            Directory.Delete(path + ".tmp");
            check(store.Append(first) && !store.LastWriteFailed && store.Load().Entries.Count == 3, "Log writer recovers without losing cached history after temporary failure");
            File.WriteAllText(path, "broken file");
            check(store.Load().Failed && !store.Append(first) && File.ReadAllText(path) == "broken file", "Cached logger fails closed on external corruption without overwriting it");
            check(store.Clear() && !store.Load().Failed && store.Load().Entries.Count == 0, "Explicit clear repairs corrupted cache/file atomically");
            store.Append(first); File.Delete(path);
            check(store.Load().Entries.Count == 0 && store.Append(external) && store.Load().Entries.SequenceEqual(new[] { external }), "External log deletion never resurrects cached records");
            // The old ReadAllText accepted UTF-8 BOM. Keep that compatibility with streamed reads.
            File.WriteAllBytes(path, Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new[] { first }))).ToArray());
            check(!store.Load().Failed && store.Load().Entries.Single() == first, "Streamed JSON retains legacy UTF-8 BOM and Hungarian text compatibility");
            var concurrent = new ApiLogStore(Path.Combine(root, "parallel.json"));
            Parallel.For(0, 16, i => { if (!concurrent.Append(first with { Message = i.ToString() })) throw new Exception("Concurrent append failed"); });
            check(concurrent.Load().Entries.Count == 16 && concurrent.Load().Entries.Select(e => e.Message).Distinct().Count() == 16, "Bounded log cache serializes concurrent diagnostics without missing records");
            string ratesPath = Path.Combine(root, "rates.json"); var rates = new SettingsStore(ratesPath); var quote = new RateSettings(400m, "Európai Központi Bank", new DateOnly(2026, 10, 8), now);
            check(rates.Save(quote) && rates.Load() == quote, "Shared streamed storage preserves rate/date/time/provenance fields");
            prior = File.ReadAllBytes(ratesPath); Directory.CreateDirectory(ratesPath + ".tmp");
            check(!rates.Save(quote with { Rate = 450m }) && prior.SequenceEqual(File.ReadAllBytes(ratesPath)), "Shared atomic JSON writer preserves rate file on interrupted write");
            var parcels = Enumerable.Range(0, 100).Select(i => ParcelBook.Add(i.ToString(), Courier.Ups, "", now.AddMinutes(i % 7)) with
                { State = i % 3 == 0 ? ParcelState.Delivered : ParcelState.InTransit, DeliveredAt = i % 3 == 0 ? now.AddMinutes(i % 5) : null }).ToArray();
            foreach (bool delivered in new[] { false, true })
            foreach (int requested in new[] { -1, 0, 1, 3, 99 })
            {
                var expected = parcels.Where(p => (p.State == ParcelState.Delivered) == delivered).OrderByDescending(p => delivered ? p.DeliveredAt : p.AddedAt).ToArray();
                int pages = Math.Max(1, (expected.Length + ParcelBook.PageSize - 1) / ParcelBook.PageSize), page = Math.Clamp(requested, 0, pages - 1);
                var actual = ParcelBook.Page(parcels, delivered, requested);
                check(actual.Count == expected.Length && actual.Pages == pages && actual.Index == page && actual.Rows.SequenceEqual(expected.Skip(page * ParcelBook.PageSize).Take(ParcelBook.PageSize)), "Optimized parcel pagination preserves sorting, ties and bounds: " + delivered + "/" + requested);
            }
            var empty = ParcelBook.Page([], false, 100);
            check(empty.Count == 0 && empty.Index == 0 && empty.Pages == 1 && empty.Rows.Length == 0, "Optimized parcel pagination keeps empty-state behavior without scrollbars");
        }
        finally { Directory.Delete(root, true); }
    }
}
