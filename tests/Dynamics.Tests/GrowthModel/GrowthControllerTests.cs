using System.Globalization;
using Dynamics.Core.General;
using Dynamics.Core.GrowthModel;

namespace Dynamics.Tests.GrowthModel;

public class GrowthControllerTests
{
    private static string F(double d) => d.ToString(CultureInfo.CurrentCulture);

    private static int IndexOf(GrowthControllerBase controller, Type type) => controller.Types.ToList().IndexOf(type);

    [Fact]
    public void ChaosOnlyAtTheBorderOfTheParameterRange()
    {
        var logistic = new LogisticGrowth { ParameterA = 4 };
        Assert.True(logistic.IsBehaviourChaotic());
        logistic.ParameterA = 3.9;
        Assert.False(logistic.IsBehaviourChaotic());

        var mandelbrot = new MandelbrotReal { ParameterA = -2 };
        Assert.True(mandelbrot.IsBehaviourChaotic());
        mandelbrot.ParameterA = -1.5;
        Assert.False(mandelbrot.IsBehaviourChaotic());
    }

    [Theory]
    [InlineData(typeof(LogisticGrowth), "0110100111010")]
    [InlineData(typeof(Parabola), "1100101011110000")]
    [InlineData(typeof(Tentmap), "10101")]
    public void StartValueForProtocolProducesTheProtocol(Type model, string protocol)
    {
        var controller = new IterationController(530);
        controller.SelectSystem(IndexOf(controller, model));
        controller.SetParameterA(F(controller.DS.ChaoticParameterValue));
        controller.TargetProtocol = protocol;
        Assert.Null(controller.CalculateStartValueForProtocol());

        var (error, _) = controller.Iterate(protocol.Length);
        Assert.Null(error);
        Assert.Equal(protocol, string.Concat(controller.Protocol));
        Assert.Equal(protocol.Length, controller.N);
    }

    [Fact]
    public void InvalidProtocolIsReported()
    {
        var controller = new IterationController(530);
        controller.SetParameterA("4");
        controller.TargetProtocol = "0120";
        var start = controller.StartValue;
        Assert.Equal(Text.Get("InvalidDualNumberDigits"), controller.CalculateStartValueForProtocol());
        Assert.Equal(start, controller.StartValue);
    }

    [Fact]
    public void TimeAxisStopsAtNegativeTargetValue()
    {
        // Corrected: VB stopped only for target values > 0
        var controller = new IterationController(530);
        controller.SelectSystem(IndexOf(controller, typeof(Parabola)));
        controller.SetPresentation(IterationPresentation.TimeAxis);
        controller.SetParameterA("2");
        controller.TargetValue = F(-0.8);
        Assert.Null(controller.CalculateStartValueForTargetValue());

        var (error, drawing) = controller.Iterate(10);
        Assert.Null(error);
        Assert.NotNull(drawing!.TargetLine);
        var last = double.Parse(controller.ValueList[^1], CultureInfo.CurrentCulture);
        Assert.True(Math.Abs(last - -0.8) < IterationController.TargetValuePrecision);
        Assert.True(controller.N < 530 / 2, $"stopped after {controller.N} steps");
    }

    [Fact]
    public void TimeAxisContinuesTheOrbitInTheNextDiagram()
    {
        var controller = new IterationController(530);
        controller.SetPresentation(IterationPresentation.TimeAxis);
        var (_, first) = controller.Iterate(10);
        var lastOfFirst = first!.Lines[^1].B.Y;
        var (_, second) = controller.Iterate(10);
        Assert.True(second!.ClearPicture);
        Assert.Equal(lastOfFirst, second.Lines[0].A.Y);
        Assert.Equal(266, controller.N); // 530 pixels / stretching 2 (+1, rounded step width), counted per diagram
    }

    [Fact]
    public void CobwebAlternatesBetweenDiagonalAndGraph()
    {
        var controller = new IterationController(530);
        var (_, drawing) = controller.Iterate(10);
        Assert.True(drawing!.ClearPicture);
        Assert.Equal(20, drawing.Lines.Count);
        foreach (var line in drawing.Lines.Where((_, i) => i % 2 == 0))
        {
            Assert.Equal(line.A.X, line.A.Y); // on the 45° line
            Assert.Equal(controller.DS.FN(line.B.X), line.B.Y); // on the graph
        }

        var (_, next) = controller.Iterate(1);
        Assert.False(next!.ClearPicture);
        Assert.Equal(11, controller.N);
    }

