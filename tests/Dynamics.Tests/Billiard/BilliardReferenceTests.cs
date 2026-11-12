using System.Globalization;
using System.Text.Json;
using Dynamics.Core.Billiard;
using Dynamics.Core.General;
using static Dynamics.Tests.ReferenceData;

namespace Dynamics.Tests.Billiard;

public class BilliardReferenceTests(Xunit.Abstractions.ITestOutputHelper output)
{
    /// <summary>
    /// Iteration steps and value pairs that have to follow VB before Double and Decimal may part (the oval and the
    /// stadium are chaotic).
    /// </summary>
    private const int MinimumSteps = 10; // measured 2026-11-12: stadium 14 to 23, oval 44 to 60, ellipse all
    private const int MinimumPairs = 10; // measured: stadium 14 to 27, oval 46 to 90, ellipse all

    /// <summary>A sequence of steps or pairs: how many follow VB, until the first that does not.</summary>
    private sealed class Sequence(string name)
    {
        public string Name { get; } = name;
        public int Total { get; private set; }
        public int Following { get; private set; }
        public bool IsFollowing => Following == Total;

        public void Add(bool matches)
        {
            if (IsFollowing && matches)
            {
                Following++;
            }

            Total++;
        }
    }

    private static IEnumerable<string> Keys()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "ReferenceData", "billiard.json");
        return JsonSerializer.Deserialize<Dictionary<string, string[][]>>(File.ReadAllText(path))!.Keys;
    }

    public static TheoryData<string> Tables() => new(Keys().Where(k => !k.StartsWith("CDiagram", StringComparison.Ordinal)));

    public static TheoryData<string> CDiagrams() => new(Keys().Where(k => k.StartsWith("CDiagram", StringComparison.Ordinal)));

    private static int Int(string s) => int.Parse(s, CultureInfo.InvariantCulture);

    private static IBilliardTable Create(string vbClass) => vbClass switch
    {
        "ClsEllipseBilliardTable" => new EllipseBilliardTable(),
        "ClsOvalBilliardTable" => new OvalBilliardTable(),
        "ClsStadiumBilliardTable" => new StadiumBilliardTable(),
        _ => throw new InvalidOperationException(vbClass),
    };

    /// <summary>VB: the picture box of the billiard table is 600 x 600, mathematical range [-1, 1].</summary>
    private static readonly CoordinateTransform Picture = new(600, 600, new Interval(-1.0, 1.0), new Interval(-1.0, 1.0));

    [Theory]
    [MemberData(nameof(Tables))]
    public void TableMatchesVB(string scenario)
    {
        // The VB language manager was German when the data was exported ("Rot" in the protocol)
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = new CultureInfo("de-CH");
        var rows = Rows("billiard.json", scenario);
        var table = Create(rows[0][1]);
        table.C = Dec(rows[0][2]);
        Close(rows[0][3], table.A);
        Close(rows[0][4], table.B);

        var sequences = new List<Sequence>();
        Sequence? sequence = null;
        IBilliardBall? ball = null;
        IBilliardBall? pairBall = null;
        DecValuePair? pair = null;
        var protocol = new List<string>();
        foreach (var row in rows.Skip(1))
        {
            switch (row[0])
            {
                case "orbit":
                    ball = table.GetBilliardBall();
                    ball.ColorName = "Red";
                    ball.Speed = 1000;
                    ball.HasPhasePortrait = true;
                    ball.IsProtocol = true;
                    ball.Startparameter = Dec(row[1]);
                    ball.Startangle = Dec(row[2]);
                    sequence = new Sequence("orbit " + row[1]);
                    sequences.Add(sequence);
                    break;
                case "step":
                    var step = ball!.IterationStep();
                    if (step.Protocol is not null)
                    {
                        protocol.Add(step.Protocol);
                    }

                    sequence!.Add(IsClose(Dec(row[2]), ball.Startparameter, 1e-6) && IsClose(Dec(row[3]), ball.Phi, 1e-6));
                    break;
                case "protocol":
                    // The VB oval and stadium balls had no texts (the protocol stopped the program), so it was off there.
                    // Compared only as long as the orbit follows VB.
                    if (row.Length > 1 && sequence!.IsFollowing)
                    {
                        AssertProtocol(row.Skip(1).ToArray(), protocol);
                    }
                    break;
                case "pairs":
                    pairBall = table.GetBilliardBall();
                    pair = new DecValuePair(Dec(row[1]), Dec(row[2]));
                    sequence = new Sequence("pairs " + row[1]);
                    sequences.Add(sequence);
                    break;
                case "pair":
                    pair = pairBall!.GetNextValuePair(pair!);
                    sequence!.Add(IsClose(Dec(row[2]), pair.X, 1e-6) && IsClose(Dec(row[3]), pair.Y, 1e-6));
                    break;
                case "mouse":
                    ball = table.GetBilliardBall();
                    var t = ball.SetUserStartposition(Picture.PixelToMathPoint(Int(row[1]), Int(row[2])));
                    ball.Startparameter = t;
                    Close(row[3], t);
                    break;
                case "mouseend" when table is OvalBilliardTable:
                    // Corrected in Dynamics (VB took the angle seen from (0, 0)), see BilliardCorrectionTests
                    break;
                case "mouseend":
                    var phi = ball!.SetUserEndposition(Picture.PixelToMathPoint(Int(row[1]), Int(row[2])));
                    Close(row[3], phi);
                    Close(row[4], ball.CalculateAlfa(ball.Startparameter, phi));
                    break;
                case "error":
                    Assert.Fail("VB threw " + row[1]);
                    break;
            }
        }

        foreach (var s in sequences)
        {
            output.WriteLine($"{scenario}, {s.Name}: {s.Following} of {s.Total} follow VB");
            var minimum = s.Name.StartsWith("orbit", StringComparison.Ordinal) ? MinimumSteps : MinimumPairs;
            Assert.True(s.Following >= Math.Min(minimum, s.Total), $"{scenario}, {s.Name}: only {s.Following} of {s.Total} follow VB");
        }
    }

    /// <summary>Protocol lines "Rot t/alfa = t/alfa": the text equal, the numbers within the tolerance.</summary>
    private static void AssertProtocol(string[] expected, List<string> actual)
    {
        Assert.Equal(expected.Length, actual.Count);
        for (var i = 0; i < expected.Length; i++)
        {
            var e = expected[i].Split(" = ");
            var a = actual[i].Split(" = ");
            Assert.Equal(e[0], a[0]);
            var eValues = e[1].Split('/');
            var aValues = a[1].Split('/');
            Assert.Equal(eValues.Length, aValues.Length);
            for (var k = 0; k < eValues.Length; k++)
            {
                Close(eValues[k], Dec(aValues[k]), 1e-6, expected[i]);
            }
        }
    }

    [Theory]
    [MemberData(nameof(CDiagrams))]
    public void CDiagramColumnsMatchVB(string scenario)
    {
        var rows = Rows("billiard.json", scenario);
        var table = Create(rows[0][1]);
        var range = new Interval(Dec(rows[0][2]), Dec(rows[0][3]));
        var trackBar = Int(rows[0][4]);
        var ball = table.GetBilliardBall();

        foreach (var row in rows.Skip(1))
        {
            var column = CDiagramController.ComputeColumn(table, ball, range, Int(row[1]), 580, 580, trackBar);
            Close(row[2], column.C);
            var expected = row.Skip(3).Select(v => v.Split(';')).ToArray();
            var followsX = AssertColumn($"{scenario}, column {row[1]}, t", expected.Select(v => Dec(v[0])).ToArray(), column.Pairs.Select(p => p.X).ToArray());
            var followsY = AssertColumn($"{scenario}, column {row[1]}, φ", expected.Select(v => Dec(v[1])).ToArray(), column.Pairs.Select(p => p.Y).ToArray());
            output.WriteLine($"{scenario}, column {row[1]}: {(followsX && followsY ? "follows VB" : "chaotic, in VB's range")}");
        }
    }
}
