using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using CatPriceCalculator.Core;
internal static class GoogleSheetsChecks
{
    private const string Id="TEST_SHEET_ID_123456789012345";
    public static async Task RunAsync(Action<bool,string> check){
        using var rsa=RSA.Create(2048);var key=rsa.ExportPkcs8PrivateKeyPem();
        var imported=GoogleCalendarSettings.ImportCredential(new JsonObject{["type"]="service_account",["client_email"]="test@example.iam.gserviceaccount.com",["private_key"]=key,["token_uri"]="https://invalid.example/steal"}.ToJsonString());
        var settings=imported with{SpreadsheetId=Id};
        check(settings.IsConfigured && !settings.ToString().Contains(key),"Google credential validates RSA key without exposing it in string output");
        check(GoogleCalendarSettings.ParseId("https://docs.google.com/spreadsheets/d/"+Id+"/edit#gid=0")==Id && GoogleCalendarSettings.ParseId(Id)==Id,"Google sheet ID accepts canonical URL and raw identifier");
        foreach(var input in new[]{"https://evil.example/spreadsheets/d/"+Id,"https://docs.google.com.evil.example/spreadsheets/d/"+Id,"bad"}){
            try{GoogleCalendarSettings.ParseId(input);check(false,"Invalid sheet URL accepted");}catch(FormatException){check(true,"Untrusted sheet URL rejected");}
        }
        try{GoogleCalendarSettings.ImportCredential("{\"type\":\"authorized_user\"}");check(false,"Wrong credential type");}catch(FormatException){check(true,"User OAuth file cannot masquerade as service-account key");}
        var today=new DateOnly(2026,10,13);
        ServiceJob Job(DateOnly date,string staff)=>new(Guid.NewGuid(),date,staff,"001","Demo Customer","M001","Demo Machine","0000123","Demo Contact","+361234567","Demo Location","=IMPORTXML(\"https://example.invalid\")","New Logged-in User");
        var jobs=new[]{Job(today,"szalai"),Job(today.AddDays(-1),"szalai"),Job(today.AddDays(1),"gulyas")};
        var metadata=new JsonObject{["sheets"]=new JsonArray()};var batch=GoogleSheetsCalendar.BuildBatch(metadata,jobs,today,new DateTimeOffset(2026,10,13,6,0,0,TimeSpan.Zero));
        var requests=(JsonArray)batch["requests"]!;var additions=requests.Where(r=>r?["addSheet"]!=null).ToArray();
        check(additions.Length==6 && additions.Select(r=>r!["addSheet"]!["properties"]!["sheetId"]!.GetValue<int>()).Distinct().Count()==6,"Sync creates one full job list and five unique staff tabs");
        var updates=requests.Where(r=>r?["updateCells"]!=null).ToArray();
        check(((JsonArray)updates[0]!["updateCells"]!["rows"]!).Count==5,"Raw sheet contains timestamp, header and every local job including history");
        check(((JsonArray)updates[1]!["updateCells"]!["rows"]!).Count==3,"Mobile staff view excludes past jobs and other staff");
        check(updates[0]!["updateCells"]!["rows"]![2]!["values"]![8]!["userEnteredValue"]!["stringValue"]!.GetValue<string>()=="+361234567","Phone remains text with plus sign");
        check(updates[0]!["updateCells"]!["rows"]![2]!["values"]![9]!["userEnteredValue"]?["formulaValue"]==null && batch.ToJsonString().Contains("stringValue"),"Descriptions that start with equals are literal strings, never formulas");
        check(updates.All(r=>r!["updateCells"]!["range"]!.AsObject().Count==1),"Full owned-sheet update clears obsolete rows rather than appending duplicates");
        var owned=new JsonArray();foreach(var add in additions)owned.Add(new JsonObject{["properties"]=add!["addSheet"]!["properties"]!.DeepClone(),["developerMetadata"]=new JsonArray(new JsonObject{["metadataKey"]="linser_calendar",["metadataValue"]="v1"})});
        var repeated=GoogleSheetsCalendar.BuildBatch(new JsonObject{["sheets"]=owned},jobs,today,DateTimeOffset.UtcNow);
        check(((JsonArray)repeated["requests"]!).All(r=>r?["addSheet"]==null),"Repeated sync reuses marked staff sheets without duplicate tabs");
        var foreign=new JsonObject{["sheets"]=new JsonArray(new JsonObject{["properties"]=new JsonObject{["sheetId"]=42,["title"]="LINSER · Munkák"}})};
        try{GoogleSheetsCalendar.BuildBatch(foreign,jobs,today,DateTimeOffset.UtcNow);check(false,"Foreign sheet overwritten");}catch(FormatException){check(true,"Unmarked existing sheet with reserved name is protected");}
        var folder=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString());Directory.CreateDirectory(folder);
        try{
            using var protector=new DhlChecks.TestProtector();var store=new GoogleCalendarSettingsStore(Path.Combine(folder,"google.bin"),protector);
            check(store.Save(settings) && store.Load().Settings.ClientEmail==settings.ClientEmail && !Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(folder,"google.bin"))).Contains("PRIVATE KEY"),"Google credential survives encrypted persistence without plaintext key on disk");
            var states=new GoogleSyncStateStore(Path.Combine(folder,"state.json"));var state=new GoogleSyncState(DateTimeOffset.UtcNow,3,true,"offline");
            check(states.Save(state) && states.Load()==state,"Pending sync and last successful time persist across restart");
        }finally{Directory.Delete(folder,true);}
        using(var handler=new GoogleHandler(rsa)){using var http=new HttpClient(handler);var service=new GoogleSheetsCalendar(http);
            check(await service.TestAsync(settings)=="Test Sheet" && handler.TokenCalls==1 && handler.ValidAssertion,"Google OAuth uses fixed audience, Sheets scope and verified RS256 JWT");
            await service.SyncAsync(settings,jobs);
            check(handler.TokenCalls==1 && handler.Writes==1 && handler.LastBatch?["requests"]!=null,"Read test and full sync reuse in-memory token; sync is one atomic batch");
            check(!http.DefaultRequestHeaders.Contains("Authorization"),"Google bearer token is request scoped, not a default HTTP header");
        }
        using(var handler=new GoogleHandler(rsa){UnauthorizedOnce=true}){using var http=new HttpClient(handler);var service=new GoogleSheetsCalendar(http);await service.TestAsync(settings);check(handler.TokenCalls==2,"One 401 refreshes Google token once and retries");}
        using(var handler=new GoogleHandler(rsa){Failure=HttpStatusCode.Forbidden}){using var http=new HttpClient(handler);var service=new GoogleSheetsCalendar(http);try{await service.SyncAsync(settings,jobs);check(false,"403 ignored");}catch(HttpRequestException e){check(handler.Writes==0 && e.Message.Contains("Szerkesztő") && !e.Message.Contains("SERVER_SECRET"),"403 gives safe sharing guidance and never writes after failed metadata read");}}
        var user=new UserSession(Guid.NewGuid(),"user","User",UserRole.User,[]);
        check(!user.Can("settings.google") && (user with{Permissions=["settings.google"]}).Can("settings.google"),"Google settings require the new explicit per-user permission");
        check((user with{Role=UserRole.Administrator}).Can("settings.google") && UserAccess.ForSettings("Google")=="settings.google","Administrator and settings navigation recognize Google permission");
    }
    private sealed class GoogleHandler(RSA rsa) : HttpMessageHandler
    {
        public int TokenCalls,Writes;public bool ValidAssertion,UnauthorizedOnce;public HttpStatusCode? Failure;public JsonObject? LastBatch;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellation){
            string result;var status=HttpStatusCode.OK;
            if(request.RequestUri!.AbsoluteUri==GoogleSheetsCalendar.TokenEndpoint){TokenCalls++;
                var form=(await request.Content!.ReadAsStringAsync(cancellation)).Split('&').Select(p=>p.Split('=',2)).ToDictionary(p=>p[0],p=>Uri.UnescapeDataString(p[1]));var assertion=form["assertion"].Split('.');
                byte[] Decode(string s)=>Convert.FromBase64String(s.Replace('-','+').Replace('_','/').PadRight((s.Length+3)/4*4,'='));
                var claims=JsonNode.Parse(Decode(assertion[1]))!;
                ValidAssertion=rsa.VerifyData(Encoding.UTF8.GetBytes(assertion[0]+"."+assertion[1]),Decode(assertion[2]),HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1) && claims["aud"]!.GetValue<string>()==GoogleSheetsCalendar.TokenEndpoint && claims["scope"]!.GetValue<string>()==GoogleSheetsCalendar.Scope;
                result="{\"access_token\":\"test-token\",\"expires_in\":3600}";
            }else{
                if(request.Headers.Authorization?.Scheme!="Bearer")throw new Exception("Missing Google bearer");
                if(UnauthorizedOnce){UnauthorizedOnce=false;status=HttpStatusCode.Unauthorized;result="{}";}
                else if(Failure!=null){status=Failure.Value;result="{\"error\":\"SERVER_SECRET\"}";}
                else if(request.Method==HttpMethod.Get)result="{\"properties\":{\"title\":\"Test Sheet\"},\"sheets\":[]}";
                else{Writes++;LastBatch=JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellation))!.AsObject();result="{}";}
            }
            return new(status){Content=new StringContent(result,Encoding.UTF8,"application/json")};
        }
    }
}
