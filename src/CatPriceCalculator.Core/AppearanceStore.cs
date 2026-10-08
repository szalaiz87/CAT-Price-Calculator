using System.Text.Json;
namespace CatPriceCalculator.Core;
public enum Appearance { Dark, Light }
public sealed class AppearanceStore(string path)
{
    private sealed record Preference(string Theme);
    public Appearance Load()
    {
        try { return LocalJsonFile.Read<Preference>(path)?.Theme == "Light" ? Appearance.Light : Appearance.Dark; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or ArgumentException) { return Appearance.Dark; }
    }
    public bool Save(Appearance appearance)
    {
        return LocalJsonFile.Write(path, new Preference(appearance.ToString()));
    }
}
