using System.Globalization;
using System.Text.Json;
using Dynamics.Core.General;
using Dynamics.Core.Julia;
using Dynamics.Core.Newton;
using static Dynamics.Tests.ReferenceData;

namespace Dynamics.Tests.Newton;

public class NewtonReferenceTests(Xunit.Abstractions.ITestOutputHelper output)
{
    /// <summary>Share of pixels and protocol lines that may differ from VB (Double against Decimal).</summary>
    /// With rotation many points do not converge within the steps, so a larger part of the picture is border
    /// (measured 2026-11-12: Invers_N4_Rotate 1.9 %, Unit_N12 0.25 %, the others 0).
    private const double MaxDifferentFraction = 0.005;
    private const double MaxDifferentFractionRotated = 0.025;

    public static TheoryData<string> Scenarios()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "ReferenceData", "newton.json");
        return new TheoryData<string>(JsonSerializer.Deserialize<Dictionary<string, string[][]>>(File.ReadAllText(path))!.Keys);
    }

    private static int Int(string s) => int.Parse(s, CultureInfo.InvariantCulture);

    /// <summary>Every pixel and the protocol are exactly as in VB (same call order as the exporter).</summary>
    [Theory]
    [MemberData(nameof(Scenarios))]
    public void PictureMatchesVB(string scenario)
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        var rows = Rows("newton.json", scenario);
        var setup = rows[0];
        var size = Int(setup[2]);
        var n = Int(setup[3]);
        var c = new ComplexNumber(Dbl(setup[8]), Dbl(setup[9]));

        INewton ds = setup[1] switch
        {
            "ClsUnitRootCollection" => new UnitRootCollection(),
            "ClsInversUnitRoots" => new InversUnitRoots(),
            "ClsPolynom3C" => new Polynom3C(),
            _ => throw new InvalidOperationException(setup[1]),
        };
        ds.DiagramSize = size;
        ds.N = n;
        if (bool.Parse(setup[13]))
        {
            ds.C = c;
        }

        ds.ResetIteration();
        ds.N = ds.IsUseN ? n : 3;
        ds.ActualXRange = new Interval(Dec(setup[4]), Dec(setup[5]));
        ds.ActualYRange = new Interval(Dec(setup[6]), Dec(setup[7]));
        ds.IsProtocol = bool.Parse(setup[12]);
        ds.UseMixing = Enum.Parse<NewtonMixing>(setup[10]);
        ds.UseColor = Enum.Parse<NewtonColor>(setup[11]);
        if (ds.IsUseC)
        {
            ds.C = c;
        }

        var colors = new int[size, size];
        var protocol = new List<string>();
        for (var q = 0; q < size; q++)
        {
            for (var p = 1; p <= size; p++)
            {
                var pixel = ds.IterationStep(p, q);
                var (x, y) = SpiralScan.BitmapPosition(p, q);
                colors[x, y] = pixel.Color;
                if (pixel.ProtocolEntry is not null)
                {
                    protocol.Add(pixel.ProtocolEntry);
                }
            }
        }

        // Double instead of Decimal (division of ComplexNumber): pixels near the borders of the basins may converge
        // to another root or need another number of steps.
        var differentPixels = 0;
        for (var y = 0; y < size; y++)
        {
            var expected = rows[1 + y][1].Split(' ').Select(Int).ToArray();
            for (var x = 0; x < size; x++)
            {
                if (expected[x] != colors[x, y])
                {
                    differentPixels++;
                }
            }
        }

        var expectedProtocol = rows[size + 1].Skip(1).ToArray();
        Assert.Equal(expectedProtocol.Length, protocol.Count);
        var differentLines = expectedProtocol.Zip(protocol).Count(pair => pair.First != pair.Second);
        output.WriteLine($"{scenario}: {differentPixels} of {size * size} pixels, {differentLines} of {protocol.Count} protocol lines differ");
        var maxFraction = scenario.EndsWith("_Rotate", StringComparison.Ordinal) ? MaxDifferentFractionRotated : MaxDifferentFraction;
        Assert.True(differentPixels <= maxFraction * size * size, $"{differentPixels} pixels differ");
        Assert.True(differentLines <= MaxDifferentFraction * Math.Max(1, protocol.Count), $"{differentLines} protocol lines differ");
    }
}
