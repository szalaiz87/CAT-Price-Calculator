using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CatPriceCalculator.Core;
public static class UpdateChecks
{
    public static async Task Run(Action<bool,string> check)
    {
        var profit = PriceCalculator.Profit(73562.5m, 102988m);
        check(profit.Amount == 29425.5m && decimal.Round(profit.MarginPercent!.Value,2) == 28.57m, "Profit and margin use displayed selling price");
        check(PriceCalculator.DefaultSellingMultiplier == 1.40m, "Default 40 percent markup");
        check(PriceCalculator.Profit(0.1m,0m) == (-0.1m,null), "Zero rounded selling price has no undefined margin");
        using var zipBytes = new MemoryStream();
        using (var zip = new ZipArchive(zipBytes, ZipArchiveMode.Create,true))
        { using var entry = zip.CreateEntry("CAT-Price-Calculator.exe").Open(); entry.Write([0x4d,0x5a,1,2]); }
        var bytes = zipBytes.ToArray();
        const string baseUrl = "https://github.com/szalaiz87/CAT-Price-Calculator/releases/download/v0.7.0/";
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var checksum = Encoding.UTF8.GetBytes(hash + "  CAT-Price-Calculator-Windows-x64.zip\n");
        var assets = new[] { new UpdateAsset("SHA256SUMS.txt",baseUrl+"SHA256SUMS.txt",checksum.Length,null),new UpdateAsset("CAT-Windows-x64.zip.part01",baseUrl+"CAT-Windows-x64.zip.part01",bytes.Length,"sha256:"+hash) };
        var metadata = JsonSerializer.SerializeToUtf8Bytes(new { draft=false, prerelease=false, tag_name="v0.7.0",html_url="https://github.com/szalaiz87/CAT-Price-Calculator/releases/tag/v0.7.0",assets=assets.Select(a=>new {name=a.Name,browser_download_url=a.Url,size=a.Size,digest=a.Digest}) });
        var responses = new Dictionary<string,byte[]> { [UpdateService.LatestEndpoint]=metadata,[assets[0].Url]=checksum,[assets[1].Url]=bytes };
        using var handler = new FixtureHandler(responses);
        using var http = new HttpClient(handler);
        var service = new UpdateService(http);
        var release = await service.CheckAsync(new Version(0,6,0));
        check(release?.Version == new Version(0,7,0), "New public release detected");
        check(await service.CheckAsync(new Version(0,7,0)) == null && await service.CheckAsync(new Version(0,8,0)) == null, "Equal and older versions do not update");
        var directory = Path.Combine(Path.GetTempPath(),"cat-update-test-"+Guid.NewGuid());
        try
        {
            var exe = await service.PrepareAsync(release!,directory);
            check(File.ReadAllBytes(exe).SequenceEqual(new byte[] {0x4d,0x5a,1,2}), "Parts assembled, checksum verified and executable staged");
            File.Delete(exe);
            responses[assets[1].Url] = bytes.Select((b,i)=> i==0 ? (byte)(b^1) : b).ToArray();
            try { await service.PrepareAsync(release!,directory); throw new Exception("Corrupt update accepted"); }
            catch (InvalidDataException) { check(!File.Exists(exe), "Corrupt part rejected before executable extraction"); }
            responses[assets[1].Url]=bytes;
            responses[assets[0].Url]=Encoding.UTF8.GetBytes(new string('0',64)+"  CAT-Price-Calculator-Windows-x64.zip\n");
            try { await service.PrepareAsync(release!,directory); throw new Exception("Bad final hash accepted"); }
            catch (InvalidDataException) { check(!File.Exists(exe), "Full archive checksum required even when part checks pass"); }
            responses[assets[0].Url]=checksum;
            responses[assets[1].Url]=bytes[..^1];
            try { await service.PrepareAsync(release!,directory); throw new Exception("Truncated update accepted"); }
            catch (InvalidDataException) { check(!File.Exists(exe), "Truncated stream rejected before executable extraction"); }
            responses[assets[1].Url]=[..bytes,0];
            try { await service.PrepareAsync(release!,directory); throw new Exception("Oversized update accepted"); }
            catch (InvalidDataException) { check(!File.Exists(exe), "Oversized stream rejected before executable extraction"); }
            responses[assets[1].Url]=bytes;
            using (var streaming = new HttpClient(new StreamingHandler(responses)))
            {
                var downloaded=await new UpdateService(streaming).PrepareAsync(release!,directory);
                check(File.ReadAllBytes(downloaded).SequenceEqual(new byte[] {0x4d,0x5a,1,2}), "Update body read as a stream without HttpClient buffering");
                File.Delete(downloaded);
            }
            using (var slow = new HttpClient(new StreamingHandler(responses,true)) {Timeout=TimeSpan.FromMilliseconds(20)})
            {
                try {await new UpdateService(slow).PrepareAsync(release!,directory);throw new Exception("Body timeout ignored");}
                catch(OperationCanceledException) {check(!File.Exists(exe), "ResponseHeadersRead preserves timeout during response-body transfer");}
            }
            var untrusted = release! with { Assets = [assets[0] with { Url="https://example.com/secret" }, assets[1]] };
            try { await service.PrepareAsync(untrusted,directory); throw new Exception("Untrusted URL accepted"); }
            catch (InvalidDataException) { check(true, "Untrusted download host rejected"); }
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory,true); }
        check(handler.NoAuthorization, "Update checks and downloads send no GitHub token");
        responses.Remove(UpdateService.LatestEndpoint);
        check(await service.CheckAsync(new Version(0,6,0)) == null, "No published release handled");
    }
    private sealed class StreamingHandler(Dictionary<string,byte[]> responses,bool slow=false):HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {Content=new StreamingContent(responses[request.RequestUri!.AbsoluteUri],slow)});
    }
    private sealed class StreamingContent(byte[] bytes,bool slow):HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream,System.Net.TransportContext? context) => throw new InvalidOperationException("Buffering forbidden in this fixture");
        protected override bool TryComputeLength(out long length) {length=0;return false;}
        protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(new ResponseStream(bytes,slow));
        protected override Task<Stream> CreateContentReadStreamAsync(CancellationToken token) => CreateContentReadStreamAsync();
    }
    private sealed class ResponseStream(byte[] bytes,bool slow):MemoryStream(bytes,false)
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken token=default)
        {
            if(buffer.Length>65536)throw new Exception("Streaming buffer grew above 64 KiB");
            if(slow)await Task.Delay(100,token);
            return await base.ReadAsync(buffer,token);
        }
    }
    private sealed class FixtureHandler(Dictionary<string,byte[]> responses) : HttpMessageHandler
    {
        public bool NoAuthorization { get; private set; } = true;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); NoAuthorization &= request.Headers.Authorization == null;
            if (!responses.TryGetValue(request.RequestUri!.AbsoluteUri,out var bytes)) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content=new ByteArrayContent(bytes) });
        }
    }
}
