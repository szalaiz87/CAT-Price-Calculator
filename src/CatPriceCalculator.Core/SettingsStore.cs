using System.Text.Json;
namespace CatPriceCalculator.Core;
public sealed record RateSettings(decimal Rate, string? Source = null, DateOnly? Date = null, DateTimeOffset? RetrievedAt = null);
public sealed class SettingsStore(string path)
{
    public RateSettings? Load()
    {
        try { var value = JsonSerializer.Deserialize<RateSettings>(File.ReadAllText(path)); return value?.Rate > 0 ? value : null; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or ArgumentException) { return null; }
    }
    public bool Save(RateSettings value)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(value));
            File.Move(path + ".tmp", path, true);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return false; }
    }
}
