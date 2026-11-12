using System.Globalization;
using System.Text.Json;
using Dynamics.Core.General;
using Dynamics.Core.StrangeAttractor;
using static Dynamics.Tests.ReferenceData;

namespace Dynamics.Tests.StrangeAttractor;

public class StrangeAttractorReferenceTests(Xunit.Abstractions.ITestOutputHelper output)
{
    private Xunit.Abstractions.ITestOutputHelper Output { get; } = output;

    /// <summary>Rows of a trajectory that have to follow VB before Double and Decimal may part.</summary>
    private const int MinimumRows = 40; // measured 2026-11-12: 42 of 65 for Lorenz ρ = 27.99, all for the others

    private static IEnumerable<string> Keys()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "ReferenceData", "strangeattractor.json");
        return JsonSerializer.Deserialize<Dictionary<string, string[][]>>(File.ReadAllText(path))!.Keys;
    }

    /// <summary>
    /// Corrected in Dynamics, therefore not compared with VB: the complex case of the linear map (VB computed Y
    /// with the new X) and the linear map in the bifurcation diagram (VB left the matrix 0). See LinearMapTests.
    /// </summary>
    private static readonly string[] Corrected = ["Linear_Complex", "Linear_Complex_Large", "Bifurcation_Linear_X"];

    public static TheoryData<string> Trajectories() => new(Keys().Where(k =>
        !k.StartsWith("Bifurcation", StringComparison.Ordinal) && !Corrected.Contains(k)));

    public static TheoryData<string> Bifurcations() => new(Keys().Where(k =>
        k.StartsWith("Bifurcation", StringComparison.Ordinal) && !Corrected.Contains(k)));

    private static int Int(string s) => int.Parse(s, CultureInfo.InvariantCulture);

    private static IStrangeAttractor Create(string vbClass) => vbClass switch
    {
        "ClsLinear" => new Linear(),
        "ClsLorenz" => new Lorenz(),
        "ClsRoessler" => new Roessler(),
        "ClsThomas" => new Thomas(),
        _ => throw new InvalidOperationException(vbClass),
    };

    [Theory]
    [MemberData(nameof(Trajectories))]
    public void TrajectoryMatchesVB(string scenario)
    {
        var rows = Rows("strangeattractor.json", scenario);
        var setup = rows[0];
        var ds = Create(setup[1]);
        ds.DSParameterValue = Dec(setup[2]);
        ds.StepWidth = Dec(setup[3]);
        ds.SetIterationParameters();
        ds.ActualMathPoint = new MathPoint(Dec(setup[4]), Dec(setup[5]), Dec(setup[6]));

        // Chaotic: Double follows VB's Decimal trajectory for a while, then they part.
        var trajectory = rows.Skip(1).ToArray();
        var n = 0;
        var matching = FollowsVB(scenario, trajectory.Length, i =>
        {
            var row = trajectory[i];
            var target = Int(row[1]);
            while (n < target)
            {
                ds.IterationStep();
                n++;
            }

            var p = ds.ActualMathPoint;
            return IsClose(Dec(row[2]), p.X, 1e-6) && IsClose(Dec(row[3]), p.Y, 1e-6) && IsClose(Dec(row[4]), p.Z, 1e-6);
        }, MinimumRows);
        Output.WriteLine($"{scenario}: {matching} of {trajectory.Length} rows (up to step {n}) follow VB");
    }

    [Theory]
    [MemberData(nameof(Bifurcations))]
    public void BifurcationColumnsMatchVB(string scenario)
    {
        var rows = Rows("strangeattractor.json", scenario);
        var setup = rows[0];
        var ds = Create(setup[1]);
        var range = new Interval(Dec(setup[2]), Dec(setup[3]));
        var width = Int(setup[4]);
        var lengthOfCycle = Convert.ToInt32(Int(setup[5]) / 10.0);
        var coordinate = Enum.Parse<AttractorCoordinate>(setup[6]);
        IterationPoint[] points =
        [
            new(0.1, 0.1, 0.1, AttractorColors.Blue),
            new(-0.1, -0.1, 0.1, AttractorColors.Red),
        ];

        foreach (var row in rows.Skip(1))
        {
            var p = Int(row[1]);
            var parameterValue = range.A + (range.IntervalWidth * p / width);
            Close(row[2], parameterValue);

            var cycles = BifurcationController.Compute(ds, points, parameterValue, coordinate, lengthOfCycle);
            var values = cycles.SelectMany(c => c.Values).ToArray();
            var follows = AssertColumn($"{scenario}, column {p}", row.Skip(3).Select(Dec).ToArray(), values);
            Output.WriteLine($"{scenario}, column {p}: {(follows ? "follows VB" : "chaotic, in VB's range")}");
        }
    }

    [Fact]
    public void ControllerColumnsAreTheModelColumns()
    {
        var controller = new BifurcationController(580, 580);
        Assert.IsType<Lorenz>(controller.DS);
        Assert.Null(controller.Start());

        var column = controller.NextColumn()!;
        var rows = Rows("strangeattractor.json", "Bifurcation_Lorenz_X");
        Close(rows[1][2], column.ParameterValue);
        var model = BifurcationController.Compute(new Lorenz(), [new(0.1, 0.1, 0.1, AttractorColors.Blue), new(-0.1, -0.1, 0.1, AttractorColors.Red)],
            column.ParameterValue, AttractorCoordinate.X, Convert.ToInt32(580 / 10.0));
        Assert.Equal(model.SelectMany(c => c.Values), column.Cycles.SelectMany(c => c.Values));
    }
}
