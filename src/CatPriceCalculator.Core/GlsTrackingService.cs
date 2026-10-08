using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using static CatPriceCalculator.Core.TrackingJson;
namespace CatPriceCalculator.Core;

public sealed class GlsTrackingService
{
    public const string TrackingEndpoint = "https://api.mygls.hu/ParcelService.svc/json/GetParcelStatuses";
    private readonly ManualTrackingSession session;
    public GlsTrackingService(HttpClient client, TrackingRequestBudget budget, Func<DateTimeOffset>? clock = null, Action<ApiLogEntry>? log = null) => session = new(client, budget, "GLS", clock, log);
    public Task<ParcelTrackingResult> FetchAsync(GlsConnectionSettings settings, string number, CancellationToken token = default)
    {
        number = number?.Trim() ?? "";
        return session.RunAsync(number, () =>
        {
            if (!settings.IsValid || !settings.HasCredentials) throw new ParcelTrackingException(TrackingErrorCode.Configuration, "Add meg és mentsd a MyGLS API-hozzáférést a Beállítások / GLS lapon.");
            if (!long.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out long id) || id <= 0) throw new ParcelTrackingException(TrackingErrorCode.InvalidNumber, "A GLS csomagszám csak pozitív egész szám lehet.");
        }, async request =>
        {
            byte[] password = Encoding.UTF8.GetBytes(settings.Password);
            byte[] hash = SHA512.HashData(password);
            int[] hashArray = hash.Select(b => (int)b).ToArray();
            try
            {
                using var message = new HttpRequestMessage(HttpMethod.Post, TrackingEndpoint)
                {
                    Content = JsonContent.Create(new
                    {
                        Username = settings.Username, Password = hashArray,
                        ClientNumberList = settings.ClientNumber.Length > 0 ? new[] { int.Parse(settings.ClientNumber, CultureInfo.InvariantCulture) } : [],
                        WebshopEngine = "LinserHungary", ParcelNumber = long.Parse(number, CultureInfo.InvariantCulture), ReturnPOD = false, LanguageIsoCode = "HU"
                    }, options: JsonSerializerOptions.Default)
                };
                using var response = await request.SendAsync(message, settings.DailyLimit, "Követés");
                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) throw new ParcelTrackingException(TrackingErrorCode.Authentication, "A GLS elutasította a hozzáférést. Ellenőrizd az API-felhasználót és jogosultságait.");
                if (!response.IsSuccessStatusCode) throw new ParcelTrackingException(TrackingErrorCode.Offline, "A GLS jelenleg nem ad követési adatot. A korábbi adatok megmaradnak.");
                request.Stage = "Feldolgozás";
                using var document = await ReadAsync(response.Content, token);
                if (Array(document.RootElement, "GetParcelStatusErrors").Any(e => Number(e, "ErrorCode") == "31")) request.Block(session.Now().AddMinutes(5));
                return Parse(document.RootElement, number);
            }
            finally { CryptographicOperations.ZeroMemory(password); CryptographicOperations.ZeroMemory(hash); System.Array.Clear(hashArray); }
        }, token);
    }
    private static string? Timestamp(JsonElement item)
    {
        string? value = Text(item, "StatusDate");
        if (value == null) return null;
        if (value.StartsWith("/Date(", StringComparison.Ordinal) && value.EndsWith(")/", StringComparison.Ordinal))
        {
            var span = value.AsSpan(6, value.Length - 8);
            if (span.Length == 0) return null;
            int offset = span[1..].IndexOfAny('+', '-');
            if (offset >= 0)
            {
                var suffix = span[(offset + 1)..];
                if (suffix.Length != 5 || !suffix[1..].ToString().All(char.IsAsciiDigit)) return null;
                span = span[..(offset + 1)];
            }
            if (!long.TryParse(span, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long ms)) return null;
            try { var date = DateTimeOffset.FromUnixTimeMilliseconds(ms); return date.Year > 1 ? date.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) : null; }
            catch (ArgumentOutOfRangeException) { return null; }
        }
        return value.StartsWith("0001", StringComparison.Ordinal) ? null : value;
    }
    private static string? Location(JsonElement item)
    {
        string? city = Text(item, "DepotCity");
        if (!string.IsNullOrWhiteSpace(city)) return city;
        string? depot = Text(item, "DepotNumber");
        return string.IsNullOrWhiteSpace(depot) ? null : "GLS depó: " + depot;
    }
    public static ParcelTrackingResult Parse(JsonElement root, string number)
    {
        var errors = Array(root, "GetParcelStatusErrors");
        if (errors.Length > 0)
        {
            var codes = errors.Select(e => Number(e, "ErrorCode")).ToArray();
            var error = codes.Any(c => c is "1000" or "1001") ? TrackingErrorCode.Offline : codes.Any(c => c is "14" or "27") ? TrackingErrorCode.Authentication : codes.Contains("31") ? TrackingErrorCode.Quota : codes.Any(c => c is "5" or "15") ? TrackingErrorCode.PermissionDenied : codes.Any(c => c is "9" or "10" or "26") ? TrackingErrorCode.NotFound : TrackingErrorCode.InvalidResponse;
            string message = error switch
            {
                TrackingErrorCode.Authentication => "A GLS elutasította az API-felhasználót vagy az ügyfélszámot. Ellenőrizd a MyGLS API-jogosultságot.",
                TrackingErrorCode.PermissionDenied => "A MyGLS-felhasználónak nincs joga ehhez a csomaghoz. Beérkező, más által feladott csomagnál kérj jogosultságot a GLS-től.",
                TrackingErrorCode.NotFound => "A GLS még nem talál követési adatot ehhez a csomagszámhoz.",
                TrackingErrorCode.Quota => "A GLS túl gyakori lekérést jelzett. Várj legalább öt percet a következő frissítésig.",
                _ => "A GLS nem adott értelmezhető követési adatot. A korábbi adatok megmaradnak."
            };
            throw new ParcelTrackingException(error, message);
        }
        if (!long.TryParse(Number(root, "ParcelNumber"), out long returned) || !long.TryParse(number, out long expected) || returned != expected) throw new ParcelTrackingException(TrackingErrorCode.Ambiguous, "A GLS válaszában másik vagy hiányzó csomagszám szerepel. A korábbi adatok megmaradnak.");
        var statuses = Array(root, "ParcelStatusList");
        if (statuses.Length == 0) throw new ParcelTrackingException(TrackingErrorCode.NotFound, "A GLS még nem adott státuszt ehhez a csomaghoz.");
        var latest = statuses.OrderByDescending(s => TrackingTimestamp.SortKey(Timestamp(s))).First();
        string? code = Number(latest, "StatusCode");
        if (string.IsNullOrWhiteSpace(code)) throw new ParcelTrackingException(TrackingErrorCode.InvalidResponse, "A GLS válaszából hiányzik az aktuális státusz.");
        var state = code switch
        {
            "5" or "54" or "55" or "58" => ParcelState.Delivered,
            "4" or "32" => ParcelState.OutForDelivery,
            "51" or "52" => ParcelState.PreTransit,
            "60" or "61" or "62" or "66" or "67" => ParcelState.Customs,
            "1" or "2" or "3" or "6" or "7" or "8" or "9" or "10" or "22" or "24" or "26" or "27" or "41" or "46" or "47" or "53" or "56" or "59" or "64" or "65" => ParcelState.InTransit,
            "11" or "12" or "13" or "14" or "15" or "16" or "17" or "18" or "19" or "20" or "21" or "23" or "25" or "28" or "29" or "30" or "31" or "33" or "34" or "35" or "36" or "37" or "38" or "39" or "40" or "42" or "43" or "44" or "57" or "68" => ParcelState.Exception,
            _ => ParcelState.Unknown
        };
        var located = statuses.Where(s => Location(s) != null).OrderByDescending(s => TrackingTimestamp.SortKey(Timestamp(s))).FirstOrDefault();
        string? delivered = state == ParcelState.Delivered ? Timestamp(statuses.Where(s => Number(s, "StatusCode") is "5" or "54" or "55" or "58").OrderBy(s => TrackingTimestamp.SortKey(Timestamp(s))).FirstOrDefault()) : null;
        string detail = string.Join(" • ", new[] { Text(latest, "StatusDescription"), Text(latest, "StatusInfo") }.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct());
        return new(state, Location(located), Timestamp(located), delivered, detail.Length > 2000 ? detail[..2000] : detail, "GLS");
    }
    public static Parcel Apply(Parcel parcel, ParcelTrackingResult result, DateTimeOffset checkedAt) => ParcelTracking.Apply(parcel, result, checkedAt, Courier.Gls);
}
