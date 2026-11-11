using System.Globalization;
using System.Text.Json;
using Dynamics.Core.General;
using Dynamics.Core.Julia;
using static Dynamics.Tests.ReferenceData;

namespace Dynamics.Tests.Julia;

public class JuliaReferenceTests
{
    public static TheoryData<string> Scenarios()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "ReferenceData", "julia.json");
        return new TheoryData<string>(JsonSerializer.Deserialize<Dictionary<string, string[][]>>(File.ReadAllText(path))!.Keys);
    }

    private static int Int(string s) => int.Parse(s, CultureInfo.InvariantCulture);

    /// <summary>Every pixel (steps and color) and the protocol are exactly as in VB.</summary>
    [Theory]
    [MemberData(nameof(Scenarios))]
    public void PictureMatchesVB(string scenario)
    {
        var rows = Rows("julia.json", scenario);
        var setup = rows[0];
        var size = Int(setup[2]);

        IJulia ds = setup[1] switch
        {
            "ClsJuliaPN" => new JuliaPN(),
            "ClsMandelbrot" => new Mandelbrot(),
            _ => throw new InvalidOperationException(setup[1]),
        };
        ds.DiagramSize = size;
        ds.N = Int(setup[3]);
        ds.ActualXRange = new Interval(Dec(setup[4]), Dec(setup[5]));
        ds.ActualYRange = new Interval(Dec(setup[6]), Dec(setup[7]));
        ds.C = new ComplexNumber(Dbl(setup[8]), Dbl(setup[9]));
        ds.IsUseSystemColors = bool.Parse(setup[10]);
        ds.RedPercent = Int(setup[11]) / 10.0;
        ds.GreenPercent = Int(setup[12]) / 10.0;
        ds.BluePercent = Int(setup[13]) / 10.0;
        ds.IsProtocol = bool.Parse(setup[14]);
        ds.ResetIteration();

        var protocol = new List<string>();
        for (var q = 1; q <= size; q++)
        {
            var expected = rows[q][1].Split(' ');
            Assert.Equal(size, expected.Length);
            for (var p = 1; p <= size; p++)
            {
                var pixel = ds.IterationStep(p, q);
                var parts = expected[p - 1].Split(';');
                Assert.True(Int(parts[0]) == pixel.Steps && Int(parts[1]) == pixel.Color,
                    $"pixel ({p}, {q}): VB {parts[0]};{parts[1]}, C# {pixel.Steps};{pixel.Color}");
                if (pixel.ProtocolEntry is not null)
                {
                    protocol.Add(pixel.ProtocolEntry);
                }
            }
        }

        Assert.Equal(rows[size + 1].Skip(1), protocol);
    }
}
