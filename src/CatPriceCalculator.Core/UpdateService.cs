using System.Security.Cryptography;
using System.Buffers;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.IO.Compression;
namespace CatPriceCalculator.Core;
public sealed record UpdateAsset(string Name, string Url, long Size, string? Digest);
public sealed record UpdateRelease(Version Version, string PageUrl, IReadOnlyList<UpdateAsset> Assets, int? BetaNumber = null)
{
    public ReleaseVersion Identifier => new(Version, BetaNumber);
    public string DisplayVersion => Identifier.ToString();
    public bool IsBeta => BetaNumber.HasValue;
}
public sealed class UpdateService(HttpClient client)
{
    public const string LatestEndpoint = "https://api.github.com/repos/szalaiz87/CAT-Price-Calculator/releases/latest";
    public const string ReleasesEndpoint = "https://api.github.com/repos/szalaiz87/CAT-Price-Calculator/releases?per_page=100&page=";
    // Existing stable clients keep the same method and endpoint.
    public Task<UpdateRelease?> CheckAsync(Version installedVersion, CancellationToken token = default) =>
        CheckAsync(new ReleaseVersion(installedVersion), false, token);
    public async Task<UpdateRelease?> CheckAsync(ReleaseVersion installedVersion, bool allowBeta, CancellationToken token = default)
    {
        var release = allowBeta ? await GetLatestEligibleAsync(token) : await GetLatestStableAsync(token);
        return release != null && release.Identifier.CompareTo(installedVersion) > 0 ? release : null;
    }
    // This deliberately ignores installed version: explicit beta rollback may be a downgrade.
    public async Task<UpdateRelease?> GetLatestStableAsync(CancellationToken token = default)
    {
        using var json = await ReadMetadataAsync(LatestEndpoint, token);
        return json == null ? null : ParseRelease(json.RootElement, false);
    }
    private async Task<UpdateRelease?> GetLatestEligibleAsync(CancellationToken token)
    {
        UpdateRelease? latest = null;
        for (int page = 1; ; page++)
        {
            using var json = await ReadMetadataAsync(ReleasesEndpoint + page, token);
            if (json == null) break;
            if (json.RootElement.ValueKind != JsonValueKind.Array) throw new InvalidDataException("Invalid release list.");
            foreach (var item in json.RootElement.EnumerateArray())
            {
                var candidate = ParseRelease(item, true);
                if (candidate != null && (latest == null || candidate.Identifier.CompareTo(latest.Identifier) > 0)) latest = candidate;
            }
            if (json.RootElement.GetArrayLength() < 100) break;
        }
        return latest;
    }
    private async Task<JsonDocument?> ReadMetadataAsync(string endpoint, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        request.Headers.UserAgent.ParseAdd(BuildInfo.UserAgent);
        using var response = await client.SendAsync(request, token);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
    }
    private static UpdateRelease? ParseRelease(JsonElement root, bool allowBeta)
    {
        if (root.GetProperty("draft").GetBoolean()) return null;
        bool prerelease = root.GetProperty("prerelease").GetBoolean();
        if (prerelease && !allowBeta) return null;
        if (!ReleaseVersion.TryParse(root.GetProperty("tag_name").GetString(), out var version)) return null;
        // A beta tag must also be a GitHub prerelease; unsupported preview formats are excluded.
        if (version.IsBeta != prerelease) return null;
        var assets = root.GetProperty("assets").EnumerateArray().Select(a => new UpdateAsset(a.GetProperty("name").GetString()!, a.GetProperty("browser_download_url").GetString()!, a.GetProperty("size").GetInt64(), a.TryGetProperty("digest", out var digest) ? digest.GetString() : null)).ToArray();
        return new(version.Version, root.GetProperty("html_url").GetString()!, assets, version.BetaNumber);
    }
    public async Task<string> PrepareAsync(UpdateRelease release, string stagingDirectory, IProgress<int>? progress = null, CancellationToken token = default)
    {
        Directory.CreateDirectory(stagingDirectory);
        var sums = release.Assets.Single(a => a.Name == "SHA256SUMS.txt");
        var checksumText = System.Text.Encoding.UTF8.GetString(await DownloadAsync(sums, token));
        var line = checksumText.Split('\n').Select(l => l.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Single(l => l.Length == 2 && l[1] == "CAT-Price-Calculator-Windows-x64.zip");
        if (!Regex.IsMatch(line[0], "\\A[0-9a-fA-F]{64}\\z")) throw new InvalidDataException("Invalid archive checksum.");
        var parts = release.Assets.Where(a => Regex.IsMatch(a.Name, "\\ACAT-Windows-x64\\.zip\\.part[0-9]{2}\\z")).OrderBy(a => a.Name, StringComparer.Ordinal).ToArray();
        if (parts.Length == 0 || parts.Length > 99) throw new InvalidDataException("Missing update parts.");
        var archivePath = Path.Combine(stagingDirectory, "CAT.zip");
        await using (var archive = File.Create(archivePath))
        {
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Name != $"CAT-Windows-x64.zip.part{i + 1:00}") throw new InvalidDataException("Incomplete update.");
                await DownloadToAsync(parts[i], archive, token);
                progress?.Report((i + 1) * 100 / parts.Length);
            }
        }
        await using (var archive = File.OpenRead(archivePath))
        {
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(archive, token));
            if (!hash.Equals(line[0], StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Update checksum mismatch.");
        }
        using var zip = ZipFile.OpenRead(archivePath);
        var exe = zip.GetEntry("CAT-Price-Calculator.exe") ?? throw new InvalidDataException("Missing executable.");
        var executable = Path.Combine(stagingDirectory, "CAT-Price-Calculator.exe");
        exe.ExtractToFile(executable, true);
        return executable;
    }
    private async Task<byte[]> DownloadAsync(UpdateAsset asset, CancellationToken token)
    {
        using var output = new MemoryStream();
        await DownloadToAsync(asset, output, token);
        return output.ToArray();
    }
    private async Task DownloadToAsync(UpdateAsset asset, Stream output, CancellationToken token)
    {
        var uri = new Uri(asset.Url);
        if (uri.Scheme != "https" || uri.Host != "github.com" || !uri.AbsolutePath.StartsWith("/szalaiz87/CAT-Price-Calculator/releases/download/", StringComparison.Ordinal)) throw new InvalidDataException("Untrusted update URL.");
        if (asset.Size <= 0 || asset.Size > 8 * 1024 * 1024) throw new InvalidDataException("Invalid update asset size.");
        // Headers-only completion avoids HttpClient buffering each 4 MiB part in memory.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(client.Timeout);
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.UserAgent.ParseAdd(BuildInfo.UserAgent);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        response.EnsureSuccessStatusCode();
        await using var input = await response.Content.ReadAsStreamAsync(timeout.Token);
        using var hash = asset.Digest != null ? IncrementalHash.CreateHash(HashAlgorithmName.SHA256) : null;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
        try
        {
            long received = 0;
            int count;
            while ((count = await input.ReadAsync(buffer.AsMemory(0, 64 * 1024), timeout.Token)) != 0)
            {
                received += count;
                if (received > asset.Size) throw new InvalidDataException("Invalid download size.");
                hash?.AppendData(buffer, 0, count);
                await output.WriteAsync(buffer.AsMemory(0, count), timeout.Token);
            }
            if (received != asset.Size) throw new InvalidDataException("Incomplete download.");
            if (hash != null && asset.Digest != "sha256:" + Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant()) throw new InvalidDataException("Asset checksum mismatch.");
        }
        finally { ArrayPool<byte>.Shared.Return(buffer); }
    }
}
