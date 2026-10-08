namespace CatPriceCalculator.Core;

public sealed record UpsConnectionSettings(string ClientId = "", string ClientSecret = "", string AccountNumber = "", int DailyLimit = 250)
{
    public bool HasCredentials => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
    public bool IsValid => CredentialRules.ApiValue(ClientId) && !ClientId.Contains(':') && CredentialRules.ApiValue(ClientSecret) &&
        AccountNumber != null && (AccountNumber.Length == 0 || AccountNumber.Length == 6 && AccountNumber.All(char.IsAsciiLetterOrDigit)) && DailyLimit is >= 1 and <= 100000;
    public override string ToString() => $"UPS connection: credentials configured={HasCredentials}, local daily limit={DailyLimit}";
}
public sealed class UpsSettingsStore(string path, ISecretProtector protector)
{
    private readonly ProtectedSettingsStore<UpsConnectionSettings> store = new(path, protector, () => new(), s => s.IsValid);
    public ProtectedSettingsLoad<UpsConnectionSettings> Load() => store.Load();
    public bool Save(UpsConnectionSettings settings) => store.Save(settings);
}
