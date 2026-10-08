namespace CatPriceCalculator.Core;

public sealed record UpsConnectionSettings(string ClientId = "", string ClientSecret = "", string AccountNumber = "", int DailyLimit = 250)
{
    public bool HasCredentials => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
    private static bool ValidCredential(string? value) => value != null && value.Length <= 512 && value.All(c => c > 32 && c < 127);
    public bool IsValid => ValidCredential(ClientId) && !ClientId.Contains(':') && ValidCredential(ClientSecret) &&
        AccountNumber != null && (AccountNumber.Length == 0 || AccountNumber.Length == 6 && AccountNumber.All(char.IsAsciiLetterOrDigit)) && DailyLimit is >= 1 and <= 100000;
    public override string ToString() => $"UPS connection: credentials configured={HasCredentials}, local daily limit={DailyLimit}";
}
public sealed class UpsSettingsStore(string path, ISecretProtector protector)
{
    private readonly ProtectedSettingsStore<UpsConnectionSettings> store = new(path, protector, () => new(), s => s.IsValid);
    public ProtectedSettingsLoad<UpsConnectionSettings> Load() => store.Load();
    public bool Save(UpsConnectionSettings settings) => store.Save(settings);
}
