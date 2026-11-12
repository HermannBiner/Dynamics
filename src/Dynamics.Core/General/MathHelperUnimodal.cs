namespace Dynamics.Core.General;

/// <summary>
/// Helper functions for unimodal maps (dual numbers, protocols); see the mathematical documentation.
/// Port of ClsMathhelperUnimodal.
/// </summary>
public static class MathHelperUnimodal
{
    /// <summary>
    /// Converts a dual string (positive dual number &lt; 1, digits only) to a double number.
    /// With isMidOfInterval the result is moved to the middle of the interval of the missing digits.
    /// </summary>
    public static double DualStringToDecimalNumber(string dualString, bool isMidOfInterval = false)
    {
        double result = 0;
        double factor = 1;

        foreach (var digit in dualString)
        {
            factor /= 2;
            switch (digit)
            {
                case '1':
                    result += factor;
                    break;
                case '0':
                    break;
                default:
                    throw new ArgumentException(Text.Get("InvalidDualNumberDigits"));
            }
        }

        if (isMidOfInterval)
        {
            result += factor / 4;
        }

        return result;
    }

    /// <summary>
    /// Converts a double number to a dual string. At most numberOfDigits + 1 digits are produced
    /// behind the point, because the dual number can be periodic.
    /// </summary>
    public static string DecimalNumberToDualString(double decNumber, int numberOfDigits = 20)
    {
        var dualString = "";
        var leadingDigits = Math.Floor(decNumber);
        var minorPart = decNumber - leadingDigits;

        if (minorPart > 0 && leadingDigits > 0)
        {
            dualString = ".";
        }

        while (leadingDigits >= 1)
        {
            if (leadingDigits % 2 == 1)
            {
                dualString = "1" + dualString;
                leadingDigits -= 1;
            }
            else
            {
                dualString = "0" + dualString;
            }

            // VB CInt rounds half to even; Convert.ToInt32 does the same.
            leadingDigits = Convert.ToInt32(leadingDigits / 2);
        }

        var counter = 0;

        while (minorPart > 0 && counter <= numberOfDigits)
        {
            minorPart *= 2;
            counter += 1;
            if (minorPart >= 1)
            {
                dualString += "1";
                minorPart -= 1;
            }
            else
            {
                dualString += "0";
            }
        }

        return dualString;
    }

    /// <summary>Appends "0" until the dual number has numberOfDigits characters.</summary>
    public static string FixNumberOfDigitsInDualNumber(string dualNumber, int numberOfDigits) =>
        dualNumber.Length >= numberOfDigits ? dualNumber : dualNumber.PadRight(numberOfDigits, '0');

    /// <summary>Counts the "1" in a dual number ("." is allowed).</summary>
    public static int NumberOfOnesInaDualNumber(string dualNumber)
    {
        var counter = 0;

        foreach (var digit in dualNumber)
        {
            switch (digit)
            {
                case '0':
                case '.':
                    break;
                case '1':
                    counter++;
                    break;
                default:
                    throw new ArgumentException(Text.Get("InvalidDualNumberDigits"));
            }
        }

        return counter;
    }

    /// <summary>
    /// True if the string is a non-empty dual number of 0 and 1.
    /// Unlike VB, no message box is shown; the UI shows Text.Get("InvalidDualNumberDigits").
    /// </summary>
    public static bool CheckFormatDualNumber(string dualNumber) =>
        dualNumber.Length > 0 && dualNumber.All(digit => digit is '0' or '1');

    /// <summary>
    /// Start value (as dual string) for the tent map that generates the given protocol.
    /// Also used for conjugates of the tent map.
    /// </summary>
    public static string ProtocolToTentmapStartValueAsString(string protocol)
    {
        var dualString = "";

        for (var i = protocol.Length - 1; i >= 0; i--)
        {
            dualString = protocol[i] switch
            {
                '0' => "0" + dualString,
                '1' => "1" + ComplementDualString(dualString),
                _ => throw new ArgumentException(Text.Get("InvalidProtocolDigits")),
            };
        }

        return dualString;
    }

    /// <summary>Swaps "0" and "1".</summary>
    public static string ComplementDualString(string dualNumber) =>
        string.Concat(dualNumber.Select(digit => digit switch
        {
            '0' => '1',
            '1' => '0',
            _ => throw new ArgumentException(Text.Get("InvalidDualNumberDigits")),
        }));
}
