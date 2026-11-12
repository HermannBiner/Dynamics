using Dynamics.Core.Billiard;
using Dynamics.Core.General;

namespace Dynamics.Tests.Billiard;

/// <summary>The VB errors of the billiards corrected in Dynamics.</summary>
public class BilliardCorrectionTests
{
    [Theory]
    [InlineData(0.5)]
    [InlineData(0.8)]
    [InlineData(2)]
    public void StadiumBallMovesThroughTheCurvesWithoutJumping(double c)
    {
        // A ball crossing the curved parts: all positions are on the segment, the last one is the end point
        var table = new StadiumBilliardTable { C = c };
        var ball = table.GetBilliardBall();
        ball.Speed = 10;
        ball.Startparameter = 2 * table.A + table.B; // in the left half circle
        ball.Startangle = 1.2;

        for (var i = 0; i < 30; i++)
        {
            var step = ball.IterationStep();
            var length = Distance(step.Start, step.End);

            // VB: in the curves the positions jumped to the end point; now each step moves 2 · 10 / 1000 of the segment
            for (var k = 0; k < step.Positions.Count - 1; k++)
            {
                var expected = Math.Min(1, (k + 1) * 0.02) * length;
                Assert.Equal(expected, Distance(step.Start, step.Positions[k]), 6);
            }

            Assert.Equal(0, Distance(step.Positions[^1], step.End), 6);
        }
    }

    private static double Distance(MathPoint a, MathPoint b) =>
        Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));

    [Theory]
    [InlineData(0.5, 0.9, 0.1)]
    [InlineData(0.8, -0.6, 0.5)]
    [InlineData(1.5, 0.2, -0.8)]
    public void OvalEndPositionIsInTheDirectionOfTheMouse(double c, double mouseX, double mouseY)
    {
        var table = new OvalBilliardTable { C = c };
        var ball = table.GetBilliardBall();
        ball.Startparameter = 1;
        var mouse = new MathPoint(mouseX, mouseY);
        ball.SetUserEndposition(mouse);

        // seen from the center (m, 0), the end position is on the border in the direction of the mouse
        var m = (table.A - table.B) / 2;
        var end = ball.UserEndposition;
        var mouseAngle = Math.Atan2(mouseY, (mouseX - m) * (mouseX >= m ? 1 : (table.B / table.A)));
        var endAngle = Math.Atan2(end.Y, (end.X - m) * (end.X >= m ? 1 : (table.B / table.A)));
        Assert.Equal(mouseAngle, endAngle, 8);
    }

    [Fact]
    public void ParameterTrackBarShowsC()
    {
        var controller = new BilliardTableController();
        Assert.Equal(Convert.ToInt32(controller.DS.C * 100), controller.ParameterTrackBar);
    }
}
