namespace CatPriceCalculator.Core;
public static class RateDeviation
{
    public const decimal Threshold = 0.03m;
    // Relative to the current reference rate; exactly 3% does not trigger a warning.
    public static bool IsSignificant(decimal configuredRate, decimal currentRate)
    {
        if (configuredRate <= 0 || currentRate <= 0) throw new ArgumentOutOfRangeException(nameof(configuredRate));
        return decimal.Abs(configuredRate - currentRate) > currentRate * Threshold;
    }
}
