using System.Text.Json;
namespace CatPriceCalculator.Core;
public sealed record RateSettings(decimal Rate, string? Source = null, DateOnly? Date = null, DateTimeOffset? RetrievedAt = null);
public sealed class SettingsStore(string path)
{
    public RateSettings? Load()
    {
        try { var value = LocalJsonFile.Read<RateSettings>(path); return value?.Rate > 0 ? value : null; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or ArgumentException) { return null; }
    }
    public bool Save(RateSettings value)
    {
        return LocalJsonFile.Write(path, value);
    }
}
