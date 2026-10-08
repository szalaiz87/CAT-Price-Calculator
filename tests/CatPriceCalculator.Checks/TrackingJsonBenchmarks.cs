using System.Diagnostics;
using System.Text;
using System.Text.Json;
public static class TrackingJsonBenchmarks
{
    // Isolated response-body processing, without network, quota files, GUI or company credentials.
    public static async Task Run()
    {
        byte[] payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { shipments = new[] { new { id = "test-only", status = new { statusCode = "transit" }, description = new string('x', 1024 * 1024) } } }));
        async Task Parse(bool utf8)
        {
            using var content = new ByteArrayContent(payload);
            using var doc = utf8 ? await JsonDocument.ParseAsync(await content.ReadAsStreamAsync()) : JsonDocument.Parse(await content.ReadAsStringAsync());
            if (doc.RootElement.GetProperty("shipments")[0].GetProperty("status").GetProperty("statusCode").GetString() != "transit") throw new Exception("Benchmark result changed");
        }
        for (int i = 0; i < 10; i++) { await Parse(false); await Parse(true); }
        var results = new List<object>();
        for (int run = 0; run < 5; run++)
            foreach (bool utf8 in run % 2 == 0 ? new[] { false, true } : new[] { true, false })
            {
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                long start = GC.GetTotalAllocatedBytes(true); var timer = Stopwatch.StartNew();
                for (int i = 0; i < 20; i++) await Parse(utf8);
                timer.Stop(); long allocated = GC.GetTotalAllocatedBytes(true) - start;
                results.Add(new { scenario = utf8 ? "UTF8 stream" : "beta6 UTF16 string", run, responses = 20, responseBytes = payload.Length, allocatedBytes = allocated, milliseconds = timer.Elapsed.TotalMilliseconds });
            }
        Console.WriteLine(JsonSerializer.Serialize(results));
    }
}
