using System.Net;
using System.Text;
using System.Text.Json;
using CatPriceCalculator.Core;
public static class UpdateChannelChecks
{
    public static async Task Run(Action<bool,string> check)
    {
        check(ReleaseVersion.Parse("v1.0+commit").ToString()=="1.0.0", "Stable version normalizes and ignores build metadata");
        check(ReleaseVersion.Parse("1.1.0-beta.10").CompareTo(ReleaseVersion.Parse("1.1.0-beta.2"))>0,"Beta sequence comparison is numeric");
        check(ReleaseVersion.Parse("1.1.0").CompareTo(ReleaseVersion.Parse("1.1.0-beta.99"))>0,"Stable promotion outranks the same numeric beta");
        check(ReleaseVersion.Parse("1.1.0-beta.1").CompareTo(ReleaseVersion.Parse("1.0.0"))>0,"Next-version beta outranks older stable");
        foreach(var invalid in new[]{"", "garbage", "1.1.0-rc.1", "1.1.0-beta.0", "1.1.0-beta.-1", "1.1.0-beta.01", "1.1.0-beta.x", "1.1.0-beta.1-beta.2", "1.1.0.1"})
            check(!ReleaseVersion.TryParse(invalid,out _), "Reject unsupported release identifier: "+invalid);
        check(BuildInfo.UserAgent=="LinserHungary/"+BuildInfo.Current, "Build metadata controls version and user agent in stable and beta builds");
        var directory=Path.Combine(Path.GetTempPath(),"linser-channel-"+Guid.NewGuid());
        try
        {
            var path=Path.Combine(directory,"startup-preferences.json");var store=new StartupPreferencesStore(path);
            check(!store.Load().AllowBetaUpdates,"Beta updates disabled by default");
            Directory.CreateDirectory(directory);File.WriteAllText(path,"{\"AlwaysOnTop\":true,\"RefreshRates\":true}");
            check(store.Load()==new StartupPreferences(true,true,false),"Legacy startup preferences preserve flags and default to stable channel");
            check(store.Save(new(true,true,true)) && new StartupPreferencesStore(path).Load()==new StartupPreferences(true,true,true),"Beta opt-in persists without changing startup flags");
            check(store.Save(new(true,true,false)) && store.Load()==new StartupPreferences(true,true,false),"Stable rollback preference disables beta and preserves startup flags");
        } finally {Directory.Delete(directory,true);}
        var stable=Release("v1.0.0");
        var beta2=Release("v1.1.0-beta.2",true);var beta10=Release("v1.1.0-beta.10",true);
        var responses=new Dictionary<string,string> {
            [UpdateService.LatestEndpoint]=JsonSerializer.Serialize(stable),
            [UpdateService.ReleasesEndpoint+"1"]=JsonSerializer.Serialize(new[]{Release("v9.0.0-beta.1",true,true),Release("v8.0.0-rc.1",true),Release("v7.0.0-beta.1",false),beta2,stable,beta10})
        };
        using var handler=new FixtureHandler(responses);using var http=new HttpClient(handler);var service=new UpdateService(http);
        var current=ReleaseVersion.Parse("1.0.0");
        check(await service.CheckAsync(current,false)==null && handler.ListCalls==0,"Stable-only updates never query or offer beta feed");
        check((await service.CheckAsync(ReleaseVersion.Parse("0.11.2"),false))?.DisplayVersion=="1.0.0","Previous stable version upgrades to official 1.0");
        var latestBeta=await service.CheckAsync(current,true);
        check(latestBeta?.DisplayVersion=="1.1.0-beta.10" && latestBeta.IsBeta,"Beta opt-in chooses latest eligible beta and excludes drafts, RCs and mislabeled tags");
        check(await service.CheckAsync(ReleaseVersion.Parse("1.1.0-beta.10"),true)==null,"Installed beta does not reinstall itself");
        check(await service.CheckAsync(ReleaseVersion.Parse("1.1.0-beta.10"),false)==null,"Disabling beta does not silently downgrade");
        check((await service.GetLatestStableAsync())?.DisplayVersion=="1.0.0","Explicit rollback finds latest stable even below installed beta number");
        responses[UpdateService.LatestEndpoint]=JsonSerializer.Serialize(Release("v1.1.0"));
        responses[UpdateService.ReleasesEndpoint+"1"]=JsonSerializer.Serialize(new[]{beta10,Release("v1.1.0")});
        check((await service.CheckAsync(ReleaseVersion.Parse("1.1.0-beta.10"),false))?.DisplayVersion=="1.1.0","Stable promotion upgrades beta on stable-only channel");
        check((await service.CheckAsync(ReleaseVersion.Parse("1.1.0-beta.10"),true))?.IsBeta==false,"Beta-enabled channel also receives stable promotions");
        responses[UpdateService.ReleasesEndpoint+"1"]=JsonSerializer.Serialize(Enumerable.Range(1,100).Select(i=>Release($"v1.1.0-beta.{i}",true)));
        responses[UpdateService.ReleasesEndpoint+"2"]=JsonSerializer.Serialize(new[]{Release("v1.2.0-beta.1",true),stable});
        check((await service.CheckAsync(current,true))?.DisplayVersion=="1.2.0-beta.1","Eligible release selection follows pagination beyond 100 results");
        responses[UpdateService.LatestEndpoint]=JsonSerializer.Serialize(Release("v1.2.0-beta.1",true));
        check(await service.GetLatestStableAsync()==null,"Rollback and stable endpoint refuse prerelease payloads");
        responses.Remove(UpdateService.LatestEndpoint);
        check(await service.GetLatestStableAsync()==null,"Missing stable release is handled without selecting a beta");
        check(handler.NoAuthorization,"Both update channels remain public and send no GitHub token");
    }
    private static object Release(string tag,bool beta=false,bool draft=false) => new {tag_name=tag,prerelease=beta,draft,html_url="https://github.com/szalaiz87/CAT-Price-Calculator/releases/tag/"+tag,assets=Array.Empty<object>()};
    private sealed class FixtureHandler(Dictionary<string,string> responses):HttpMessageHandler
    {
        public int ListCalls;public bool NoAuthorization=true;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();NoAuthorization&=request.Headers.Authorization==null;
            string url=request.RequestUri!.AbsoluteUri;if(url.StartsWith(UpdateService.ReleasesEndpoint))ListCalls++;
            return Task.FromResult(responses.TryGetValue(url,out var text)?new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(text,Encoding.UTF8,"application/json")}:new(HttpStatusCode.NotFound));
        }
    }
}
