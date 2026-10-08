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
public sealed class DhlSettingsStore(string path, ISecretProtector protector)
{
    private readonly ProtectedSettingsStore<DhlConnectionSettings> store = new(path, protector, () => new(), s => s.IsValid);
    public ProtectedSettingsLoad<DhlConnectionSettings> Load() => store.Load();
    public bool Save(DhlConnectionSettings settings) => store.Save(settings);
}
