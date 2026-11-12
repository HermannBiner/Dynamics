namespace Dynamics.Core.General;

/// <summary>
/// Checks of Double values. In the Decimal version an overflow threw an OverflowException; Double goes on with
/// infinity or NaN, so the models check their state and throw the same exception (the pages stop and show a message).
/// </summary>
public static class NumericCheck
{
    /// <summary>Throws an OverflowException if one of the values is infinite or NaN.</summary>
    public static void EnsureFinite(params ReadOnlySpan<double> values)
    {
        foreach (var value in values)
        {
            if (!double.IsFinite(value))
            {
                throw new OverflowException(Text.Get("NumericOverflow"));
            }
        }
    }

    /// <summary>
    /// For the angle normalisations (loops adding or subtracting 2π): throws for infinity or NaN (the loops would
    /// never end) and reduces large angles first, where adding 2π no longer changes the Double.
    /// </summary>
    public static double ReduceAngle(double angle)
    {
        EnsureFinite(angle);
        return Math.Abs(angle) > 1e6 ? Math.IEEERemainder(angle, 2 * Math.PI) : angle;
    }
}
