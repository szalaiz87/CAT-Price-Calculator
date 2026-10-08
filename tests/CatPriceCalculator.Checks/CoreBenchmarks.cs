using System.Diagnostics;
using System.Text.Json;
using CatPriceCalculator.Core;

// Opt-in, local-only benchmark. No network, no app settings, no production log file.
public static class CoreBenchmarks
{
    public static void Run()
    {
        string root = Path.Combine(Path.GetTempPath(), "linser-benchmark-" + Guid.NewGuid()); Directory.CreateDirectory(root);
        try
        {
            var records = new List<object>();
            for (int run = 0; run < 5; run++)
            {
                var store = new ApiLogStore(Path.Combine(root, "log-" + run + ".json"));
                var entry = new ApiLogEntry(DateTimeOffset.Parse("2026-10-08T12:00:00Z"), "Feldolgozás", "A követési válasz sikeresen feldolgozva.", 200, "…3456", 123, Carrier: "UPS");
                for (int i = 0; i < 200; i++) if (!store.Append(entry)) throw new Exception("Benchmark seed failed.");
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                long before = GC.GetAllocatedBytesForCurrentThread(); var time = Stopwatch.StartNew();
                for (int i = 0; i < 1000; i++) if (!store.Append(entry)) throw new Exception("Benchmark append failed.");
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before; time.Stop();
                if (store.Load().Entries.Count != 200) throw new Exception("Benchmark log cap incorrect.");
                records.Add(new { scenario = "API log 1000 appends at capacity", run, allocatedBytes = allocated, milliseconds = time.Elapsed.TotalMilliseconds });
            }
            Console.WriteLine(JsonSerializer.Serialize(records));
        }
        finally { Directory.Delete(root, true); }
    }
}
