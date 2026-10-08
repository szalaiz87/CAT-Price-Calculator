using System.Text.Json;
namespace CatPriceCalculator.Core;

public enum Courier { Dhl, FedEx, Ups, Gls }
public enum ParcelState { AwaitingConnection, InTransit, OutForDelivery, Customs, Delivered, PreTransit, Exception, Unknown }
public sealed record Parcel(Guid Id, string TrackingNumber, Courier Carrier, string Note, ParcelState State,
    string? Location, DateTimeOffset? LastEventAt, DateTimeOffset? EstimatedDeliveryAt,
    DateTimeOffset? DeliveredAt, DateTimeOffset AddedAt, bool IsSample = false,
    string? StatusDetail = null, string? LastEventRawTimestamp = null, string? EstimatedDeliveryRawTimestamp = null,
    DateTimeOffset? LastCheckedAt = null, string? TrackingError = null, string? DhlService = null, bool RetentionFromObservation = false);

public static class ParcelBook
{
    public static readonly TimeSpan DeliveredRetention = TimeSpan.FromHours(48);
    public const int PageSize = 4;
    public static bool CanRefresh(Parcel parcel) => parcel.Carrier is (Courier.Dhl or Courier.Ups) && !parcel.IsSample && parcel.State != ParcelState.Delivered;
    public static Guid[] RefreshTargets(IEnumerable<Parcel> parcels, Guid? only = null) => parcels
        .Where(p => CanRefresh(p) && (!only.HasValue || p.Id == only.Value)).Select(p => p.Id).ToArray();
    public static bool IsExpired(Parcel parcel, DateTimeOffset now) => parcel.State == ParcelState.Delivered &&
        parcel.DeliveredAt.HasValue && now - parcel.DeliveredAt.Value >= DeliveredRetention;
    public static bool HasDuplicate(IEnumerable<Parcel> parcels, Courier carrier, string number) =>
        parcels.Any(p => p.Carrier == carrier && p.TrackingNumber.Equals(number.Trim(), StringComparison.OrdinalIgnoreCase));
    public static Parcel Add(string number, Courier carrier, string note, DateTimeOffset now)
    {
        number = number.Trim(); note = note.Trim();
        if (number.Length is < 1 or > 64 || note.Length > 160 || !Enum.IsDefined(carrier)) throw new ArgumentException("Invalid parcel input.");
        // A locally entered number has no invented location, event or delivery estimate.
        return new(Guid.NewGuid(), number, carrier, note, ParcelState.AwaitingConnection, null, null, null, null, now);
    }
    public static Parcel[] Samples(DateTimeOffset now) => [
        new(Guid.Parse("00000000-0000-0000-0000-000000000001"), "MINTA-DHL-001", Courier.Dhl, "CAT alkatrészek", ParcelState.InTransit, "Leipzig, DE", now.AddHours(-2), now.AddDays(1), null, now.AddDays(-3), true),
        new(Guid.Parse("00000000-0000-0000-0000-000000000002"), "MINTA-FEDEX-002", Courier.FedEx, "Sürgős rendelés", ParcelState.Customs, "Budapest, HU", now.AddHours(-4), now.AddDays(2), null, now.AddDays(-4), true),
        new(Guid.Parse("00000000-0000-0000-0000-000000000003"), "MINTA-UPS-003", Courier.Ups, "Tömítéskészlet", ParcelState.OutForDelivery, "Budapest, HU", now.AddMinutes(-40), now, null, now.AddDays(-5), true),
        new(Guid.Parse("00000000-0000-0000-0000-000000000004"), "MINTA-GLS-004", Courier.Gls, "Műhely • szerszámok", ParcelState.InTransit, "Győr, HU", now.AddHours(-6), null, null, now.AddDays(-6), true),
        new(Guid.Parse("00000000-0000-0000-0000-000000000005"), "MINTA-DHL-005", Courier.Dhl, "Raktárnak átadva", ParcelState.Delivered, "Budapest, HU", now.AddHours(-12), null, now.AddHours(-12), now.AddDays(-7), true),
        new(Guid.Parse("00000000-0000-0000-0000-000000000006"), "MINTA-GLS-006", Courier.Gls, "Csapágyak", ParcelState.Delivered, "Budapest, HU", now.AddHours(-30), null, now.AddHours(-30), now.AddDays(-8), true)
    ];
}

public sealed record ParcelLoadResult(List<Parcel> Parcels, bool IsNew = false, bool Failed = false);
public sealed class ParcelStore(string path)
{
    public ParcelLoadResult Load()
    {
        try
        {
            if (!File.Exists(path)) return new([], IsNew: true);
            var parcels = JsonSerializer.Deserialize<List<Parcel>>(File.ReadAllText(path));
            if (parcels == null || parcels.Any(p => p == null || p.Id == Guid.Empty ||
                !Enum.IsDefined(p.Carrier) || !Enum.IsDefined(p.State) || string.IsNullOrWhiteSpace(p.TrackingNumber) ||
                p.TrackingNumber.Length > 64 || p.Note == null || p.Note.Length > 160 ||
                p.State == ParcelState.Delivered && !p.DeliveredAt.HasValue) ||
                parcels.Select(p => p.Id).Distinct().Count() != parcels.Count) return new([], Failed: true);
            return new(parcels);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or ArgumentException) { return new([], Failed: true); }
    }
    public bool Save(IEnumerable<Parcel> parcels)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(parcels));
            File.Move(path + ".tmp", path, true);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return false; }
    }
}
