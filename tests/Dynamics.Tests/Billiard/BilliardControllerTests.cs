using System.Globalization;
using Dynamics.Core.Billiard;
using Dynamics.Core.General;
using static Dynamics.Tests.ReferenceData;

namespace Dynamics.Tests.Billiard;

public class BilliardControllerTests
{
    public BilliardControllerTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
    }

    [Fact]
    public void ControllerStartsAsInVB()
    {
        var controller = new BilliardTableController();
        Assert.Equal([typeof(EllipseBilliardTable), typeof(OvalBilliardTable), typeof(StadiumBilliardTable)], controller.TableTypes);
        Assert.IsType<EllipseBilliardTable>(controller.DS);
        Assert.Equal(0.8m, controller.DS.C);
        Assert.Equal("0.8", controller.ParameterText);
        Assert.Equal((50, 200), (controller.ParameterTrackBarMinimum, controller.ParameterTrackBarMaximum));

        // Corrected: the track bar shows c (VB: CInt(0.5 + 1.5 · 0.5 · 100) = 76)
        Assert.Equal(80, controller.ParameterTrackBar);
        Assert.Equal(10, controller.Speed);
        Assert.Equal(("0", "1"), (controller.TText, controller.AlfaText));
        Assert.All(controller.TableNames, name => Assert.DoesNotContain("Cls", name));
    }

    [Fact]
    public void StartNeedsABall()
    {
        var controller = new BilliardTableController();
        Assert.NotNull(controller.Start());
        Assert.Equal(IterationStatus.Stopped, controller.Status);
    }

    [Fact]
    public void TakeOverAndIterate()
    {
        var controller = new BilliardTableController { IsProtocol = true };
        Assert.Null(controller.TakeOverStartParameter());
        var drawings = controller.TakeDrawings();
        Assert.Equal(2, drawings.OfType<PermanentBall>().Count());
        Assert.Single(controller.DS.BilliardBallCollection);

        Assert.Null(controller.Start());
        controller.Step();
        drawings = controller.TakeDrawings();
        var segment = Assert.Single(drawings.OfType<OrbitSegment>());

        // speed 10: step width 2 · 10 / 1000 = 0.02, positions until i · 0.02 > 1
        Assert.Equal(51, segment.Positions.Count);
        Assert.Single(drawings.OfType<PhasePortraitPoint>());
        Assert.Single(controller.Protocol);
        Assert.Equal(1, controller.Steps);

        // The orbit is the one of the model
        var ball = new EllipseBilliardTable { C = 0.8m }.GetBilliardBall();
        ball.Speed = 10;
        ball.Startparameter = 0;
        ball.Startangle = 1;
        var step = ball.IterationStep();
        Assert.Equal(step.End.X, segment.Positions[^1].X);
    }

    [Fact]
    public void NextStepOnlyWhenStopped()
    {
        var controller = new BilliardTableController();
        controller.TakeOverStartParameter();
        Assert.Null(controller.NextStep());
        Assert.Equal(1, controller.N);

        controller.Start();
        controller.Stop();
        Assert.False(controller.CanNextStep);
        controller.NextStep();
        Assert.Equal(1, controller.N);
    }

    [Fact]
    public void FillPhasePortraitAsInVB()
    {
        var controller = new BilliardTableController();
        Assert.Null(controller.PrepareBallsForPhasePortrait());

        // alfa = 0.001 + k·π/30 < π for k = 1..29; the first ball is not on the table
        Assert.Equal(29, controller.DS.BilliardBallCollection.Count);
        var drawings = controller.TakeDrawings();
        Assert.Equal(30, drawings.OfType<OrbitSegment>().Count());
        Assert.All(drawings.OfType<OrbitSegment>(), s => Assert.Single(s.Positions));

        // These balls never write the protocol
        controller.IsProtocol = true;
        controller.Start();
        controller.Step();
        Assert.Empty(controller.Protocol);
    }

    [Fact]
    public void MouseSetsStartAndDirection()
    {
        var controller = new BilliardTableController();
        Assert.False(controller.MouseDown(new MathPoint(0, 0.5m)));
        Assert.Null(controller.CreateNewBall());
        Assert.True(controller.CanSetWithMouse);

        // start: on the ellipse in the direction of the mouse
        Assert.True(controller.MouseDown(new MathPoint(0, 0.5m)));
        Assert.True(controller.MouseUp(new MathPoint(0, 0.5m)));
        Assert.Equal((decimal)(Math.PI / 2), Dec(controller.TText), 10);
        Assert.Empty(controller.DS.BilliardBallCollection);

        // direction: then the ball is on the table
        Assert.True(controller.MouseDown(new MathPoint(0.5m, 0)));
        Assert.True(controller.MouseUp(new MathPoint(0.5m, 0)));
        Assert.Single(controller.DS.BilliardBallCollection);
        Assert.False(controller.CanSetWithMouse);
    }

    [Fact]
    public void ParameterCAsInVB()
    {
        var controller = new BilliardTableController();
        controller.SetParameterFromTrackBar(150);
        Assert.Equal(1.5m, controller.DS.C);
        Assert.Equal(0.66m, controller.DS.A);

        controller.ParameterText = "3";
        Assert.NotNull(controller.TakeOverParameterText());
        controller.ParameterText = "0.6";
        Assert.Null(controller.TakeOverParameterText());
        Assert.Equal(60, controller.ParameterTrackBar);
    }

    [Fact]
    public void CDiagramStartsWithTheOvalAsInVB()
    {
        var controller = new CDiagramController(580, 580);
        Assert.IsType<OvalBilliardTable>(controller.DS);
        Assert.Equal(1, controller.SelectedValueParameter);
        Assert.Equal("0.001", controller.VMin);
        Assert.Equal("3.13", controller.VMax);
        Assert.Equal(60, controller.Position);

        Assert.Null(controller.Start());
        var column = controller.NextColumn()!;
        Assert.Equal(581, column.Pairs.Count);
        Assert.Equal(column.Pairs[3].Y, controller.DrawnValue(column.Pairs[3]));

        controller.SetValueParameter(0);
        Assert.Equal("0", controller.VMin);
    }
}
