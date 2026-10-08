using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
namespace CatPriceCalculator.Core;

// No timer/background requests: OAuth and tracking are both part of an explicit FetchAsync.
public sealed class UpsTrackingService(HttpClient client, TrackingRequestBudget budget,
    Func<DateTimeOffset>? clock = null, Action<ApiLogEntry>? log = null)
{
    public const string TokenEndpoint = "https://onlinetools.ups.com/security/v1/oauth/token";
    public const string TrackingEndpoint = "https://onlinetools.ups.com/api/track/v1/details/";
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Func<DateTimeOffset> now = clock ?? (() => DateTimeOffset.UtcNow);
    private UpsConnectionSettings? authenticatedSettings;
    private string? accessToken;
    private DateTimeOffset tokenExpires, blockedUntil;
    public void ForgetToken() { accessToken = null; authenticatedSettings = null; tokenExpires = default; }
    public async Task<ParcelTrackingResult> FetchAsync(UpsConnectionSettings settings, string number, CancellationToken token = default)
    {
        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        string reference = "…" + new string((number ?? "").Where(char.IsLetterOrDigit).TakeLast(4).ToArray());
        string stage = "Beállítás";
        bool acquired = false;
        void Trace(string message, bool error = false, int? status = null)
        {
            try { log?.Invoke(new(now(), stage, message, status, reference,
                (long)System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds, error, "UPS")); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
        async Task<HttpResponseMessage> Send(HttpRequestMessage request)
        {
            if (now() < blockedUntil) throw new ParcelTrackingException(TrackingErrorCode.Quota, "A UPS korlátozta a lekéréseket. Próbáld újra később.");
            await budget.ReserveAsync(settings.DailyLimit, token);
            request.Headers.UserAgent.ParseAdd(BuildInfo.UserAgent);
            var response = await client.SendAsync(request, token);
            Trace("UPS HTTP-válasz érkezett.", !response.IsSuccessStatusCode, (int)response.StatusCode);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                blockedUntil = response.Headers.RetryAfter?.Date ?? now() + (response.Headers.RetryAfter?.Delta ?? TimeSpan.FromMinutes(15));
                if (blockedUntil <= now()) blockedUntil = now().AddSeconds(5);
                response.Dispose();
                throw new ParcelTrackingException(TrackingErrorCode.Quota, "A UPS lekérdezési limitet jelzett. A UPS lekéréseit leállítottuk; próbáld később.");
            }
            return response;
        }
        async Task Authenticate()
        {
            stage = "OAuth";
            Trace("UPS hozzáférési token kérése a kézi lekéréshez.");
            using var request = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes(settings.ClientId + ":" + settings.ClientSecret)));
            if (settings.AccountNumber.Length > 0) request.Headers.Add("x-merchant-id", settings.AccountNumber);
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "client_credentials" });
            using var response = await Send(request);
            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                throw new ParcelTrackingException(TrackingErrorCode.Authentication, "A UPS hitelesítés sikertelen. Ellenőrizd a Client ID / Client Secret értékét és a portálon az alkalmazás hozzáférését.");
            if (!response.IsSuccessStatusCode) throw new ParcelTrackingException(TrackingErrorCode.Offline, "A UPS hitelesítési szolgáltatása most nem elérhető. Próbáld később.");
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
            var root = document.RootElement;
            string? value = Text(root, "access_token");
            var expires = Object(root, "expires_in");
            string? seconds = expires.ValueKind == JsonValueKind.String ? expires.GetString() : expires.ValueKind == JsonValueKind.Number ? expires.GetRawText() : null;
            if (string.IsNullOrEmpty(value) || value.Length > 16384 || value.Any(c => c <= 32 || c >= 127) ||
                !string.Equals(Text(root, "token_type"), "Bearer", StringComparison.OrdinalIgnoreCase) ||
                !int.TryParse(seconds, NumberStyles.None, CultureInfo.InvariantCulture, out int ttl) || ttl is <= 0 or > 2592000)
                throw new ParcelTrackingException(TrackingErrorCode.InvalidResponse, "A UPS nem adott értelmezhető hozzáférési tokent. A titkos értékeket nem naplózzuk.");
            accessToken = value; authenticatedSettings = settings;
            tokenExpires = now().AddSeconds(ttl - Math.Min(60, ttl / 2));
            Trace("UPS hitelesítés sikeres; token csak memóriában tárolva.");
        }
        try
        {
            Trace("Kézzel indított UPS lekérés.");
            if (!settings.IsValid || !settings.HasCredentials) throw new ParcelTrackingException(TrackingErrorCode.Configuration, "Előbb ments Client ID és Client Secret értékeket a Beállítások / UPS API lapon.");
            number = (number ?? "").Trim();
            if (number.Length is < 7 or > 34 || !number.All(char.IsAsciiLetterOrDigit))
                throw new ParcelTrackingException(TrackingErrorCode.InvalidNumber, "A UPS csomagszám 7–34 betűből és számból álljon.");
            stage = "Várakozás";
            await gate.WaitAsync(token); acquired = true;
            if (accessToken == null || authenticatedSettings != settings || now() >= tokenExpires) await Authenticate();
            for (int attempt = 0; attempt < 2; attempt++)
            {
                stage = "Követés";
                using var request = new HttpRequestMessage(HttpMethod.Get, TrackingEndpoint + Uri.EscapeDataString(number) + "?locale=en_US&returnSignature=false&returnPOD=false&returnMilestones=false");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                request.Headers.Add("transId", Guid.NewGuid().ToString("N"));
                request.Headers.Add("transactionSrc", "LinserHungary");
                Trace("GET kérés a UPS éles követési végpontjára.");
                using var response = await Send(request);
                if (response.StatusCode == HttpStatusCode.Unauthorized && attempt == 0)
                { ForgetToken(); await Authenticate(); continue; }
                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                    throw new ParcelTrackingException(TrackingErrorCode.Authentication, "A UPS nem engedélyezte a követést. Ellenőrizd a Tracking API engedélyezését az alkalmazásodnál.");
                if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
                    throw new ParcelTrackingException(TrackingErrorCode.NotFound, "A UPS nem találja ezt a csomagot. Ellenőrizd a csomagszámot; friss feladásnál próbáld később.");
                if (!response.IsSuccessStatusCode) throw new ParcelTrackingException(TrackingErrorCode.Offline, "A UPS követési szolgáltatása most nem elérhető. A korábbi adatok megmaradnak.");
                stage = "Feldolgozás";
                using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
                var result = Parse(document.RootElement, number);
                Trace("A UPS követési válasza sikeresen feldolgozva.");
                return result;
            }
            throw new ParcelTrackingException(TrackingErrorCode.Authentication, "A UPS hitelesítést nem sikerült megújítani.");
        }
        catch (ParcelTrackingException ex) { Trace(ex.Message, true); throw; }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        { const string message = "A UPS lekérése túllépte az időkorlátot. Próbáld később."; Trace(message, true); throw new ParcelTrackingException(TrackingErrorCode.Offline, message); }
        catch (OperationCanceledException) { Trace("A kézi UPS lekérés megszakítva."); throw; }
        catch (HttpRequestException) { const string message = "A UPS nem érhető el. Ellenőrizd az internetkapcsolatot; a korábbi adatok megmaradnak."; Trace(message, true); throw new ParcelTrackingException(TrackingErrorCode.Offline, message); }
        catch (JsonException) { const string message = "A UPS válasza nem értelmezhető. A korábbi követési adatok megmaradnak."; Trace(message, true); throw new ParcelTrackingException(TrackingErrorCode.InvalidResponse, message); }
        finally { if (acquired) gate.Release(); }
    }
    private static JsonElement Object(JsonElement element, string name) => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) ? value : default;
    private static string? Text(JsonElement element, string name) => Object(element, name) is var v && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    private static JsonElement[] Array(JsonElement element, string name) => Object(element, name) is var v && v.ValueKind == JsonValueKind.Array ? v.EnumerateArray().ToArray() : [];
    private static string? Location(JsonElement activity)
    {
        var address = Object(Object(activity, "location"), "address");
        return string.Join(", ", new[] { Text(address, "city"), Text(address, "stateProvince"), Text(address, "countryCode") ?? Text(address, "country") }.Where(s => !string.IsNullOrWhiteSpace(s))) is var value && value.Length > 0 ? value : null;
    }
    public static string? Timestamp(string? date, string? time = null, string? offset = null)
    {
        if (!DateOnly.TryParseExact(date, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)) return null;
        string value = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (time == null || time.Length is < 5 or > 6 || !TimeOnly.TryParseExact(time.PadLeft(6, '0'), "HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var hour)) return value;
        value += "T" + hour.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        if (offset == "Z" || offset != null && System.Text.RegularExpressions.Regex.IsMatch(offset, @"^[+-]\d{1,2}:\d{2}$") && TrackingTimestamp.Instant(value + offset).HasValue) value += offset;
        return value;
    }
    private static string? EventTimestamp(JsonElement activity)
    {
        string? gmt = Timestamp(Text(activity, "gmtDate"), Text(activity, "gmtTime"), "Z");
        return TrackingTimestamp.Instant(gmt).HasValue ? gmt : Timestamp(Text(activity, "date"), Text(activity, "time"), Text(activity, "gmtOffset"));
    }
    public static ParcelTrackingResult Parse(JsonElement root, string requestedNumber)
    {
        var response = Object(root, "trackResponse");
        if (Object(response, "shipment").ValueKind != JsonValueKind.Array)
            throw new ParcelTrackingException(TrackingErrorCode.InvalidResponse, "A UPS válaszából hiányzik a csomaglista.");
        var packages = Array(response, "shipment").SelectMany(s => Array(s, "package")).ToArray();
        if (packages.Length == 0) throw new ParcelTrackingException(TrackingErrorCode.NotFound, "A UPS még nem talál követési adatot ehhez a csomaghoz.");
        var matches = packages.Where(p => string.Equals(Text(p, "trackingNumber"), requestedNumber, StringComparison.OrdinalIgnoreCase)).ToArray();
        var package = matches.Length == 1 ? matches[0] : packages.Length == 1 && matches.Length == 0 ? packages[0] : throw new ParcelTrackingException(TrackingErrorCode.Ambiguous, "Több UPS csomag tartozik a számhoz. Add meg az egyedi csomagkövetési számot.");
        // Official API order is newest first. Preserve each scan's location/time pair.
        var activities = Array(package, "activity");
        var status = Object(package, "currentStatus");
        if (Text(status, "type") == null && activities.Length > 0) status = Object(activities[0], "status");
        string? type = Text(status, "type");
        if (type == null && Text(status, "code") == null && Text(status, "statusCode") == null)
            throw new ParcelTrackingException(TrackingErrorCode.InvalidResponse, "A UPS válaszából hiányzik az aktuális státusz.");
        var state = type == "I" && Text(status, "code") == "OT" ? ParcelState.OutForDelivery : type switch { "D" => ParcelState.Delivered, "I" or "P" => ParcelState.InTransit, "O" => ParcelState.OutForDelivery, "M" => ParcelState.PreTransit, "X" => ParcelState.Exception, _ => ParcelState.Unknown };
        var located = activities.FirstOrDefault(a => Location(a) != null);
        var delivered = activities.LastOrDefault(a => Text(Object(a, "status"), "type") == "D");
        string? eta = null;
        if (state == ParcelState.Delivered)
        {
            eta = EventTimestamp(delivered);
            if (eta == null)
            {
                var date = Array(package, "deliveryDate").LastOrDefault(d => Text(d, "type") == "DEL");
                var time = Object(package, "deliveryTime");
                eta = Timestamp(Text(date, "date"), Text(time, "type") == "DEL" ? Text(time, "endTime") : null);
            }
        }
        else
        {
            var dates = Array(package, "deliveryDate");
            var scheduled = dates.LastOrDefault(d => Text(d, "type") == "RDD");
            if (scheduled.ValueKind == JsonValueKind.Undefined) scheduled = dates.LastOrDefault(d => Text(d, "type") == "SDD");
            // Delivery windows have no zone. Show date only rather than inventing a precise instant.
            eta = Timestamp(Text(scheduled, "date"));
        }
        var current = Object(package, "currentStatus");
        string detail = string.Join(" • ", new[] { Text(current, "description"), Text(current, "simplifiedTextDescription"), Text(status, "description") }.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct());
        return new(state, Location(located), EventTimestamp(located), eta, detail.Length > 2000 ? detail[..2000] : detail, "UPS");
    }
    public static Parcel Apply(Parcel parcel, ParcelTrackingResult result, DateTimeOffset checkedAt) => ParcelTracking.Apply(parcel, result, checkedAt, Courier.Ups);
}
