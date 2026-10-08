using System.Globalization;
namespace CatPriceCalculator.Core;
public sealed record GlsConnectionSettings(string Username = "", string Password = "", string ClientNumber = "", int DailyLimit = 250)
{
    public bool HasCredentials => !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrEmpty(Password);
    public bool IsValid => Username != null && Username.Length <= 254 && Username.All(c => !char.IsControl(c) && !char.IsWhiteSpace(c)) &&
        Password != null && Password.Length <= 512 && Password.All(c => !char.IsControl(c)) && ClientNumber != null &&
        (ClientNumber.Length == 0 || int.TryParse(ClientNumber, NumberStyles.None, CultureInfo.InvariantCulture, out int value) && value > 0) && DailyLimit is >= 1 and <= 100000;
    public override string ToString() => $"GLS connection: configured={HasCredentials}, local daily limit={DailyLimit}";
}
public sealed class GlsSettingsStore(string path, ISecretProtector protector)
{
    private readonly ProtectedSettingsStore<GlsConnectionSettings> store = new(path, protector, () => new(), s => s.IsValid);
    public ProtectedSettingsLoad<GlsConnectionSettings> Load() => store.Load();
    public bool Save(GlsConnectionSettings settings) => store.Save(settings);
}
