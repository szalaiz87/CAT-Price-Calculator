using System.Text.Json;
namespace CatPriceCalculator.Core;
public sealed record StartupPreferences(bool AlwaysOnTop = false, bool RefreshRates = false, bool AllowBetaUpdates = false);
public sealed class StartupPreferencesStore(string path)
{
    public StartupPreferences Load()
    {
        try { return LocalJsonFile.Read<StartupPreferences>(path) ?? new(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or ArgumentException) { return new(); }
    }
    public bool Save(StartupPreferences preferences)
    {
        return LocalJsonFile.Write(path, preferences);
    }
}
