using System.Net.Http.Json;
using System.Text.Json;
using static CatPriceCalculator.Core.TrackingJson;
namespace CatPriceCalculator.Core;

public sealed class FedExTrackingService
{
    public const string TokenEndpoint = "https://apis.fedex.com/oauth/token";
    public const string TrackingEndpoint = "https://apis.fedex.com/track/v1/trackingnumbers";
    private readonly OAuthTrackingClient<FedExConnectionSettings> oauth;
    public FedExTrackingService(HttpClient client, TrackingRequestBudget budget, Func<DateTimeOffset>? clock = null, Action<ApiLogEntry>? log = null)
    {
        oauth = new(new(client, budget, "FedEx", clock, log), "FedEx", s => s.IsValid && s.HasCredentials,
            s => new(HttpMethod.Post, TokenEndpoint) { Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "client_credentials", ["client_id"] = s.ClientId, ["client_secret"] = s.ClientSecret }) },
            (_, number) =>
            {
                var request = new HttpRequestMessage(HttpMethod.Post, TrackingEndpoint) { Content = JsonContent.Create(new { includeDetailedScans = true, trackingInfo = new[] { new { trackingNumberInfo = new { trackingNumber = number } } } }) };
                request.Headers.Add("x-locale", "en_US"); request.Headers.Add("x-customer-transaction-id", Guid.NewGuid().ToString("N")); return request;
            }, Parse, number => number.Length is >= 7 and <= 34 && number.All(char.IsAsciiLetterOrDigit));
    }
    public void ForgetToken() => oauth.ForgetToken();
    public Task<ParcelTrackingResult> FetchAsync(FedExConnectionSettings settings, string number, CancellationToken token = default) => oauth.FetchAsync(settings, number, token);
    private static string? Location(JsonElement scan)
    {
        var address = Object(scan, "scanLocation");
        string value = string.Join(", ", new[] { Text(address, "city"), Text(address, "stateOrProvinceCode"), Text(address, "countryCode") }.Where(s => !string.IsNullOrWhiteSpace(s)));
        return value.Length > 0 ? value : null;
    }
    public static ParcelTrackingResult Parse(JsonElement root, string number)
    {
        var complete = Object(Object(root, "output"), "completeTrackResults");
        if (complete.ValueKind != JsonValueKind.Array) throw new ParcelTrackingException(TrackingErrorCode.InvalidResponse, "A FedEx válaszából hiányzik a csomaglista.");
        var groups = complete.EnumerateArray().Where(g => string.Equals(Text(g, "trackingNumber"), number, StringComparison.OrdinalIgnoreCase)).ToArray();
        var results = groups.SelectMany(g => Array(g, "trackResults")).ToArray();
        if (results.Length == 0) throw new ParcelTrackingException(TrackingErrorCode.NotFound, "A FedEx még nem talál követési adatot ehhez a csomaghoz.");
        if (results.Length != 1) throw new ParcelTrackingException(TrackingErrorCode.Ambiguous, "Több FedEx feladás tartozik a számhoz. Egyedi követési szám szükséges; a régi adatok megmaradnak.");
        var result = results[0]; var error = Object(result, "error");
        if (Text(error, "code") != null)
            throw new ParcelTrackingException(Text(error, "code") is "TRACKING.DATA.NOTFOUND" or "TRACKING.TRACKINGNUMBER.INVALID" ? TrackingErrorCode.NotFound : TrackingErrorCode.InvalidResponse, "A FedEx ehhez a számhoz nem adott követési adatot. Ellenőrizd a számot és a projekt hozzáférését.");
        string? returned = Text(Object(result, "trackingNumberInfo"), "trackingNumber");
        if (returned != null && !returned.Equals(number, StringComparison.OrdinalIgnoreCase)) throw new ParcelTrackingException(TrackingErrorCode.Ambiguous, "A FedEx másik csomag követési adatait adta vissza. A korábbi adatok megmaradnak.");
        var status = Object(result, "latestStatusDetail");
        string? code = Text(status, "derivedCode") ?? Text(status, "code");
        if (string.IsNullOrWhiteSpace(code)) throw new ParcelTrackingException(TrackingErrorCode.InvalidResponse, "A FedEx válaszából hiányzik az aktuális státusz.");
        var state = code switch { "DL" => ParcelState.Delivered, "OD" => ParcelState.OutForDelivery, "OC" => ParcelState.PreTransit, "IT" or "PU" or "DP" or "AR" or "AF" or "AP" or "CC" => ParcelState.InTransit, "CP" or "CD" => ParcelState.Customs, "DE" or "SE" or "CA" or "RS" => ParcelState.Exception, _ => ParcelState.Unknown };
        var scans = Array(result, "scanEvents");
        var located = scans.Where(s => Location(s) != null).OrderByDescending(s => TrackingTimestamp.SortKey(Text(s, "date"))).FirstOrDefault();
        string? location = Location(located) ?? Location(status);
        var dates = Array(result, "dateAndTimes");
        string? GetDate(string type) => Text(dates.FirstOrDefault(d => Text(d, "type") == type), "dateTime");
        string? delivery = state == ParcelState.Delivered ? GetDate("ACTUAL_DELIVERY") ?? Text(scans.Where(s => (Text(s, "derivedStatusCode") ?? Text(s, "eventType")) == "DL").OrderBy(s => TrackingTimestamp.SortKey(Text(s, "date"))).FirstOrDefault(), "date") : GetDate("ESTIMATED_DELIVERY") ?? GetDate("COMMITMENT") ?? GetDate("APPOINTMENT_DELIVERY");
        string detail = string.Join(" • ", new[] { Text(status, "statusByLocale"), Text(status, "description"), Text(located, "exceptionDescription") }.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct());
        return new(state, location, Text(located, "date"), delivery, detail.Length > 2000 ? detail[..2000] : detail, "FedEx");
    }
    public static Parcel Apply(Parcel parcel, ParcelTrackingResult result, DateTimeOffset checkedAt) => ParcelTracking.Apply(parcel, result, checkedAt, Courier.FedEx);
}
