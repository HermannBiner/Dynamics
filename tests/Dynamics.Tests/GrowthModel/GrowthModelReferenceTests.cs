using System.Globalization;
using System.Text.Json;
using Dynamics.Core.GrowthModel;
using static Dynamics.Tests.ReferenceData;

namespace Dynamics.Tests.GrowthModel;

public class GrowthModelReferenceTests(Xunit.Abstractions.ITestOutputHelper output)
{
    /// <summary>
    /// Applications of F that have to follow VB before Double and Decimal may part (measured 2026-11-12: 33 to 35 for
    /// the chaotic border parameters, i.e. one decimal digit per step lost; all for the others).
    /// </summary>
    private const int MinimumApplications = 30;

    private const string File = "growthmodel.json";

    private static IEnumerable<string> Keys()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "ReferenceData", File);
        return JsonSerializer.Deserialize<Dictionary<string, string[][]>>(System.IO.File.ReadAllText(path))!.Keys;
    }

    public static TheoryData<string> Scenarios(string prefix) => new(Keys().Where(k => k.StartsWith(prefix, StringComparison.Ordinal)));

    private static int Int(string s) => int.Parse(s, CultureInfo.InvariantCulture);

    internal static IIteration Create(string vbClass) => vbClass switch
    {
        "ClsLogisticGrowth" => new LogisticGrowth(),
        "ClsMandelbrotReal" => new MandelbrotReal(),
        "ClsParabola" => new Parabola(),
        "ClsTentmap" => new Tentmap(),
        _ => throw new InvalidOperationException(vbClass),
    };

    [Fact]
    public void ModelsAreFoundInTheOrderOfVB()
    {
        var vb = Rows(File, "Types").Select(r => r[1]).ToList();
        Assert.Equal(vb, GrowthModels.FindTypes(includeMandelbrot: true).Select(t => "Cls" + t.Name));
        Assert.DoesNotContain(typeof(MandelbrotReal), GrowthModels.FindTypes(includeMandelbrot: false));
    }

    [Theory]
    [MemberData(nameof(Scenarios), "Trajectory")]
    public void TrajectoryMatchesVB(string scenario)
    {
        var rows = Rows(File, scenario);
        var ds = Create(rows[0][1]);
        ds.ParameterA = Dec(rows[0][2]);
        ds.Power = Int(rows[0][4]);
        var x = Dec(rows[0][3]);

        // Chaotic for the border parameters: Double follows the Decimal trajectory of VB for a while
        var steps = rows.Skip(1).ToArray();
        var matching = FollowsVB(scenario, steps.Length, i =>
        {
            x = ds.FN(x);
            return IsClose(Dec(steps[i][2]), x, 1e-6);
        }, Math.Max(1, MinimumApplications / ds.Power));
        output.WriteLine($"{scenario}: {matching} of {steps.Length} steps follow VB");
    }

    [Fact]
    public void StartValuesForProtocolsMatchVB()
    {
        foreach (var row in Rows(File, "Protocols"))
        {
            var ds = Create(row[1]);
            ds.ParameterA = ds.ChaoticParameterValue;
            ds.Power = 1;
            Close(row[3], ds.CalculateStartValueForProtocol(row[2]), 1e-12, row[1] + " " + row[2]);
        }
    }

    /// <summary>
    /// Corrected in Dynamics (complement rule, exactly 20 start digits, target filled up to 20 digits), so not equal to
    /// VB; both adapt the start value only minimally.
    /// </summary>
    [Fact]
    public void StartValuesForTargetValuesStayCloseToVB()
    {
        foreach (var row in Rows(File, "Targets"))
        {
            var ds = Create(row[1]);
            ds.ParameterA = ds.ChaoticParameterValue;
            ds.Power = 1;
            var result = ds.CalculateStartValueForTargetValue(Dec(row[2]), Dec(row[3]));
            Assert.True(Math.Abs(result - Dec(row[4])) < 1e-5, $"{row[1]} {row[2]} → {row[3]}: {result}, VB {row[4]}");
        }
    }

    public static TheoryData<Type, double, double> TargetCases()
    {
        var data = new TheoryData<Type, double, double>();
        foreach (var start in new[] { 0.314159, 0.1, 0.7, 0.414, 0.407, 0.6, 0.999 })
        {
            foreach (var target in new[] { 0.9, 0.5, 0.001, 0.629, 0.75, 0.123, 0.25 })
            {
                data.Add(typeof(LogisticGrowth), start, target);
                data.Add(typeof(Tentmap), start, target);
                data.Add(typeof(Parabola), 2 * start - 1, 2 * target - 1);
            }
        }

        return data;
    }

    /// <summary>The adjusted start value reaches the target after 20 steps (the kept dual digits of the start value).</summary>
    [Theory]
    [MemberData(nameof(TargetCases))]
    public void StartValueForTargetValueReachesTheTarget(Type model, double start, double target)
    {
        var ds = GrowthModels.Create(model);
        ds.ParameterA = ds.ChaoticParameterValue;
        ds.Power = 1;
        var x = ds.CalculateStartValueForTargetValue(start, target);
        Assert.True(Math.Abs(x - start) < 1e-5, $"start {x}");

        for (var n = 0; n < GrowthModelBase.StartDigits; n++)
        {
            x = ds.FN(x);
        }

        Assert.True(Math.Abs(x - target) < 1e-5, $"{x} after {GrowthModelBase.StartDigits} steps");
    }


    [Theory]
    [MemberData(nameof(Scenarios), "Feigenbaum")]
    public void FeigenbaumColumnsMatchVB(string scenario)
    {
        var rows = Rows(File, scenario);
        var setup = rows[0];
        var controller = new FeigenbaumController(Int(setup[4]), Int(setup[5]));
        controller.SelectSystem(controller.Types.ToList().FindIndex(t => "Cls" + t.Name == setup[1]));
        controller.SetAMin(setup[2]);
        controller.SetAMax(setup[3]);
        Assert.Null(controller.Start());

        foreach (var row in rows.Skip(1))
        {
            var p = Int(row[1]);
            var column = controller.ComputeColumn(p);
            Close(row[2], column.ParameterValue);
            Assert.Equal(4 * Int(setup[5]) + 1, column.Values.Count);
            var expected = new List<double>();
            var actual = new List<double>();
            for (var i = 3; i < row.Length; i += 2)
            {
                expected.Add(Dec(row[i + 1]));
                actual.Add(column.Values[Int(row[i])]);
            }

            var follows = AssertColumn($"{scenario}, column {p}", expected, actual);
            output.WriteLine($"{scenario}, column {p}: {(follows ? "follows VB" : "chaotic, in VB's range")}");
        }
    }

    [Theory]
    [MemberData(nameof(Scenarios), "Histogram")]
    public void HistogramMatchesVB(string scenario)
    {
        var rows = Rows(File, scenario);
        var setup = rows[0];
        var controller = new HistogramController(Int(setup[4]));
        controller.SelectSystem(controller.Types.ToList().FindIndex(t => "Cls" + t.Name == setup[1]));
        controller.Parameter = setup[2];
        controller.StartValue = setup[3];
        controller.Start(); // a < 4 only warns (not chaotic), the iteration runs anyway
        Assert.Equal(Dynamics.Core.General.IterationStatus.Running, controller.Status);
        controller.Steps(Int(setup[5]));

        Assert.Equal(Int(setup[5]), controller.N);
        // Chaotic: the hits of single pixels differ, the distribution stays. Compared in 20 coarse classes, each within
        // 2 % of all steps.
        var expected = rows[1].Skip(1).Select(Int).ToArray();
        var actual = controller.NumberOfHits;
        Assert.Equal(expected.Length, actual.Length);
        Assert.Equal(expected.Sum(), actual.Sum());
        const int classes = 20;
        for (var c = 0; c < classes; c++)
        {
            var from = c * expected.Length / classes;
            var to = (c + 1) * expected.Length / classes;
            var e = expected[from..to].Sum();
            var a = actual[from..to].Sum();
            Assert.True(Math.Abs(e - a) <= 0.02 * controller.N, $"{scenario}, class {c}: VB {e}, Dynamics {a}");
        }
    }
}
