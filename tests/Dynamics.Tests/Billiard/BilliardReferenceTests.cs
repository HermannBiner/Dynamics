using System.Globalization;
using System.Text.Json;
using Dynamics.Core.Billiard;
using Dynamics.Core.General;
using static Dynamics.Tests.ReferenceData;

namespace Dynamics.Tests.Billiard;

public class BilliardReferenceTests
{
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
    private static readonly CoordinateTransform Picture = new(600, 600, new Interval(-1m, 1m), new Interval(-1m, 1m));

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
        Assert.Equal([Dec(rows[0][3]), Dec(rows[0][4])], new[] { table.A, table.B });

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
                    break;
                case "step":
                    var step = ball!.IterationStep();
                    if (step.Protocol is not null)
                    {
                        protocol.Add(step.Protocol);
                    }

                    Assert.Equal([Dec(row[2]), Dec(row[3])], new[] { ball.Startparameter, ball.Phi });
                    break;
                case "protocol":
                    // The VB oval and stadium balls had no texts (the protocol stopped the program), so it was off there
                    if (row.Length > 1)
                    {
                        Assert.Equal(row.Skip(1), protocol);
                    }
                    break;
                case "pairs":
                    pairBall = table.GetBilliardBall();
                    pair = new DecValuePair(Dec(row[1]), Dec(row[2]));
                    break;
                case "pair":
                    pair = pairBall!.GetNextValuePair(pair!);
                    Assert.Equal([Dec(row[2]), Dec(row[3])], new[] { pair.X, pair.Y });
                    break;
                case "mouse":
                    ball = table.GetBilliardBall();
                    var t = ball.SetUserStartposition(Picture.PixelToMathPoint(Int(row[1]), Int(row[2])));
                    ball.Startparameter = t;
                    Assert.Equal(Dec(row[3]), t);
                    break;
                case "mouseend" when table is OvalBilliardTable:
                    // Corrected in Dynamics (VB took the angle seen from (0, 0)), see BilliardCorrectionTests
                    break;
                case "mouseend":
                    var phi = ball!.SetUserEndposition(Picture.PixelToMathPoint(Int(row[1]), Int(row[2])));
                    Assert.Equal(Dec(row[3]), phi);
                    Assert.Equal(Dec(row[4]), ball.CalculateAlfa(ball.Startparameter, phi));
                    break;
                case "error":
                    Assert.Fail("VB threw " + row[1]);
                    break;
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
            Assert.Equal(Dec(row[2]), column.C);
            var expected = row.Skip(3).Select(v => v.Split(';')).Select(v => (Dec(v[0]), Dec(v[1]))).ToArray();
            Assert.Equal(expected, column.Pairs.Select(p => (p.X, p.Y)).ToArray());
        }
    }
}
