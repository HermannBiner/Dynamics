using System.Globalization;
using System.Text.Json;

namespace Dynamics.Tests;

/// <summary>
/// Reference results of the original VB Simulator, written by tools/ReferenceExport.
/// Each row holds the inputs followed by the outputs, as invariant strings.
/// </summary>
public static class ReferenceData
{
    private static readonly Dictionary<string, Dictionary<string, string[][]>> Cache = [];

    public static string[][] Rows(string file, string name)
    {
        lock (Cache)
        {
            if (!Cache.TryGetValue(file, out var data))
            {
                var path = Path.Combine(AppContext.BaseDirectory, "ReferenceData", file);
                data = JsonSerializer.Deserialize<Dictionary<string, string[][]>>(File.ReadAllText(path))!;
                Cache[file] = data;
            }

            return data[name];
        }
    }

    /// <summary>A Decimal value of VB, read as Double (Dynamics computes in Double since step 6).</summary>
    public static double Dec(string s) => double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);

    public static double Dbl(string s) => double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);

    /// <summary>Relative tolerance for values computed in a few steps (Double against VB's Decimal).</summary>
    public const double Tolerance = 1e-9;

    /// <summary>|expected − actual| ≤ tolerance · max(1, |expected|).</summary>
    public static bool IsClose(double expected, double actual, double tolerance = Tolerance) =>
        Math.Abs(expected - actual) <= tolerance * Math.Max(1, Math.Abs(expected));

    public static void Close(double expected, double actual, double tolerance = Tolerance, string? what = null) =>
        Assert.True(IsClose(expected, actual, tolerance),
            $"{what}: expected {expected.ToString("R", CultureInfo.InvariantCulture)}, actual {actual.ToString("R", CultureInfo.InvariantCulture)} (tolerance {tolerance})");

    public static void Close(string expected, double actual, double tolerance = Tolerance, string? what = null) =>
        Close(Dec(expected), actual, tolerance, what);

    /// <summary>
    /// Chaotic systems: the Double trajectory follows VB's Decimal trajectory only for a while, then the rounding
    /// differences grow exponentially. Compares the rows in order until the first one that does not match and asserts
    /// that at least minRows rows matched (or all rows, if there are fewer). Returns the number of matching rows.
    /// </summary>
    public static int FollowsVB(string what, int rowCount, Func<int, bool> rowMatches, int minRows)
    {
        var matching = 0;
        while (matching < rowCount && rowMatches(matching))
        {
            matching++;
        }

        Assert.True(matching >= Math.Min(minRows, rowCount), $"{what}: only {matching} of {rowCount} rows follow VB (at least {minRows} expected)");
        return matching;
    }

    /// <summary>
    /// A column of a bifurcation diagram (values after a long transient). A periodic column follows VB within the
    /// tolerance; in a chaotic column Double and Decimal parted long ago, so only the range is checked: every value lies
    /// in VB's range, widened by half its width. Returns true if the column follows VB.
    /// </summary>
    public static bool AssertColumn(string what, IReadOnlyList<double> expected, IReadOnlyList<double> actual, double tolerance = 1e-6)
    {
        Assert.Equal(expected.Count, actual.Count);
        if (expected.Zip(actual).All(pair => IsClose(pair.First, pair.Second, tolerance)))
        {
            return true;
        }

        var min = expected.Min();
        var max = expected.Max();
        var margin = Math.Max((max - min) / 2, tolerance * Math.Max(1, Math.Abs(max)));
        foreach (var value in actual)
        {
            Assert.True(value >= min - margin && value <= max + margin, $"{what}: {value} outside of VB's range [{min}, {max}]");
        }

        return false;
    }
}
