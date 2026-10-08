using static CatPriceCalculator.Core.TrackingJson;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
namespace CatPriceCalculator.Core;

public sealed class UpsTrackingService
{
    public const string TokenEndpoint = "https://onlinetools.ups.com/security/v1/oauth/token";
    public const string TrackingEndpoint = "https://onlinetools.ups.com/api/track/v1/details/";
    private readonly OAuthTrackingClient<UpsConnectionSettings> oauth;
    public UpsTrackingService(HttpClient client, TrackingRequestBudget budget, Func<DateTimeOffset>? clock = null, Action<ApiLogEntry>? log = null)
    {
        oauth = new(new(client, budget, "UPS", clock, log), "UPS", s => s.IsValid && s.HasCredentials,
            s =>
            {
                var request = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint);
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes(s.ClientId + ":" + s.ClientSecret)));
                if (s.AccountNumber.Length > 0) request.Headers.Add("x-merchant-id", s.AccountNumber);
                request.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "client_credentials" });
                return request;
            }, (_, number) =>
            {
                var request = new HttpRequestMessage(HttpMethod.Get, TrackingEndpoint + Uri.EscapeDataString(number) + "?locale=en_US&returnSignature=false&returnPOD=false&returnMilestones=false");
                request.Headers.Add("transId", Guid.NewGuid().ToString("N")); request.Headers.Add("transactionSrc", "LinserHungary"); return request;
            }, Parse, number => number.Length is >= 7 and <= 34 && number.All(char.IsAsciiLetterOrDigit));
    }
    public void ForgetToken() => oauth.ForgetToken();
    public Task<ParcelTrackingResult> FetchAsync(UpsConnectionSettings settings, string number, CancellationToken token = default) => oauth.FetchAsync(settings, number, token);
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
