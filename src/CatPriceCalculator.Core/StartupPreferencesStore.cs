using System.Text.Json;
namespace CatPriceCalculator.Core;
public sealed record StartupPreferences(bool AlwaysOnTop = false, bool RefreshRates = false, bool AllowBetaUpdates = false);
public sealed class StartupPreferencesStore(string path)
{
    public StartupPreferences Load()
    {
        try { return JsonSerializer.Deserialize<StartupPreferences>(File.ReadAllText(path)) ?? new(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or ArgumentException) { return new(); }
    }
    public bool Save(StartupPreferences preferences)
    {
        try {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(preferences));
            File.Move(path + ".tmp", path, true); return true;
        } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return false; }
    }
}