    [Fact]
    public void TrackBarMatchesTheParameter()
    {
        var controller = new IterationController(530);
        Assert.Equal("2.8", controller.ParameterA.Replace(',', '.'));
        Assert.Equal(700, controller.TrackBar); // CInt(2.8 · 999 / 4 + 1)
        controller.SetTrackBar(1000);
        Assert.Equal(4.0, controller.DS.ParameterA);
    }

    [Fact]
    public void SensitivityOrbitsAreContinuous()
    {
        // Corrected: VB drew the second orbit to FN(x1)
        var controller = new SensitivityController(600, 600);
        controller.SetDifference(false);
        var (error, drawing) = controller.Start();
        Assert.Null(error);
        Assert.Equal(300, drawing!.Orbit2.Count);
        for (var i = 0; i + 1 < drawing.Orbit2.Count; i++)
        {
            Assert.Equal(drawing.Orbit2[i].B.Y, drawing.Orbit2[i + 1].A.Y);
            Assert.Equal(drawing.Orbit1[i].B.Y, drawing.Orbit1[i + 1].A.Y);
        }

        // 1e-12 apart at the start, apart after about 40 doublings
        Assert.Equal(drawing.Orbit1[10].A.Y, drawing.Orbit2[10].A.Y, 8);
        Assert.Contains(drawing.Orbit1.Zip(drawing.Orbit2), pair => Math.Abs(pair.First.A.Y - pair.Second.A.Y) > 0.5);
    }

    [Fact]
    public void SensitivityDifferenceIsTheDistanceOfTheOrbits()
    {
        var controller = new SensitivityController(600, 600);
        var (_, drawing) = controller.Start();
        Assert.Equal(300, drawing!.Difference.Count);
        Assert.InRange(drawing.Difference[0].A.Y, 0.99e-12, 1.01e-12);
        for (var i = 0; i + 1 < drawing.Difference.Count; i++)
        {
            Assert.Equal(drawing.Difference[i].B.Y, drawing.Difference[i + 1].A.Y);
        }
    }

    [Fact]
    public void SensitivityNeedsChaos()
    {
        var controller = new SensitivityController(600, 600);
        controller.Parameter = "3.5";
        Assert.Equal(Text.Get("NonChaoticBehaviour"), controller.Start().Error);
    }

    [Fact]
    public void FeigenbaumDefaultsShowTheRightHalf()
    {
        var controller = new FeigenbaumController(580, 580);
        Assert.Equal(typeof(LogisticGrowth), controller.Types[0]);
        Assert.Equal(("2", "4"), (controller.AMin, controller.AMax));
        controller.SelectSystem(IndexOf(controller, typeof(MandelbrotReal)));
        Assert.Equal((F(-2.0), "0"), (controller.AMin, controller.AMax));
        Assert.Equal(4, controller.Types.Count);
    }

    [Fact]
    public void FeigenbaumColorsAlternateOnlyIfColored()
    {
        var controller = new FeigenbaumController(580, 580);
        Assert.False(controller.IsGreen(1));
        controller.IsColored = true;
        Assert.False(controller.IsGreen(0));
        Assert.True(controller.IsGreen(1));
    }

    [Fact]
    public void FeigenbaumRunsAllColumnsAndShowsSplitPoints()
    {
        var controller = new FeigenbaumController(20, 20);
        Assert.Null(controller.Start());
        var columns = 0;
        while (controller.NextColumn() is not null)
        {
            columns++;
        }

        Assert.Equal(20, columns);
        Assert.Equal(IterationStatus.Stopped, controller.Status);
        Assert.Equal(5, controller.SplitLines().Count());
        Assert.True(controller.SplitLines().Last().IsLast);
    }

    [Fact]
    public void PopulationShowsTheDensity()
    {
        var controller = new PopulationController(new Random(1));
        Assert.Equal(F(31.416), controller.PopulationDensity);
        var picture = controller.StartPicture()!;
        Assert.Equal(1571, picture.Individuals.Count); // CInt(5000 · 0.314159)
        Assert.All(picture.Individuals, p =>
            Assert.True((p.X - 0.5) * (p.X - 0.5) + (p.Y - 0.5) * (p.Y - 0.5) <= picture.Radius * picture.Radius * 1.0000001));

        Assert.Null(controller.Start(single: true));
        var step = controller.Step();
        Assert.Equal(1, controller.N);
        Assert.Equal(controller.DS.FN(0.314159), controller.X);
        Assert.Equal(controller.X / 2, step.Radius);
    }

