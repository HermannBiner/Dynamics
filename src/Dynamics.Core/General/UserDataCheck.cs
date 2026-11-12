using System.Globalization;

namespace Dynamics.Core.General;

/// <summary>
/// Checks user input. Port of ClsCheckUserData, ClsCheckIsInterval and ClsCheckIsNumeric:
/// instead of showing a message box, the methods return the message (null if the input is OK).
/// </summary>
public static class UserDataCheck
{
    /// <summary>
    /// VB IsNumeric + CDec: current culture; the invariant culture is accepted as well. Infinity and NaN are not
    /// accepted (Decimal had neither).
    /// </summary>
    public static bool TryParse(string? text, out double value) =>
        (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) ||
         double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) && double.IsFinite(value);

    /// <summary>VB ClsCheckUserData.IsTxtValueAllowed.</summary>
    public static string? CheckValue(string? text, Interval target, out double value)
    {
        if (!TryParse(text, out value))
        {
            return Text.Get("NumericValue");
        }

        if (target.IsNumberInInterval(value))
        {
            return null;
        }

        return Text.Get("TheValue") + value.ToString(CultureInfo.CurrentCulture) + Text.Get("ValueNotAllowed") +
               "[" + target.A.ToString(CultureInfo.CurrentCulture) + ", " + target.B.ToString(CultureInfo.CurrentCulture) + "]!";
    }

    /// <summary>VB ClsCheckUserData.IsIntervalAllowed.</summary>
    public static string? CheckInterval(string? textA, string? textB, Interval target, out Interval? interval)
    {
        interval = null;
        if (!TryParse(textA, out var a) || !TryParse(textB, out var b))
        {
            return Text.Get("NumericValue");
        }

        if (a >= b)
        {
            return Text.Get("LeftSmallerRight");
        }

        var userInterval = new Interval(a, b);
        if (target.IsInterval2PartOfInterval(userInterval))
        {
            interval = userInterval;
            return null;
        }

        return Text.Get("ValueRangeNotAllowed") + " [" + target.A.ToString(CultureInfo.CurrentCulture) + ", " +
               target.B.ToString(CultureInfo.CurrentCulture) + "] ";
    }
}
