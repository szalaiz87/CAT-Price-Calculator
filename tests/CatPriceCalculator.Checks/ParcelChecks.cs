using CatPriceCalculator.Core;
public static class ParcelChecks
{
    public static void Run(Action<bool, string> check)
    {
        var now = new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);
        var pending = ParcelBook.Add("  1Z123456789  ", Courier.Ups, "  Alkatrész  ", now);
        check(pending.TrackingNumber == "1Z123456789" && pending.Note == "Alkatrész", "Parcel entry trims input and notes");
        check(pending.State == ParcelState.AwaitingConnection && pending.Location == null && pending.LastEventAt == null && pending.EstimatedDeliveryAt == null && pending.DeliveredAt == null && !pending.IsSample,
            "Offline parcel never invents tracking data or delivery dates");
        check(ParcelBook.HasDuplicate([pending], Courier.Ups, " 1z123456789 ") && !ParcelBook.HasDuplicate([pending], Courier.Dhl, "1Z123456789"),
            "Duplicates match normalized number within the same carrier only");
        foreach (var input in new[] { "", "   ", new string('a', 65) })
        {
            bool rejected = false;
            try { ParcelBook.Add(input, Courier.Dhl, "", now); } catch (ArgumentException) { rejected = true; }
            check(rejected, "Invalid parcel number rejected (length " + input.Length + ")");
        }
        var delivered = pending with { Id = Guid.NewGuid(), TrackingNumber = "DELIVERED-TEST", State = ParcelState.Delivered, DeliveredAt = now.AddHours(-48) };
        check(!ParcelBook.IsExpired(delivered, now.AddTicks(-1)) && ParcelBook.IsExpired(delivered, now), "Delivered parcel expires at exactly 48 elapsed hours");
        check(!ParcelBook.IsExpired(pending with { DeliveredAt = now.AddDays(-10) }, now) && !ParcelBook.IsExpired(delivered with { DeliveredAt = now.AddHours(1) }, now),
            "Active parcels and future delivery timestamps are not deleted");
        var dst = delivered with { DeliveredAt = new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.FromHours(2)) };
        check(!ParcelBook.IsExpired(dst, new DateTimeOffset(2026, 10, 26, 10, 59, 59, TimeSpan.FromHours(1))) &&
            ParcelBook.IsExpired(dst, new DateTimeOffset(2026, 10, 26, 11, 0, 0, TimeSpan.FromHours(1))), "Retention uses elapsed time across Hungarian daylight-saving change");
        var samples = ParcelBook.Samples(now);
        check(samples.Length == 6 && samples.All(p => p.IsSample && p.TrackingNumber.StartsWith("MINTA-")) && samples.Select(p => p.Carrier).Distinct().Count() == 4,
            "Clearly labeled samples cover all four carriers");
        check(samples.Count(p => p.State != ParcelState.Delivered) == 4 && samples.Count(p => p.State == ParcelState.Delivered) == 2 && samples.All(p => !ParcelBook.IsExpired(p, now)),
            "Initial preview has four active and two arrived parcels");
        var directory = Path.Combine(Path.GetTempPath(), "linser-parcels-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "parcels.json"); var store = new ParcelStore(path);
            check(store.Load().IsNew && !store.Load().Failed, "Only a missing parcel file requests initial samples");
            check(store.Save([pending, delivered]) && store.Load().Parcels.SequenceEqual(new[] { pending, delivered }), "Parcel data and absolute delivery time survive restart");
            var reloaded = store.Load().Parcels; reloaded.RemoveAll(p => ParcelBook.IsExpired(p, now));
            check(reloaded.SequenceEqual(new[] { pending }) && store.Save(reloaded) && store.Load().Parcels.SequenceEqual(new[] { pending }), "Expired parcels are deleted from persisted state while pending parcels remain");
            check(store.Save([]) && !store.Load().IsNew && store.Load().Parcels.Count == 0, "Empty saved list does not regenerate sample parcels");
            File.WriteAllText(path, "invalid json");
            check(store.Load().Failed && !store.Load().IsNew && File.ReadAllText(path) == "invalid json", "Corrupt parcel file is reported without overwriting or reseeding");
            store.Save([delivered with { DeliveredAt = null }]);
            check(store.Load().Failed, "Delivered records without a delivery timestamp are rejected");
            store.Save([pending with { Carrier = (Courier)99 }]);
            check(store.Load().Failed, "Unknown carrier in stored data is rejected safely");
            store.Save([pending, pending]);
            check(store.Load().Failed, "Duplicate persisted parcel identifiers are rejected");
            var blocker = Path.Combine(directory, "blocked"); File.WriteAllText(blocker, "not a directory");
            check(!new ParcelStore(Path.Combine(blocker, "parcels.json")).Save([pending]), "Parcel write failure is returned without losing current UI data");
        }
        finally { Directory.Delete(directory, true); }
    }
}
