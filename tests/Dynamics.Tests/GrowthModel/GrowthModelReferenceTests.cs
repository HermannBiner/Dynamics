using System.Globalization;
using System.Text.Json;
using Dynamics.Core.GrowthModel;
using static Dynamics.Tests.ReferenceData;

namespace Dynamics.Tests.GrowthModel;

public class GrowthModelReferenceTests
{
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
        foreach (var row in rows.Skip(1))
        {
            x = ds.FN(x);
            Assert.Equal(Dec(row[2]), x);
        }
    }

    [Fact]
    public void StartValuesForProtocolsMatchVB()
    {
        foreach (var row in Rows(File, "Protocols"))
        {
            var ds = Create(row[1]);
            ds.ParameterA = ds.ChaoticParameterValue;
            ds.Power = 1;
            Assert.Equal(Dec(row[3]), ds.CalculateStartValueForProtocol(row[2]));
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
            Assert.True(Math.Abs(result - Dec(row[4])) < 1e-5m, $"{row[1]} {row[2]} → {row[3]}: {result}, VB {row[4]}");
        }
    }

    public static TheoryData<Type, decimal, decimal> TargetCases()
    {
        var data = new TheoryData<Type, decimal, decimal>();
        foreach (var start in new[] { 0.314159m, 0.1m, 0.7m, 0.414m, 0.407m, 0.6m, 0.999m })
        {
            foreach (var target in new[] { 0.9m, 0.5m, 0.001m, 0.629m, 0.75m, 0.123m, 0.25m })
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
    public void StartValueForTargetValueReachesTheTarget(Type model, decimal start, decimal target)
    {
        var ds = GrowthModels.Create(model);
        ds.ParameterA = ds.ChaoticParameterValue;
        ds.Power = 1;
        var x = ds.CalculateStartValueForTargetValue(start, target);
        Assert.True(Math.Abs(x - start) < 1e-5m, $"start {x}");

        for (var n = 0; n < GrowthModelBase.StartDigits; n++)
        {
            x = ds.FN(x);
        }

        Assert.True(Math.Abs(x - target) < 1e-5m, $"{x} after {GrowthModelBase.StartDigits} steps");
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
            Assert.Equal(Dec(row[2]), column.ParameterValue);
            Assert.Equal(4 * Int(setup[5]) + 1, column.Values.Count);
            for (var i = 3; i < row.Length; i += 2)
            {
                Assert.Equal(Dec(row[i + 1]), column.Values[Int(row[i])]);
            }
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
        Assert.Equal(rows[1].Skip(1).Select(Int), controller.NumberOfHits);
    }
}
