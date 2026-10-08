using System.Text.Json;
namespace CatPriceCalculator.Core;

// Existing UTF-8 JSON formats, streamed directly to disk without an intermediate UTF-16 string.
internal static class LocalJsonFile
{
    public static T? Read<T>(string path)
    {
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<T>(stream);
    }
    public static bool Write<T>(string path, T value)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using (var stream = File.Create(path + ".tmp")) JsonSerializer.Serialize(stream, value);
            File.Move(path + ".tmp", path, true);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return false; }
    }
}
