using System.Globalization;
using Dynamics.Core.General;
using Dynamics.Core.StrangeAttractor;
using static Dynamics.Tests.ReferenceData;

namespace Dynamics.Tests.StrangeAttractor;

public class StrangeAttractorControllerTests
{
    public StrangeAttractorControllerTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
    }

    [Fact]
    public void ControllerStartsWithLorenzAsInVB()
    {
        var controller = new StrangeAttractorController(600);
        Assert.Equal([typeof(Linear), typeof(Lorenz), typeof(Roessler), typeof(Thomas)], controller.AttractorTypes);
        Assert.IsType<Lorenz>(controller.DS);
        Assert.Equal(9, controller.DS.StartPointSets.Count);
        Assert.Equal("1", controller.X);

        // VB: CInt(28 * 1000 / 30) = 933, the text shows the value of the model
        Assert.Equal(933, controller.DSParameterTrackBar);
        Assert.Equal("28", controller.DSParameterText);
        Assert.Equal(5, controller.StepWidthTrackBar);
        Assert.Equal(-Math.PI / 15, controller.Phi);
        Assert.All(controller.AttractorNames, name => Assert.DoesNotContain("Cls", name));
    }

    [Fact]
    public void StartNeedsTakeOverAsInVB()
    {
        var controller = new StrangeAttractorController(600);
        Assert.False(controller.CanStart);
        Assert.False(controller.Start());

        Assert.Null(controller.TakeOverPointSet());
        Assert.True(controller.Start());
        var drawn = controller.Step();
        Assert.Single(drawn);
        Assert.Equal(1, controller.N);

        // The step equals the model with the step width of the track bar
        var lorenz = new Lorenz { StepWidth = 0.001, ActualMathPoint = new MathPoint(1, 1, 1) };
        lorenz.IterationStep();
        Assert.Equal(lorenz.ActualMathPoint.X, controller.IterationPointSet.Points[0].ActualPoint.X);

        controller.Stop();
        Assert.Equal(IterationStatus.Interrupted, controller.Status);
        Assert.True(controller.Start());
    }

    [Fact]
    public void TakeOverReadsASingleStartPoint()
    {
        var controller = new StrangeAttractorController(600);
        Assert.True(controller.IsStartPointEditable);
        controller.X = "2.5";
        controller.Y = "-3";
        controller.Z = "abc";
        Assert.NotNull(controller.TakeOverPointSet());
        Assert.False(controller.CanStart);

        controller.Z = "4";
        Assert.Null(controller.TakeOverPointSet());
        Assert.Equal(new[] { 2.5, -3.0, 4.0 },
            new[] { controller.IterationPointSet.Points[0].ActualPoint.X, controller.IterationPointSet.Points[0].ActualPoint.Y, controller.IterationPointSet.Points[0].ActualPoint.Z });

        // Several points cannot be edited; the texts list all points
        controller.SetStartPointSet(3);
        Assert.False(controller.IsStartPointEditable);
        Assert.Equal("0.01/-0.01", controller.X);
    }

    [Fact]
    public void ParameterTrackBarResetsTheIteration()
    {
        var controller = new StrangeAttractorController(600);
        controller.TakeOverPointSet();
        controller.SetDSParameterFromTrackBar(500);
        Assert.Equal(15.0, controller.DS.DSParameterValue);
        Assert.Equal("15", controller.DSParameterText);
        Assert.False(controller.CanStart);
    }

    [Fact]
    public void ProjectionsAsInVB()
    {
        var controller = new StrangeAttractorController(600);
        var point = new MathPoint(3, 4, 5);

        // Lorenz: interval [0, 60], view corrections -10, 10, -10
        controller.SetProjection(AttractorProjection.XY);
        Assert.Equal(new MathPoint(33, 4 + 20 + 10).Y, controller.Project(point).Y);
        Assert.Equal(33.0, controller.Project(point).X);
        controller.SetProjection(AttractorProjection.XZ);
        Assert.Equal(5.0 + 20 - 10, controller.Project(point).Y);

        controller.SetProjection(AttractorProjection.ThreeD);
        double phi = controller.Phi, theta = controller.Theta;
        var rotatedX = 3 * Math.Cos(phi) - 4 * Math.Sin(phi) * Math.Cos(theta) - 5 * Math.Sin(phi) * Math.Sin(theta);
        var rotatedZ = -4 * Math.Sin(theta) + 5 * Math.Cos(theta);
        var plane = controller.Project(point);
        Assert.Equal(-rotatedX + 30, plane.X);
        Assert.Equal(Math.Max(0, rotatedZ + 20 - 10), plane.Y);

        var (lines, points) = controller.CoordinateCube();
        Assert.Equal(10, lines.Count);
        Assert.Equal(7, points.Count);
    }

    [Fact]
    public void MouseTurnsTheViewOnlyIn3DWhenStopped()
    {
        var controller = new StrangeAttractorController(600);
        Assert.True(controller.MouseDown());
        Assert.True(controller.MouseMove(300, 300));

        // x = 300 → Phi = π / 2 - π / 2 = 0 up to rounding;
        // y = 300 → plane y = 30 → Theta = π/2 · (60 - 90) / 60 < 0.01 → 0.01
        Assert.Equal(0, controller.Phi, 12);
        Assert.Equal(0.01, controller.Theta);
        controller.MouseUp();
        Assert.False(controller.MouseMove(0, 0));

        controller.SetProjection(AttractorProjection.XY);
        Assert.False(controller.MouseDown());
    }

    [Fact]
    public void LinearMapUsesTheRealOrComplexForm()
    {
        var linear = new Linear { StepWidth = 0.005, ActualMathPoint = new MathPoint(1, 0, 1) };
        linear.SetIterationParameters();
        linear.IterationStep();

        // ρ = 4 ≤ 10: diagonal map with λ = 0.989, α = 0.993, δ = 0.997
        Assert.Equal(0.993, linear.ActualMathPoint.X);
        Assert.Equal(0.0, linear.ActualMathPoint.Y);
        Assert.Equal(0.989, linear.ActualMathPoint.Z);
    }

    [Fact]
    public void BifurcationControllerAsInVB()
    {
        var controller = new BifurcationController(580, 580);
        Assert.Equal("0", controller.AMin);
        Assert.Equal("30", controller.AMax);
        Assert.Equal("-20", controller.XMin);
        Assert.Equal("Delta = 30", controller.DeltaA);

        controller.SetActualValueRange(AttractorCoordinate.Z);
        Assert.Equal("0", controller.XMin);
        Assert.Equal("50", controller.XMax);

        // The split points of the Lorenz attractor; the last one red
        Assert.Equal([(1.0, false), (1.346, false), (13.926, false), (24.06, false), (24.74, true)], controller.SplitLines());

        controller.SetAMin("31");
        Assert.NotNull(controller.Start());
        Assert.Equal(IterationStatus.Stopped, controller.Status);

        // Area selection whenever stopped
        controller.SetDefaultUserData();
        Assert.True(controller.SelectArea(1, 1, 580, 580));
        Assert.Equal(0.0, Dec(controller.AMin));
        Assert.Equal(30.0, Dec(controller.AMax));
    }
}
