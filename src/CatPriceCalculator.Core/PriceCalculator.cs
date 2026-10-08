using System.Globalization;
using System.Buffers;
namespace CatPriceCalculator.Core;
public static class PriceCalculator
{
    public const decimal DefaultSellingMultiplier = 1.40m;
    public const decimal DealerMultiplier = 1.10m;
    public static IReadOnlyList<decimal> SellingMultipliers { get; } = Array.AsReadOnly(Enumerable.Range(0, 15).Select(i => 1.30m + i * 0.05m).ToArray());
    public static IReadOnlyList<decimal> EuroSellingMultipliers => SellingMultipliers;
    public static readonly CultureInfo Hungarian = CultureInfo.GetCultureInfo("hu-HU");
    public static bool TryPositive(string text, out decimal value)
    {
        // Most edits fit on the stack; preserve accepted separators without allocating strings.
        char[]? rented = null;
        Span<char> normalized = text.Length <= 128 ? stackalloc char[text.Length] : (rented = ArrayPool<char>.Shared.Rent(text.Length));
        try
        {
            int length = 0;
            foreach (char c in text)
            {
                if (c is ' ' or '\u00a0' or '\u202f') continue;
                normalized[length++] = c == ',' ? '.' : c;
            }
            return decimal.TryParse(normalized[..length], NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value) && value > 0;
        }
        finally { if (rented != null) ArrayPool<char>.Shared.Return(rented); }
    }

    public static (decimal Dealer, decimal Cost, decimal Selling) Calculate(decimal price, decimal rate, decimal multiplier)
    {
        if (price <= 0 || rate <= 0 || !SellingMultipliers.Contains(multiplier)) throw new ArgumentOutOfRangeException(nameof(price));
        return CalculateCore(price, rate, multiplier, DealerMultiplier);
    }
    public static (decimal Dealer, decimal Cost, decimal Selling) CalculateEuro(decimal price, decimal rate, decimal multiplier)
    {
        if (price <= 0 || rate <= 0 || !EuroSellingMultipliers.Contains(multiplier)) throw new ArgumentOutOfRangeException(nameof(price));
        return CalculateCore(price, rate, multiplier, 1m);
    }
    private static (decimal Dealer, decimal Cost, decimal Selling) CalculateCore(decimal price, decimal rate, decimal multiplier, decimal markup)
    {
        var dealer = price * markup;
        var cost = dealer * rate;
        return (dealer, cost, decimal.Round(cost * multiplier, 0, MidpointRounding.AwayFromZero));
    }
    public static (decimal Amount, decimal? MarginPercent) Profit(decimal cost, decimal selling)
    {
        if (cost <= 0 || selling < 0) throw new ArgumentOutOfRangeException(nameof(cost));
        var amount = selling - cost;
        return (amount, selling == 0 ? null : amount / selling * 100m);
    }
    public static decimal ConvertToEuro(decimal priceDkk, decimal euroPerDkk)
    {
        if (priceDkk <= 0 || euroPerDkk <= 0) throw new ArgumentOutOfRangeException(nameof(priceDkk));
        return priceDkk * euroPerDkk;
    }
    public static string Forints(decimal value) => value.ToString("N0", Hungarian).Replace('\u00a0', ' ').Replace('\u202f', ' ') + " Ft";
}
