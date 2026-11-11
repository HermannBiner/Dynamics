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

    public static decimal Dec(string s) => decimal.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);

    public static double Dbl(string s) => double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);
}
