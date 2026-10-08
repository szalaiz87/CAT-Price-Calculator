using System.Security.Cryptography;
using System.Text.Json;
namespace CatPriceCalculator.Core;

public sealed record ProtectedSettingsLoad<T>(T Settings, bool Failed = false);
public sealed class ProtectedSettingsStore<T>(string path, ISecretProtector protector, Func<T> empty, Func<T, bool> valid) where T : class
{
    public ProtectedSettingsLoad<T> Load()
    {
        try
        {
            if (!File.Exists(path)) return new(empty());
            byte[] plain = protector.Unprotect(File.ReadAllBytes(path));
            try
            {
                var settings = JsonSerializer.Deserialize<T>(plain);
                return settings != null && valid(settings) ? new(settings) : new(empty(), true);
            }
            finally { CryptographicOperations.ZeroMemory(plain); }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or CryptographicException or ArgumentException) { return new(empty(), true); }
    }
    public bool Save(T settings)
    {
        if (!valid(settings)) return false;
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
