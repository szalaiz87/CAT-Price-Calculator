using CatPriceCalculator.Core;
namespace CatPriceCalculator;

// A single settings editor uses these adapters; existing encrypted DHL/UPS file formats remain intact.
public sealed record CarrierInput(string Primary = "", string Secondary = "", string Account = "", string Postal = "", string Service = "", int DailyLimit = 250)
{
    public override string ToString() => "Carrier credentials (redacted)";
}
public sealed class CarrierBinding
{
    public Courier Carrier { get; }
    public string Name { get; }
    public CarrierInput Current { get; private set; }
    public bool LoadFailed { get; }
    private readonly Func<CarrierInput, bool> validate, configured, save;
    private readonly Func<CarrierInput, string, CancellationToken, Task<ParcelTrackingResult>> fetch;
    private readonly Action? forget;
    private CarrierBinding(Courier carrier, string name, CarrierInput current, bool failed, Func<CarrierInput, bool> validate,
        Func<CarrierInput, bool> configured, Func<CarrierInput, bool> save, Func<CarrierInput, string, CancellationToken, Task<ParcelTrackingResult>> fetch, Action? forget)
    { Carrier = carrier; Name = name; Current = current; LoadFailed = failed; this.validate = validate; this.configured = configured; this.save = save; this.fetch = fetch; this.forget = forget; }
    public bool HasCredentials => configured(Current);
    public bool IsValid(CarrierInput input) => validate(input) && configured(input);
    public bool Save(CarrierInput input)
    {
        if (!validate(input) || !save(input)) return false;
        Current = input; forget?.Invoke(); return true;
    }
    public Task<ParcelTrackingResult> FetchAsync(CarrierInput snapshot, string number, CancellationToken token) => fetch(snapshot, number, token);
    public static CarrierBinding Create<T>(Courier carrier, string name, ProtectedSettingsLoad<T> loaded, Func<T, CarrierInput> view,
        Func<CarrierInput, T> convert, Func<T, bool> valid, Func<T, bool> configured, Func<T, bool> save,
        Func<T, string, CancellationToken, Task<ParcelTrackingResult>> fetch, Action? forget = null) where T : class =>
        new(carrier, name, view(loaded.Settings), loaded.Failed, i => valid(convert(i)), i => configured(convert(i)), i => save(convert(i)), (i, n, ct) => fetch(convert(i), n, ct), forget);
}
