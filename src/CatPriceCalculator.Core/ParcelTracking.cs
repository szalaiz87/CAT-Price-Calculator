using System.Globalization;
using System.Text.RegularExpressions;
namespace CatPriceCalculator.Core;

public enum TrackingErrorCode { Configuration, Authentication, NotFound, Ambiguous, Quota, Offline, InvalidResponse, InvalidNumber, PermissionDenied }
public sealed class ParcelTrackingException(TrackingErrorCode code, string message) : Exception(message)
{
    public TrackingErrorCode Code { get; } = code;
    public bool StopsBatch => Code is not (TrackingErrorCode.NotFound or TrackingErrorCode.Ambiguous or TrackingErrorCode.InvalidNumber or TrackingErrorCode.PermissionDenied);
}
public sealed record ParcelTrackingResult(ParcelState State, string? Location, string? EventTimestamp,
    string? DeliveryTimestamp, string StatusDetail, string? Service);

public static class TrackingTimestamp
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

public static class ParcelTracking
{
    public static Parcel Apply(Parcel parcel, ParcelTrackingResult result, DateTimeOffset checkedAt, Courier carrier)
    {
        if (parcel.Carrier != carrier || !Enum.IsDefined(carrier) || parcel.IsSample) throw new ArgumentException("Only real parcels from the matching supported courier can be updated.");
        if (parcel.State == ParcelState.Delivered && result.State != ParcelState.Delivered) return parcel with { LastCheckedAt = checkedAt, TrackingError = null };
        var delivered = result.State == ParcelState.Delivered ? parcel.DeliveredAt ?? TrackingTimestamp.Instant(result.DeliveryTimestamp) ?? checkedAt : parcel.DeliveredAt;
        bool newLocation = result.Location != null || parcel.Location == null;
        // Keep a confirmed delivery monotonic even if a provider temporarily returns an older scan.
        return parcel with {
            State = parcel.State == ParcelState.Delivered ? ParcelState.Delivered : result.State,
            Location = result.Location ?? parcel.Location, LastEventAt = newLocation ? TrackingTimestamp.Instant(result.EventTimestamp) : parcel.LastEventAt,
            LastEventRawTimestamp = newLocation ? result.EventTimestamp : parcel.LastEventRawTimestamp, EstimatedDeliveryAt = result.State == ParcelState.Delivered ? null : TrackingTimestamp.Instant(result.DeliveryTimestamp),
            EstimatedDeliveryRawTimestamp = result.DeliveryTimestamp, DeliveredAt = delivered, LastCheckedAt = checkedAt,
            StatusDetail = result.StatusDetail, TrackingError = null, DhlService = result.Service,
            RetentionFromObservation = parcel.DeliveredAt.HasValue ? parcel.RetentionFromObservation : result.State == ParcelState.Delivered && TrackingTimestamp.Instant(result.DeliveryTimestamp) == null
        };
    }
}
