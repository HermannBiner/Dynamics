using System.Globalization;
using Dynamics.Core.General;
using Dynamics.Core.Pendulum;

namespace Dynamics.Tests.Pendulum;

public class PendulumControllerTests
{
    [Fact]
    public void FindsAllPendulumsByReflection()
    {
        var controller = new PendulumController();
        Assert.Equal(["CombinedPendulum", "DoublePendulum", "ShakedPendulum"], controller.PendulumTypes.Select(t => t.Name));
        Assert.All(controller.PendulumNames, name => Assert.DoesNotContain("Cls", name));
    }

    [Fact]
    public void StartParameterValuesAreConstantsThenVariables()
    {
        var controller = new PendulumController();
        controller.SelectPendulum(IndexOf(controller, typeof(DoublePendulum)));
        var values = controller.GetStartParameterValues();
        Assert.Equal(4, values.Count);
        Assert.Equal(0.7, values[0]);
        Assert.Equal(0.2, values[1]);

        controller.SelectPendulum(IndexOf(controller, typeof(CombinedPendulum)));
        Assert.Equal(2, controller.GetStartParameterValues().Count);
    }

    [Fact]
    public void TakeOverRejectsValuesOutsideTheRange()
    {
        var controller = new PendulumController();
        controller.SelectPendulum(IndexOf(controller, typeof(DoublePendulum)));

        Assert.False(controller.TakeOverStartParameter(["0.7", "5", "0.5", "0.5"]));
        Assert.False(controller.TakeOverStartParameter(["0.7", "abc", "0.5", "0.5"]));
        Assert.True(controller.TakeOverStartParameter(["0.6", "0.3", "1.5", "-0.5"]));
        Assert.Equal([0.6, 0.3, 1.5, -0.5], controller.GetStartParameterValues());
        Assert.True(controller.MainDS.IsStartparameter2Set);
    }

    [Fact]
    public void ShadowStartsWithShiftedVariables()
    {
        var controller = new PendulumController();
        controller.SelectPendulum(IndexOf(controller, typeof(DoublePendulum)));
        controller.CreateShadow();

        var shadow = Assert.IsType<DoublePendulum>(controller.ShadowDS);
        Assert.False(shadow.IsMainDS);
        Assert.Equal(controller.MainDS.CalculationVariables[0] + 0.005, shadow.CalculationVariables[0]);
        Assert.Equal(controller.MainDS.CalculationConstants![1], shadow.CalculationConstants![1]);

        controller.ResetIteration();
        Assert.Null(controller.ShadowDS);
    }

    [Fact]
    public void IterationRunsStopsAndContinues()
    {
        var controller = new PendulumController();
        controller.CreateShadow();

        Assert.True(controller.Start());
        Assert.Equal(IterationStatus.Running, controller.Status);
        for (var i = 0; i < 100; i++)
        {
            controller.Step();
        }
        Assert.Equal(100, controller.N);
        Assert.Equal(EnergyState.Ok, controller.GetEnergyBar().State);

        controller.Stop();
        Assert.Equal(IterationStatus.Interrupted, controller.Status);
        Assert.False(controller.IsEditable);
        Assert.True(controller.Start());
        Assert.Equal(100, controller.N);
    }

    [Fact]
    public void MouseSetsFirstThenSecondStartParameter()
    {
        var controller = new PendulumController();
        controller.SelectPendulum(IndexOf(controller, typeof(DoublePendulum)));

        Assert.True(controller.MouseDown(new MathPoint(0.5, -0.5)));
        controller.MouseUp();
        Assert.True(controller.MainDS.IsStartparameter1Set);
        var l1 = controller.GetStartParameterValues()[0];

        Assert.True(controller.MouseDown(new MathPoint(0.6, -0.8)));
        controller.MouseUp();
        Assert.Equal(l1, controller.GetStartParameterValues()[0]);
        Assert.False(controller.MainDS.IsStartparameter2Set);

        controller.Start();
        Assert.False(controller.MouseDown(new MathPoint(0, 0)));
    }

    [Fact]
    public void CoordinateTransformMatchesVBAndRoundTrips()
    {
        var transform = new CoordinateTransform(600, 600, new Interval(-1.0, 1.0), new Interval(-1.0, 1.0));

        // VB: A + (x - 1) * width / (size - 1)
        var point = transform.PixelToMathPoint(300, 300);
        Assert.Equal(-1.0 + 299.0 * 2.0 / 599.0, point.X);
        Assert.Equal(1.0 - 299.0 * 2.0 / 599.0, point.Y);

        var (x, y) = transform.MathToPixel(point);
        Assert.Equal(300, x, 9);
        Assert.Equal(300, y, 9);
        Assert.False(transform.IsInside(new MathPoint(1.1, 0)));
    }

    private static int IndexOf(PendulumController controller, Type type) =>
        controller.PendulumTypes.ToList().IndexOf(type);
}
