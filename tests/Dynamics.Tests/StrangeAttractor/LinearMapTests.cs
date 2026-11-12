using Dynamics.Core.General;
using Dynamics.Core.StrangeAttractor;

namespace Dynamics.Tests.StrangeAttractor;

/// <summary>
/// The linear map of ClsLinear, corrected: in the complex case (ρ &gt; 10) the XY part is a rotation by
/// φ = h·π with the stretching a, Z is multiplied by λ.
/// </summary>
public class LinearMapTests
{
    private static double Norm(MathPoint p) => Math.Sqrt(p.X * p.X + p.Y * p.Y);

    private static double Angle(MathPoint p) => Math.Atan2(p.Y, p.X);

    [Theory]
    [InlineData(15, 0.005)]
    [InlineData(19.98, 0.1)]
    [InlineData(10.5, 0.01)]
    public void ComplexCaseIsARotationWithStretching(double rho, double h)
    {
        var linear = new Linear { DSParameterValue = rho, StepWidth = h };
        linear.SetIterationParameters();
        linear.ActualMathPoint = new MathPoint(3, 1, 10);

        // a = 0.985 + 0.02 (ρ - 10) / 10, φ = h·π
        var a = 0.985 + 0.02 * (rho - 10) / 10;
        var phi = h * Math.PI;

        for (var i = 0; i < 200; i++)
        {
            var before = new MathPoint(linear.ActualMathPoint.X, linear.ActualMathPoint.Y);
            linear.IterationStep();
            var after = linear.ActualMathPoint;

            Assert.Equal(a, Norm(after) / Norm(before), 12);
            var turn = Math.IEEERemainder(Angle(after) - Angle(before), 2 * Math.PI);
            Assert.Equal(phi, turn, 12);
        }
    }

    [Fact]
    public void RealCaseIsUnchanged()
    {
        // ρ ≤ 10: Beta = 0, the corrected and the VB map are the same (also compared with VB)
        var linear = new Linear { DSParameterValue = 4, StepWidth = 0.005, ActualMathPoint = new MathPoint(2, 3, 1) };
        linear.SetIterationParameters();
        linear.IterationStep();
        Assert.Equal(new[] { 2 * 0.993, 3 * 0.997, 0.989 },
            new[] { linear.ActualMathPoint.X, linear.ActualMathPoint.Y, linear.ActualMathPoint.Z });
    }

    [Fact]
    public void BifurcationOfTheLinearMapIsNotZero()
    {
        // VB: the matrix stayed 0, every value of the diagram was 0
        var controller = new BifurcationController(580, 580);
        controller.SelectSystem(0);
        Assert.IsType<Linear>(controller.DS);
        Assert.Equal(0.005, controller.DS.StepWidth);
        Assert.Null(controller.Start());

        var column = controller.NextColumn()!;
        Assert.All(column.Cycles, c => Assert.Contains(c.Values, v => v != 0));

        // The values are the iterates of the linear map with the parameter of the column
        var linear = new Linear { DSParameterValue = column.ParameterValue, ActualMathPoint = new MathPoint(0.1, 0.1, 0.1) };
        linear.SetIterationParameters();
        for (var n = 1; n < BifurcationController.RunTimeUntilCycle; n++)
        {
            linear.IterationStep();
        }

        linear.IterationStep();
        Assert.Equal(linear.ActualMathPoint.X, column.Cycles[0].Values[0]);
    }
}