    [Fact]
    public void PopulationInvalidInputSetsDefaults()
    {
        var controller = new PopulationController();
        controller.StartValue = "2";
        Assert.NotNull(controller.Start(single: false));
        Assert.Equal(IterationStatus.Stopped, controller.Status);
        Assert.Equal(F(0.314159), controller.StartValue);
    }

    [Fact]
    public void HistogramCountsEveryStep()
    {
        var controller = new HistogramController(100);
        controller.Start();
        controller.Steps(5000);
        Assert.Equal(5000, controller.NumberOfHits.Sum());
        controller.Stop();
        controller.Steps(10);
        Assert.Equal(5000, controller.N);
        controller.Start();
        controller.Steps(10);
        Assert.Equal(5010, controller.N);
    }

    [Fact]
    public void TwoDimensionsCellsLieOnTheRaster()
    {
        // Corrected: VB used Fix(x / DeltaX) without the left border of the range
        var controller = new TwoDimensionsController(600);
        controller.SelectSystem(IndexOf(controller, typeof(Parabola)));
        var raster = controller.RasterLines();
        foreach (var (px, py) in new[] { (1.0, 599.0), (150.5, 420.2), (299.0, 301.0), (599.9, 0.1) })
        {
            var cell = controller.CellAt(px, py);
            Assert.Contains(cell.X, raster);
            Assert.Contains(cell.Y, raster);
            var x = -1.0 + px * 2 / 600;
            var y = 1.0 - py * 2 / 600;
            Assert.InRange(x, cell.X, cell.X + controller.DeltaX);
            Assert.InRange(y, cell.Y, cell.Y + controller.DeltaX);
        }
    }

    [Fact]
    public void TwoDimensionsDefaultsAndRaster()
    {
        var controller = new TwoDimensionsController(600);
        Assert.Equal(5.0 / 600, controller.DeltaX);
        Assert.Equal(121, controller.RasterLines().Count); // 120 cells, including the border lines
        Assert.Equal((F(0.414), F(0.407)), (controller.X, controller.Y));
        controller.SelectSystem(IndexOf(controller, typeof(Parabola)));
        Assert.Equal((F(-0.172), F(-0.186)), (controller.X, controller.Y));
    }

    [Fact]
    public void TwoDimensionsStartCellSetsTheMiddle()
    {
        var controller = new TwoDimensionsController(600);
        controller.MouseDown(100, 500);
        Assert.Equal(TwoDimensionsController.Blue, controller.HoverCell!.Color);
        Assert.Null(controller.MouseUp());
        var cell = controller.Cells.Single().Corner;
        Assert.Equal(F(Math.Round(cell.X + controller.DeltaX / 2, 3)), controller.X);
        Assert.True(controller.IsTargetCellMarked); // sensitivity: only a start cell
    }

    [Fact]
    public void TwoDimensionsTransitivityReachesTheTargetCell()
    {
        var controller = new TwoDimensionsController(600);
        controller.SetMode(TwoDimensionsMode.Transitivity);
        controller.MouseDown(200, 400);
        controller.MouseUp();
        controller.MouseDown(450, 120);
        Assert.Equal(TwoDimensionsController.Red, controller.HoverCell!.Color);
        Assert.Null(controller.MouseUp());
        var target = controller.Cells[1].Corner;

        Assert.Null(controller.Begin(1));
        TwoDimensionsStep? last = null;
        var steps = 0;
        while (controller.Step() is { } step)
        {
            last = step;
            steps++;
        }

        Assert.NotNull(last!.TargetCell);
        Assert.True(steps < 20, $"{steps} steps");
        Assert.True(Math.Abs(last.NextP.X - (target.X + controller.DeltaX / 2)) <= 4 * controller.DeltaX);
        Assert.Equal(IterationStatus.Ready, controller.Status);
    }

    [Fact]
    public void TwoDimensionsNewExperimentStartsAtTheTextBoxes()
    {
        var controller = new TwoDimensionsController(600);
        controller.SetExperiment(2); // before the first start: corrected, a is read
        Assert.Null(controller.Begin(10));
        Assert.Equal(TwoDimensionsController.ExperimentColors[2], controller.Color);
        var first = controller.Step()!;
        Assert.Equal(0.414, first.P.X);
        Assert.Equal(controller.DS.FN(0.414), first.NextP.X);
        Assert.NotEqual(0.0, first.NextP.X);
        while (controller.Step() is not null)
        {
        }

        Assert.Null(controller.Begin(1));
        Assert.NotEqual(0.414, controller.Step()!.P.X); // continues the orbit
    }
}
