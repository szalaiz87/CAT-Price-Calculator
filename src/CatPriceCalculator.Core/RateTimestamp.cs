namespace CatPriceCalculator.Core;
public static class RateTimestamp
{
    private static readonly TimeZoneInfo HungarianZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Budapest");
    public static string Format(DateTimeOffset? timestamp)
    {
        if (timestamp is null) return "korábban nem rögzített";
        return TimeZoneInfo.ConvertTime(timestamp.Value, HungarianZone).ToString("yyyy.MM.dd. HH:mm:ss", PriceCalculator.Hungarian) + " (magyar idő)";
    }
}
