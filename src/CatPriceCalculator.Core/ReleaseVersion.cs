using System.Globalization;
using System.Reflection;
namespace CatPriceCalculator.Core;
public sealed record ReleaseVersion : IComparable<ReleaseVersion>
{
    public Version Version { get; }
    public int? BetaNumber { get; }
    public bool IsBeta => BetaNumber.HasValue;
    public ReleaseVersion(Version version, int? betaNumber = null)
    {
        if (betaNumber is <= 0) throw new ArgumentOutOfRangeException(nameof(betaNumber));
        Version = new(version.Major, version.Minor, Math.Max(0, version.Build));
        BetaNumber = betaNumber;
    }
    public int CompareTo(ReleaseVersion? other)
    {
        if (other == null) return 1;
        int numeric = Version.CompareTo(other.Version);
        if (numeric != 0) return numeric;
        if (!BetaNumber.HasValue) return other.BetaNumber.HasValue ? 1 : 0;
        return other.BetaNumber.HasValue ? BetaNumber.Value.CompareTo(other.BetaNumber.Value) : -1;
    }
    public override string ToString() => Version.ToString(3) + (BetaNumber.HasValue ? "-beta." + BetaNumber.Value.ToString(CultureInfo.InvariantCulture) : "");
    public static bool TryParse(string? text, out ReleaseVersion result)
    {
        result = null!;
        if (string.IsNullOrWhiteSpace(text)) return false;
        if (text[0] == 'v') text = text[1..];
        text = text.Split('+')[0]; // SDK may append the source commit to informational version.
        string[] pieces = text.Split("-beta.", StringSplitOptions.None);
        if (pieces.Length > 2 || !Version.TryParse(pieces[0], out var version) || version.Revision >= 0) return false;
        int? beta = null;
        if (pieces.Length == 2)
        {
            if (!int.TryParse(pieces[1], NumberStyles.None, CultureInfo.InvariantCulture, out int number) || number <= 0 || pieces[1].StartsWith('0')) return false;
            beta = number;
        }
        result = new(version, beta);
        return true;
    }
    public static ReleaseVersion Parse(string text) => TryParse(text, out var result) ? result : throw new FormatException("Invalid release version.");
}
public static class BuildInfo
{
    public static ReleaseVersion Current { get; } = ReleaseVersion.Parse(typeof(BuildInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion);
    public static string UserAgent { get; } = "LinserHungary/" + Current;
}
