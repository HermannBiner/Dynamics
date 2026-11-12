using System.Globalization;
using System.Text.Json;
using Dynamics.Core.General;
using Dynamics.Core.NumericMethods;
using static Dynamics.Tests.ReferenceData;

namespace Dynamics.Tests.NumericMethods;

public class NumericMethodTests
{
    /// <summary>
    /// The real thread pendulum is excluded: its Runge-Kutta error in VB was corrected in Dynamics,
    /// so it is checked mathematically below instead of against VB.
    /// </summary>
    public static TheoryData<string> Scenarios()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "ReferenceData", "numericmethods.json");
        var keys = JsonSerializer.Deserialize<Dictionary<string, string[][]>>(File.ReadAllText(path))!.Keys;
        return new TheoryData<string>(keys.Where(k => !k.StartsWith("ClsRealThreadPendulum", StringComparison.Ordinal)));
    }

    /// <summary>Runs the real thread pendulum from (u0, 0) up to t = h · steps.</summary>
    private static (double U, double V) RunThreadPendulum(double h, int steps, double u0)
    {
        var ds = new RealThreadPendulum { h = h, NumberOfApproxSteps = steps, Amplitude = u0 };
        ds[0] = 0;
        ds[2] = 0;
        ds.Iteration();
        return (ds[1], ds[2]);
    }

    private static double Energy((double U, double V) s) => s.V * s.V / 2 - Math.Cos(s.U);

    [Fact]
    public void RealThreadPendulumConservesEnergy()
    {
        // y'' + sin(y) = 0 conserves E = y'^2/2 - cos(y). With the VB error the drift was 2.3e-3.
        var e0 = Energy((0.8, 0.0));
        var drift = Math.Abs(Energy(RunThreadPendulum(0.01, 1000, 0.8)) - e0);
        Assert.True(drift < 1e-10, $"energy drift {drift:E2}");
    }

    [Fact]
    public void RealThreadPendulumHasOrderFour()
    {
        // Halving h reduces the error by about 2^4 = 16 (with the VB error only by 2).
        var reference = RunThreadPendulum(0.0005, 20000, 0.8).U;
        var error1 = Math.Abs(RunThreadPendulum(0.02, 500, 0.8).U - reference);
        var error2 = Math.Abs(RunThreadPendulum(0.01, 1000, 0.8).U - reference);
        var ratio = error1 / error2;
        Assert.InRange(ratio, 12, 20);
    }

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void TrajectoryMatchesVB(string scenario)
    {
        var rows = Rows("numericmethods.json", scenario);
        var setup = rows[0];

        INumericMethod ds = setup[1] switch
        {
            "ClsRealSpringPendulum" => new RealSpringPendulum(),
            "ClsForwardEuler" => new ForwardEuler(),
            "ClsBackwardEuler" => new BackwardEuler(),
            "ClsMidpointImplicit" => new MidpointImplicit(),
            "ClsRungeKutta4Pendulum" => new RungeKutta4Pendulum(),
            "ClsRealThreadPendulum" => new RealThreadPendulum(),
            _ => throw new InvalidOperationException(setup[1]),
        };
        ds.h = Dec(setup[2]);
        ds.NumberOfApproxSteps = int.Parse(setup[3], CultureInfo.InvariantCulture);
        ds.Amplitude = Dec(setup[4]);
        ds[0] = 0;
        ds[2] = Dec(setup[5]);

        var n = 0;
        foreach (var row in rows.Skip(1))
        {
            var target = int.Parse(row[1], CultureInfo.InvariantCulture);
            while (n < target)
            {
                ds.Iteration();
                n++;
            }

            Close(row[2], ds[0], 1e-9, $"t, step {n}");
            Close(row[3], ds[1], 1e-9, $"x, step {n}");
            Close(row[4], ds[2], 1e-9, $"v, step {n}");
        }
    }

    [Fact]
    public void ControllerFindsAllMethods()
    {
        var controller = new NumericMethodController();
        Assert.Equal(6, controller.MethodTypes.Count);
        Assert.All(controller.MethodNames, name => Assert.DoesNotContain("Cls", name));
    }

    [Theory]
    // track bar, stretched -> A, B, approx steps of B, steps until shift
    [InlineData(1, false, "0.1", "0.1", 1, 1)]
    [InlineData(5, false, "0.1", "0.02", 5, 1)]
    [InlineData(10, false, "0.1", "0.0058823529411764705882352941", 17, 1)]
    [InlineData(6, false, "0.1", "0.01", 10, 1)]
    [InlineData(15, false, "0.1", "0.001", 100, 1)]
    [InlineData(5, true, "0.02", "0.02", 1, 1)]
    [InlineData(6, true, "0.01", "0.01", 1, 2)]
    [InlineData(15, true, "0.001", "0.001", 1, 20)]
    public void StepWidthsAsInVB(int trackBar, bool stretched, string a, string b, int approxStepsB, int stepsUntilShift)
    {
        var controller = new NumericMethodController();
        controller.SetStepWidth(trackBar);
        controller.SetStretched(stretched);
        Assert.True(controller.Start());

        Close(a, controller.StepWidthA);
        Close(b, controller.StepWidthB);
        Assert.Equal(approxStepsB, controller.NumberOfApproxStepsB);
        Assert.Equal(stepsUntilShift, controller.NumberOfStepsUntilShift);
        Close(a, controller.DSA.h);
    }

    [Fact]
    public void NotStretchedKeepsBothPendulumsSynchronized()
    {
        var controller = new NumericMethodController();
        controller.SelectMethod(controller.MethodTypes.ToList().IndexOf(typeof(RungeKutta4Pendulum)));
        controller.SetStepWidth(10);
        controller.Start();
        for (var i = 0; i < 63; i++)
        {
            controller.Step();
        }

        // B makes 17 steps of 0.1/17 per step of A: same time up to rounding; RK4 stays close to cos(t).
        Assert.True(Math.Abs(controller.DSA[0] - controller.DSB[0]) < 1e-12);
        Assert.True(Math.Abs(controller.DSA[1] - controller.DSB[1]) < 0.000001);
    }

    [Fact]
    public void StepReportsTraceAndShift()
    {
        var controller = new NumericMethodController();
        controller.SetStretched(true);
        controller.SetStepWidth(15);
        controller.Start();

        var shifts = Enumerable.Range(0, 40).Select(_ => controller.Step()).Count(s => s.IsShift);
        Assert.Equal(2, shifts);

        var step = controller.Step();
        Assert.Equal(NumericMethodController.XPositionA, step.NextTraceA.X);
        Assert.StartsWith("00041: ", step.ValueLine);
    }

    [Fact]
    public void MouseSetsAmplitudeOnlyInStartArea()
    {
        var controller = new NumericMethodController();

        Assert.False(controller.MouseDown(new MathPoint(0, 0.3)));
        Assert.True(controller.MouseDown(new MathPoint(-0.9, 0.99)));
        Assert.Equal(0.95, controller.DSA[1]);
        Assert.Equal(0.95, controller.DSB[1]);

        controller.MouseMove(new MathPoint(-0.9, -0.2));
        controller.MouseUp();
        Assert.Equal(-0.2, controller.DSB[1]);
        Assert.Equal(0.0, controller.DSB[2]);

        controller.Start();
        Assert.False(controller.MouseDown(new MathPoint(-0.9, 0)));
    }
}
