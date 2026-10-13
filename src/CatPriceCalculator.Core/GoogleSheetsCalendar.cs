using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
namespace CatPriceCalculator.Core;

public sealed record GoogleCalendarSettings(string SpreadsheetId="",string ClientEmail="",string PrivateKey="",bool AutoSync=false)
{
    public bool IsConfigured=>ValidId(SpreadsheetId) && ClientEmail!=null && PrivateKey!=null && ClientEmail.EndsWith(".iam.gserviceaccount.com",StringComparison.OrdinalIgnoreCase) && PrivateKey.Contains("PRIVATE KEY");
    public override string ToString()=>"Google Sheets: configured="+IsConfigured;
    public static bool ValidId(string? id)=>id!=null && Regex.IsMatch(id,@"\A[A-Za-z0-9_-]{20,200}\z");
    public static string ParseId(string text){
        text=text.Trim();if(ValidId(text))return text;
        if(Uri.TryCreate(text,UriKind.Absolute,out var uri) && uri.Scheme=="https" && uri.Host=="docs.google.com"){
            var match=Regex.Match(uri.AbsolutePath,@"^/spreadsheets/d/([A-Za-z0-9_-]+)(/|$)");if(match.Success && ValidId(match.Groups[1].Value))return match.Groups[1].Value;
        }
        throw new FormatException("Adjon meg érvényes Google Táblázatok-hivatkozást vagy táblázatazonosítót.");
    }
    public static GoogleCalendarSettings ImportCredential(string json){
        if(json.Length>65536)throw new FormatException("Túl nagy Google-kulcsfájl.");
        try{
            using var doc=JsonDocument.Parse(json);var r=doc.RootElement;
            if(r.GetProperty("type").GetString()!="service_account")throw new FormatException("Google szolgáltatásfiók JSON-kulcs szükséges.");
            var email=r.GetProperty("client_email").GetString()??"";var key=r.GetProperty("private_key").GetString()??"";
            if(email.Length>254 || !email.EndsWith(".iam.gserviceaccount.com",StringComparison.OrdinalIgnoreCase) || email.Any(char.IsWhiteSpace))throw new FormatException("Hibás szolgáltatásfiók-cím.");
            using var rsa=RSA.Create();rsa.ImportFromPem(key);if(rsa.KeySize<2048)throw new FormatException("Legalább 2048 bites Google-kulcs szükséges.");
            return new(ClientEmail:email,PrivateKey:key);
        }catch(Exception e) when(e is JsonException or KeyNotFoundException or InvalidOperationException or CryptographicException or ArgumentException){throw new FormatException("Nem olvasható Google szolgáltatásfiók-kulcs. Válasszon érvényes JSON-fájlt.");}
    }
}
public sealed class GoogleCalendarSettingsStore(string path,ISecretProtector protector)
{
    private readonly ProtectedSettingsStore<GoogleCalendarSettings> store=new(path,protector,()=>new(),s=>s.SpreadsheetId=="" && s.ClientEmail=="" && s.PrivateKey=="" && !s.AutoSync || s.IsConfigured && s.PrivateKey.Length<65536 && s.ClientEmail.Length<=254);
    public ProtectedSettingsLoad<GoogleCalendarSettings> Load()=>store.Load();
    public bool Save(GoogleCalendarSettings value)=>store.Save(value);
}
public sealed record GoogleSyncState(DateTimeOffset? LastSuccess=null,int Jobs=0,bool Pending=false,string Error="");
public sealed class GoogleSyncStateStore(string path)
{
    public GoogleSyncState Load(){try{return LocalJsonFile.Read<GoogleSyncState>(path)??new();}catch(Exception e) when(e is IOException or UnauthorizedAccessException or JsonException){return new(Pending:true,Error:"A korábbi szinkronállapot nem olvasható.");}}
    public bool Save(GoogleSyncState state)=>LocalJsonFile.Write(path,state);
}
public sealed class GoogleSheetsCalendar(HttpClient http)
{
    public const string Api="https://sheets.googleapis.com/v4/spreadsheets/",TokenEndpoint="https://oauth2.googleapis.com/token",Scope="https://www.googleapis.com/auth/spreadsheets";
    private string? token;
    private DateTimeOffset tokenExpiry;
    private string identity="";
    public void ForgetToken(){token=null;tokenExpiry=default;identity="";}
    private static string B64(byte[] data)=>Convert.ToBase64String(data).TrimEnd('=').Replace('+','-').Replace('/','_');
    private async Task<string> TokenAsync(GoogleCalendarSettings settings,CancellationToken cancellation){
        if(token!=null && identity==settings.ClientEmail+settings.PrivateKey && tokenExpiry>DateTimeOffset.UtcNow.AddMinutes(2))return token;
        var now=DateTimeOffset.UtcNow;var header=B64(JsonSerializer.SerializeToUtf8Bytes(new{alg="RS256",typ="JWT"}));
        var body=B64(JsonSerializer.SerializeToUtf8Bytes(new{iss=settings.ClientEmail,scope=Scope,aud=TokenEndpoint,iat=now.ToUnixTimeSeconds(),exp=now.AddHours(1).ToUnixTimeSeconds()}));
        using var rsa=RSA.Create();rsa.ImportFromPem(settings.PrivateKey);var unsigned=header+"."+body;
        var assertion=unsigned+"."+B64(rsa.SignData(Encoding.UTF8.GetBytes(unsigned),HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1));
        using var request=new HttpRequestMessage(HttpMethod.Post,TokenEndpoint){Content=new FormUrlEncodedContent(new Dictionary<string,string>{{"grant_type","urn:ietf:params:oauth:grant-type:jwt-bearer"},{"assertion",assertion}})};
        using var response=await http.SendAsync(request,cancellation);
        if(!response.IsSuccessStatusCode)throw new HttpRequestException("A Google-hitelesítés sikertelen. Ellenőrizze a kulcsot és a számítógép óráját.",null,response.StatusCode);
        using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellation));
        token=json.RootElement.GetProperty("access_token").GetString()??throw new FormatException("A Google nem adott hozzáférési tokent.");
        var seconds=json.RootElement.GetProperty("expires_in").GetInt32();tokenExpiry=now.AddSeconds(Math.Clamp(seconds,1,3600));identity=settings.ClientEmail+settings.PrivateKey;return token;
    }
    private async Task<JsonObject> RequestAsync(GoogleCalendarSettings settings,HttpMethod method,string suffix,JsonObject? payload,CancellationToken cancellation){
        if(!settings.IsConfigured)throw new FormatException("Mentse a Google-kapcsolat beállításait.");
        for(var attempt=0;;attempt++){
            using var request=new HttpRequestMessage(method,Api+settings.SpreadsheetId+suffix);
            request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",await TokenAsync(settings,cancellation));
            if(payload!=null)request.Content=new StringContent(payload.ToJsonString(),Encoding.UTF8,"application/json");
            using var response=await http.SendAsync(request,cancellation);
            if(response.StatusCode==HttpStatusCode.Unauthorized && attempt==0){ForgetToken();continue;}
            if(!response.IsSuccessStatusCode){var message=response.StatusCode switch{
                HttpStatusCode.Forbidden=>"A Google megtagadta a hozzáférést. Kapcsolja be a Sheets API-t, és ossza meg a táblázatot a szolgáltatásfiókkal Szerkesztőként.",
                HttpStatusCode.NotFound=>"A Google-táblázat nem található vagy nincs megosztva a szolgáltatásfiókkal.",
                HttpStatusCode.TooManyRequests=>"A Google lekérési kerete elfogyott. Próbálja később.",_=>"A Google-kapcsolat sikertelen (HTTP "+(int)response.StatusCode+"). A helyi naptár megmaradt."};throw new HttpRequestException(message,null,response.StatusCode);}
            return JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellation)) as JsonObject??throw new FormatException("Érvénytelen Google-válasz.");
        }
    }
    private const string MetadataFields="?fields=spreadsheetId,properties(title),sheets(properties(sheetId,title,gridProperties),developerMetadata(metadataKey,metadataValue))";
    public async Task<string> TestAsync(GoogleCalendarSettings settings,CancellationToken cancellation=default){var metadata=await RequestAsync(settings,HttpMethod.Get,MetadataFields,null,cancellation);return metadata["properties"]?["title"]?.GetValue<string>()??"Google-táblázat";}
    public async Task SyncAsync(GoogleCalendarSettings settings,ServiceJob[] jobs,CancellationToken cancellation=default){
        if(jobs.Any(j=>!ServiceCalendar.Valid(j)) || jobs.Select(j=>j.Id).Distinct().Count()!=jobs.Length)throw new FormatException("Hibás helyi naptáradat. Szinkronizálás leállítva.");
        var metadata=await RequestAsync(settings,HttpMethod.Get,MetadataFields,null,cancellation);
        var today=DateOnly.ParseExact(RateTimestamp.Format(DateTimeOffset.UtcNow)[..10],"yyyy.MM.dd",PriceCalculator.Hungarian);
        var payload=BuildBatch(metadata,jobs,today,DateTimeOffset.UtcNow);
        if(Encoding.UTF8.GetByteCount(payload.ToJsonString())>2*1024*1024)throw new FormatException("A szinkroncsomag meghaladja a 2 MB-ot. A táblázat változatlan; a naptár archiválása vagy kötegelt szinkron szükséges.");
        await RequestAsync(settings,HttpMethod.Post,":batchUpdate",payload,cancellation);
    }
    public static JsonObject BuildBatch(JsonObject metadata,ServiceJob[] jobs,DateOnly today,DateTimeOffset timestamp){
        var sheets=(metadata["sheets"] as JsonArray??new()).OfType<JsonObject>().ToArray();if(sheets.Any(s=>s["properties"]?["sheetId"]==null || s["properties"]?["title"]==null))throw new FormatException("Hiányos Google-munkalapadatok. A táblázat változatlan.");
        var ids=sheets.Select(s=>s["properties"]!["sheetId"]!.GetValue<int>()).ToHashSet();
        var exports=new List<(string Title,string[][] Rows)>();
        var ordered=jobs.OrderBy(j=>j.Date).ThenBy(j=>j.StaffId).ThenBy(j=>j.Id).ToArray();
        string Staff(ServiceJob j)=>ServiceCalendar.Staff.First(s=>s.Id==j.StaffId).Name;
        exports.Add(("LINSER · Munkák",ordered.Select(j=>new[]{j.Id.ToString(),j.Date.ToString("yyyy.MM.dd"),Staff(j),j.CustomerName,j.MachineName,j.SerialNumber,j.Location,j.ContactName,j.Phone,j.Description,j.RecordedBy??"",j.CustomerId,j.MachineId}).Prepend(new[]{"Feladatazonosító","Dátum","Szerelő","Ügyfél","Gép","Alvázszám","Helyszín","Kapcsolattartó","Telefon","Hibaleírás","Rögzítette","Ügyfélazonosító","Gépazonosító"}).ToArray()));
        foreach(var staff in ServiceCalendar.Staff)exports.Add(("LINSER · "+staff.Name,ordered.Where(j=>j.StaffId==staff.Id && j.Date>=today).Select(j=>new[]{j.Date.ToString("yyyy.MM.dd"),j.CustomerName,j.MachineName+"\n"+j.SerialNumber,j.Location,j.ContactName+"\n"+j.Phone,j.Description}).Prepend(new[]{"Dátum","Ügyfél","Gép / alvázszám","Helyszín","Kapcsolat / telefon","Hibaleírás"}).ToArray()));
        var requests=new JsonArray();int nextId=170000;
        foreach(var export in exports){
            var sheet=sheets.SingleOrDefault(s=>s["properties"]?["title"]?.GetValue<string>()==export.Title);int id;
            if(sheet==null){while(ids.Contains(nextId))nextId++;id=nextId++;ids.Add(id);
                requests.Add(JsonSerializer.SerializeToNode(new{addSheet=new{properties=new{sheetId=id,title=export.Title,gridProperties=new{rowCount=Math.Max(100,export.Rows.Length+1),columnCount=export.Rows[0].Length,frozenRowCount=2}}}}));
                requests.Add(JsonSerializer.SerializeToNode(new{createDeveloperMetadata=new{developerMetadata=new{metadataKey="linser_calendar",metadataValue="v1",visibility="DOCUMENT",location=new{sheetId=id}}}}));
            }else{
                if(!(sheet["developerMetadata"] as JsonArray??new()).Any(m=>m?["metadataKey"]?.GetValue<string>()=="linser_calendar" && m?["metadataValue"]?.GetValue<string>()=="v1"))throw new FormatException("Már létezik idegen „"+export.Title+"” munkalap. Nevezze át; az app nem írja felül.");
                id=sheet["properties"]!["sheetId"]!.GetValue<int>();
                var grid=sheet["properties"]?["gridProperties"];
                requests.Add(JsonSerializer.SerializeToNode(new{updateSheetProperties=new{properties=new{sheetId=id,gridProperties=new{rowCount=Math.Max(grid?["rowCount"]?.GetValue<int>()??100,export.Rows.Length+1),columnCount=Math.Max(grid?["columnCount"]?.GetValue<int>()??0,export.Rows[0].Length),frozenRowCount=2}},fields="gridProperties"}}));
            }
            var rows=new JsonArray();rows.Add(JsonSerializer.SerializeToNode(new{values=new[]{new{userEnteredValue=new{stringValue="Frissítve: "+RateTimestamp.Format(timestamp)}}}}));
            foreach(var row in export.Rows)rows.Add(JsonSerializer.SerializeToNode(new{values=row.Select(value=>new{userEnteredValue=new{stringValue=value}}).ToArray()}));
            requests.Add(new JsonObject{["updateCells"]=new JsonObject{["range"]=new JsonObject{["sheetId"]=id},["rows"]=rows,["fields"]="userEnteredValue"}});
            requests.Add(JsonSerializer.SerializeToNode(new{repeatCell=new{range=new{sheetId=id},cell=new{userEnteredFormat=new{wrapStrategy="WRAP",verticalAlignment="TOP",textFormat=new{fontSize=11}}},fields="userEnteredFormat"}}));
            requests.Add(JsonSerializer.SerializeToNode(new{repeatCell=new{range=new{sheetId=id,startRowIndex=0,endRowIndex=2},cell=new{userEnteredFormat=new{backgroundColor=new{red=.07,green=.16,blue=.27},textFormat=new{bold=true,foregroundColor=new{red=1,green=1,blue=1}}}},fields="userEnteredFormat.backgroundColor,userEnteredFormat.textFormat"}}));
            requests.Add(JsonSerializer.SerializeToNode(new{updateDimensionProperties=new{range=new{sheetId=id,dimension="COLUMNS",startIndex=0,endIndex=export.Rows[0].Length},properties=new{pixelSize=200},fields="pixelSize"}}));
            requests.Add(JsonSerializer.SerializeToNode(new{setBasicFilter=new{filter=new{range=new{sheetId=id,startRowIndex=1,endRowIndex=export.Rows.Length+1,startColumnIndex=0,endColumnIndex=export.Rows[0].Length}}}}));
        }
        return new(){["requests"]=requests};
    }
}
