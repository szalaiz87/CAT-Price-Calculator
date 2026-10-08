namespace CatPriceCalculator.Core;
public sealed record FedExConnectionSettings(string ClientId = "", string ClientSecret = "", int DailyLimit = 250)
{
    public bool HasCredentials => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
    public bool IsValid => CredentialRules.ApiValue(ClientId) && CredentialRules.ApiValue(ClientSecret) && DailyLimit is >= 1 and <= 100000;
    public override string ToString() => $"FedEx connection: configured={HasCredentials}, local daily limit={DailyLimit}";
}
public sealed class FedExSettingsStore(string path, ISecretProtector protector)
{
    private readonly ProtectedSettingsStore<FedExConnectionSettings> store = new(path, protector, () => new(), s => s.IsValid);
    public ProtectedSettingsLoad<FedExConnectionSettings> Load() => store.Load();
    public bool Save(FedExConnectionSettings settings) => store.Save(settings);
}
internal static class CredentialRules
{
    public static bool ApiValue(string? value) => value != null && value.Length <= 512 && value.All(c => c > 32 && c < 127);
}
