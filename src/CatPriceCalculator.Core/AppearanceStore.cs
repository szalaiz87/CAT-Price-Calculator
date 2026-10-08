using System.Text.Json;
namespace CatPriceCalculator.Core;
public enum Appearance { Dark, Light }
public sealed class AppearanceStore(string path)
{
    private sealed record Preference(string Theme);
    public Appearance Load()
    {
        try { return JsonSerializer.Deserialize<Preference>(File.ReadAllText(path))?.Theme == "Light" ? Appearance.Light : Appearance.Dark; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or ArgumentException) { return Appearance.Dark; }
    }
    public bool Save(Appearance appearance)
    {
        try {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(new Preference(appearance.ToString())));
            File.Move(path + ".tmp", path, true); return true;
        } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return false; }
    }
}
