using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace CatPriceCalculator.Core;

public enum DhlError { Configuration, Authentication, NotFound, Ambiguous, Quota, Offline, InvalidResponse }
public sealed class DhlTrackingException(DhlError code, string message) : Exception(message)
{
    public DhlError Code { get; } = code;
    public bool StopsBatch => Code != DhlError.NotFound && Code != DhlError.Ambiguous;
}
public sealed record DhlTrackingResult(ParcelState State, string? Location, string? EventTimestamp,
    string? DeliveryTimestamp, string StatusDetail, string? Service);

public static class DhlTimestamp
{
    private static readonly TimeZoneInfo HungarianZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Budapest");
    public static DateTimeOffset? Instant(string? value) => value != null &&
        Regex.IsMatch(value, @"T\d{2}:\d{2}(?::\d{2}(?:\.\d+)?)?(?:Z|[+-]\d{1,2}:\d{2})$") &&
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var timestamp) ? timestamp : null;
    public static string Date(string? value)
    {
        var instant = Instant(value);
        if (instant.HasValue) return TimeZoneInfo.ConvertTime(instant.Value, HungarianZone).ToString("yyyy.MM.dd.", PriceCalculator.Hungarian);
        return value?.Length >= 10 && DateOnly.TryParseExact(value[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date.ToString("yyyy.MM.dd.") : "Még nincs adat";
    }
    public static string Time(string? value)
    {
        var instant = Instant(value);
        if (instant.HasValue) return TimeZoneInfo.ConvertTime(instant.Value, HungarianZone).ToString("HH:mm", PriceCalculator.Hungarian);
        return value != null && DateTime.TryParseExact(value, ["yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF", "yyyy-MM-dd'T'HH:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var local) ? local.ToString("HH:mm") + "*" : "";
    }
    internal static long SortKey(string? value) => Instant(value)?.UtcTicks ??
        (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local) ? local.Ticks : 0);
}

public sealed class DhlTrackingService(HttpClient client, DhlRequestBudget budget, Func<DateTimeOffset>? clock = null, Action<DhlLogEntry>? log = null)
{
    public const string Endpoint = "https://api-eu.dhl.com/track/shipments";
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Func<DateTimeOffset> now = clock ?? (() => DateTimeOffset.UtcNow);
    private DateTimeOffset blockedUntil;
    public async Task<DhlTrackingResult> FetchAsync(DhlConnectionSettings settings, string number, CancellationToken token = default)
    {
        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        string reference = "…" + new string((number ?? "").Where(char.IsLetterOrDigit).TakeLast(4).ToArray());
        string stage = "Beállítás";
        bool acquired = false;
        void Trace(string message, bool error = false, int? status = null)
        {
            if (log == null) return;
            try { log(new(now(), stage, message, status, reference, (long)System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds, error)); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { /* Logging must not interrupt tracking. */ }
        }
        try
        {
            Trace("Kézzel indított DHL lekérés.");
            if (!settings.IsValid || !settings.HasKey) throw new DhlTrackingException(DhlError.Configuration, "Előbb ments egy saját DHL API-kulcsot a Beállítások / DHL API lapon.");
            number = (number ?? "").Trim();
            if (number.Length is < 1 or > 64) throw new DhlTrackingException(DhlError.Configuration, "Adj meg egy érvényes DHL csomagszámot.");
            stage = "Várakozás";
            await gate.WaitAsync(token); acquired = true;
            stage = "Keret";
            if (now() < blockedUntil) throw new DhlTrackingException(DhlError.Quota, "A DHL korlátozta a lekéréseket. Próbáld újra később.");
            await budget.ReserveAsync(settings.DailyLimit, token);
            string url = Endpoint + "?trackingNumber=" + Uri.EscapeDataString(number) + "&language=hu&requesterCountryCode=HU";
            if (settings.RecipientPostalCode.Length > 0) url += "&recipientPostalCode=" + Uri.EscapeDataString(settings.RecipientPostalCode);
            if (settings.Service.Length > 0) url += "&service=" + Uri.EscapeDataString(settings.Service);
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("DHL-API-Key", settings.ApiKey);
            request.Headers.UserAgent.ParseAdd(BuildInfo.UserAgent);
            stage = "HTTP";
            Trace("GET kérés a DHL európai követési végpontjára.");
            using var response = await client.SendAsync(request, token);
            Trace("DHL HTTP-válasz érkezett.", !response.IsSuccessStatusCode, (int)response.StatusCode);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                throw new DhlTrackingException(DhlError.Authentication, "A DHL nem engedélyezte a hozzáférést. Ellenőrizd a Consumer Key értékét és a Shipment Tracking – Unified jóváhagyását.");
            if (response.StatusCode == HttpStatusCode.NotFound) throw new DhlTrackingException(DhlError.NotFound, "A DHL még nem találja ezt a csomagot. Ellenőrizd a számot; friss feladásnál próbáld később.");
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                blockedUntil = response.Headers.RetryAfter?.Date ?? now() + (response.Headers.RetryAfter?.Delta ?? TimeSpan.FromMinutes(15));
                throw new DhlTrackingException(DhlError.Quota, "A DHL lekérdezési limitet jelzett. A többi lekérést leállítottuk; próbáld később.");
            }
            if (!response.IsSuccessStatusCode) throw new DhlTrackingException(DhlError.Offline, "A DHL szolgáltatás most nem elérhető. A korábbi követési adatok megmaradnak.");
            stage = "Feldolgozás";
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
            var result = Parse(document.RootElement, number);
            Trace("A DHL követési válasza sikeresen feldolgozva.");
            return result;
        }
        catch (DhlTrackingException ex) { Trace(ex.Message, true); throw; }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        { const string message = "A DHL lekérése túllépte az időkorlátot. Próbáld később."; Trace(message, true); throw new DhlTrackingException(DhlError.Offline, message); }
        catch (OperationCanceledException) { Trace("A kézi DHL lekérés megszakítva."); throw; }
        catch (HttpRequestException) { const string message = "A DHL nem érhető el. Ellenőrizd az internetkapcsolatot; a korábbi adatok megmaradnak."; Trace(message, true); throw new DhlTrackingException(DhlError.Offline, message); }
        catch (JsonException) { const string message = "A DHL válasza nem értelmezhető. A korábbi követési adatok megmaradnak."; Trace(message, true); throw new DhlTrackingException(DhlError.InvalidResponse, message); }
        finally { if (acquired) gate.Release(); }
    }
    private static string? Text(JsonElement element, string name) => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static JsonElement Object(JsonElement element, string name) => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) ? value : default;
    private static string? Location(JsonElement element)
    {
        var address = Object(Object(element, "location"), "address");
        var city = Text(address, "addressLocality"); var country = Text(address, "countryCode");
        // Never substitute destination for last known scan location.
        return string.IsNullOrWhiteSpace(city) ? country : string.IsNullOrWhiteSpace(country) || city.EndsWith(", " + country, StringComparison.OrdinalIgnoreCase) ? city : city + ", " + country;
    }
    public static DhlTrackingResult Parse(JsonElement root, string requestedNumber)
    {
        var shipments = Object(root, "shipments");
        if (shipments.ValueKind != JsonValueKind.Array) throw new DhlTrackingException(DhlError.InvalidResponse, "A DHL válaszából hiányzik a csomaglista.");
        var list = shipments.EnumerateArray().ToArray();
        if (list.Length == 0) throw new DhlTrackingException(DhlError.NotFound, "A DHL még nem találja ezt a csomagot.");
        var matches = list.Where(s => string.Equals(Text(s, "id"), requestedNumber, StringComparison.OrdinalIgnoreCase)).ToArray();
        var shipment = list.Length == 1 ? list[0] : matches.Length == 1 ? matches[0] : throw new DhlTrackingException(DhlError.Ambiguous, "Több DHL csomag tartozik a számhoz. A DHL API beállításoknál szűkítsd a szolgáltatást.");
        var status = Object(shipment, "status");
        string? code = Text(status, "statusCode");
        if (code == null) throw new DhlTrackingException(DhlError.InvalidResponse, "A DHL válaszából hiányzik az aktuális státusz.");
        var state = code switch { "delivered" => ParcelState.Delivered, "transit" => ParcelState.InTransit, "pre-transit" => ParcelState.PreTransit, "failure" => ParcelState.Exception, _ => ParcelState.Unknown };
        var events = Object(shipment, "events");
        var scans = events.ValueKind == JsonValueKind.Array ? events.EnumerateArray().ToArray() : [];
        var located = scans.Where(s => !string.IsNullOrWhiteSpace(Location(s))).OrderByDescending(s => DhlTimestamp.SortKey(Text(s, "timestamp"))).FirstOrDefault();
        var locationSource = !string.IsNullOrWhiteSpace(Location(status)) ? status : located;
        string? eventTimestamp = Text(locationSource, "timestamp");
        string? delivery = Text(shipment, "estimatedTimeOfDelivery") ?? Text(shipment, "agreedDeliveryDate") ?? Text(Object(shipment, "estimatedDeliveryTimeFrame"), "estimatedThrough");
        string detail = string.Join(" • ", new[] { Text(status, "status"), Text(status, "statusDetailed"), Text(status, "description"), Text(status, "remark"), Text(status, "nextSteps") }.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct());
        var deliveredEvent = scans.Where(s => Text(s, "statusCode") == "delivered" && Text(s, "timestamp") != null).OrderBy(s => DhlTimestamp.SortKey(Text(s, "timestamp"))).FirstOrDefault();
        return new(state, Location(locationSource), eventTimestamp ?? Text(status, "timestamp"), state == ParcelState.Delivered ? Text(deliveredEvent, "timestamp") ?? Text(status, "timestamp") : delivery, detail.Length > 2000 ? detail[..2000] : detail, Text(shipment, "service"));
    }
    public static Parcel Apply(Parcel parcel, DhlTrackingResult result, DateTimeOffset checkedAt)
    {
        if (parcel.Carrier != Courier.Dhl || parcel.IsSample) throw new ArgumentException("Only real DHL parcels can be updated.");
        if (parcel.State == ParcelState.Delivered && result.State != ParcelState.Delivered) return parcel with { LastCheckedAt = checkedAt, TrackingError = null };
        var delivered = result.State == ParcelState.Delivered ? parcel.DeliveredAt ?? DhlTimestamp.Instant(result.DeliveryTimestamp) ?? checkedAt : parcel.DeliveredAt;
        bool newLocation = result.Location != null || parcel.Location == null;
        // Keep a confirmed delivery monotonic even if a provider temporarily returns an older scan.
        return parcel with {
            State = parcel.State == ParcelState.Delivered ? ParcelState.Delivered : result.State,
            Location = result.Location ?? parcel.Location, LastEventAt = newLocation ? DhlTimestamp.Instant(result.EventTimestamp) : parcel.LastEventAt,
            LastEventRawTimestamp = newLocation ? result.EventTimestamp : parcel.LastEventRawTimestamp, EstimatedDeliveryAt = result.State == ParcelState.Delivered ? null : DhlTimestamp.Instant(result.DeliveryTimestamp),
            EstimatedDeliveryRawTimestamp = result.DeliveryTimestamp, DeliveredAt = delivered, LastCheckedAt = checkedAt,
            StatusDetail = result.StatusDetail, TrackingError = null, DhlService = result.Service,
            RetentionFromObservation = parcel.DeliveredAt.HasValue ? parcel.RetentionFromObservation : result.State == ParcelState.Delivered && DhlTimestamp.Instant(result.DeliveryTimestamp) == null
        };
    }
}
