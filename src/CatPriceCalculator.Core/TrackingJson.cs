using System.Text.Json;
namespace CatPriceCalculator.Core;
internal static class TrackingJson
{
    public static JsonElement Object(JsonElement element, string name) => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) ? value : default;
    public static string? Text(JsonElement element, string name) => Object(element, name) is var v && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    public static JsonElement[] Array(JsonElement element, string name) => Object(element, name) is var v && v.ValueKind == JsonValueKind.Array ? v.EnumerateArray().ToArray() : [];
    public static string? Number(JsonElement element, string name) => Object(element, name) is var v && v.ValueKind == JsonValueKind.Number ? v.GetRawText() : Text(element, name);
    public static async Task<JsonDocument> ReadAsync(HttpContent content, CancellationToken token)
    {
        using var stream = await content.ReadAsStreamAsync(token);
        return await JsonDocument.ParseAsync(stream, cancellationToken: token);
    }
}
