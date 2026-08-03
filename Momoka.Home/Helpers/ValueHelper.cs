namespace Momoka.Home.Helpers;

/// <summary>
/// Pure integer math helpers. C#'s built-in operators truncate toward zero,
/// which breaks floor semantics for negative operands — these helpers restore
/// the mathematical floor behavior that grid chunking depends on.
/// </summary>
public static class ValueHelper
{
    /// <summary>
    /// Floor division: rounds the quotient toward negative infinity, so that
    /// <c>FloorDiv(a, n) == floor(a / n)</c> for any sign of <paramref name="value"/>.
    /// </summary>
    public static int FloorDiv(int value, int divisor) =>
        value >= 0 ? value / divisor : (value - (divisor - 1)) / divisor;

    /// <summary>
    /// Non-negative remainder: the result is always in <c>[0, divisor)</c>,
    /// unlike C#'s <c>%</c> which can be negative for negative operands.
    /// </summary>
    public static int FloorMod(int value, int divisor) =>
        ((value % divisor) + divisor) % divisor;
}
