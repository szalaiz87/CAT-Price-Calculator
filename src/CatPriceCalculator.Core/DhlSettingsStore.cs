using System.Security.Cryptography;
using System.Text.Json;
namespace CatPriceCalculator.Core;

public sealed record DhlConnectionSettings(string ApiKey = "", string RecipientPostalCode = "", string Service = "", int DailyLimit = 250)
{
    public static readonly string[] Services = ["", "express", "parcel-de", "parcel-nl", "parcel-pl", "parcel-uk", "ecommerce", "ecommerce-apac", "ecommerce-europe", "ecommerce-ppl", "ecommerce-iberia", "freight", "dgf", "dsc", "post-de", "post-international", "sameday", "svb"];
    public bool HasKey => !string.IsNullOrWhiteSpace(ApiKey);
    public override string ToString() => $"DHL connection: key configured={HasKey}, service={Service}, daily limit={DailyLimit}";
    public bool IsValid => ApiKey != null && RecipientPostalCode != null && Service != null && ApiKey.Length <= 512 && ApiKey.All(c => c > 32 && c < 127) &&
        !ApiKey.Equals("demo-key", StringComparison.OrdinalIgnoreCase) && RecipientPostalCode.Length <= 20 &&
        RecipientPostalCode.All(c => char.IsAsciiLetterOrDigit(c) || c is ' ' or '-') && Services.Contains(Service) && DailyLimit is >= 1 and <= 100000;
}
public interface ISecretProtector
{
    byte[] Protect(byte[] value);
    byte[] Unprotect(byte[] value);
}
public sealed record DhlSettingsLoad(DhlConnectionSettings Settings, bool Failed = false);
public sealed class DhlSettingsStore(string path, ISecretProtector protector)
{
    public DhlSettingsLoad Load()
    {
        try
        {
            if (!File.Exists(path)) return new(new());
            byte[] plain = protector.Unprotect(File.ReadAllBytes(path));
            try
            {
                var settings = JsonSerializer.Deserialize<DhlConnectionSettings>(plain);
                return settings != null && settings.IsValid ? new(settings) : new(new(), true);
            }
            finally { CryptographicOperations.ZeroMemory(plain); }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or CryptographicException or ArgumentException) { return new(new(), true); }
    }
    public bool Save(DhlConnectionSettings settings)
    {
        if (!settings.IsValid) return false;
        byte[] plain = JsonSerializer.SerializeToUtf8Bytes(settings);
        try
        {
            byte[] encrypted = protector.Protect(plain);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path + ".tmp", encrypted);
            File.Move(path + ".tmp", path, true);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or CryptographicException) { return false; }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }
}
