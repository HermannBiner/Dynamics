using Dynamics.Core.General;
using Dynamics.Core.GrowthModel;
using Dynamics.Core.Pendulum;

namespace Dynamics.Tests.General;

/// <summary>Behaviour that changed with the switch from Decimal to Double (step 6).</summary>
public class DoubleSwitchTests
{
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void AngleNormalisationThrowsInsteadOfLoopingForever(double angle)
    {
        Assert.Throws<OverflowException>(() => MathHelperPendulum.AngleInMinusPiAndPi(angle));
        Assert.Throws<OverflowException>(() => MathHelperAngles.AngleInNullTwoPi(angle));
    }

    [Fact]
    public void LargeAnglesAreReduced()
    {
        // adding 2π no longer changes such a Double, the loops would never end
        var angle = MathHelperPendulum.AngleInMinusPiAndPi(1e20);
        Assert.InRange(angle, -Math.PI, Math.PI);
        Assert.InRange(MathHelperAngles.AngleInNullTwoPi(-1e20), 0, 2 * Math.PI);
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    [InlineData("1e400")]
    public void NonFiniteInputIsNotANumber(string text) => Assert.False(UserDataCheck.TryParse(text, out _));

    [Fact]
    public void UnstablePendulumThrowsOverflow()
    {
        // Decimal threw an OverflowException; Double would go on with infinity, so the state is checked
        var pendulum = new DoublePendulum { IsMainDS = true, StepWidth = 1 };
        pendulum.SetDefaultUserData();
        pendulum.ResetIteration();
        var variables = pendulum.CalculationVariables;
        variables[0] = double.NaN;
        pendulum.CalculationVariables = variables;
        Assert.Throws<OverflowException>(() => pendulum.IterationStep(false));
    }

    [Fact]
    public void TentmapDoesNotFallToZero()
    {
        // In binary Double, a = 2 would shift out one bit per step: after about 53 steps every orbit is 0
        var tentmap = new Tentmap { ParameterA = 2, Power = 1 };
        var x = 0.314159;
        var zeros = 0;
        for (var n = 0; n < 1000; n++)
        {
            x = tentmap.FN(x);
            if (x == 0)
            {
                zeros++;
            }
        }

        Assert.Equal(0, zeros);
    }

    [Fact]
    public void SensitivityStartValuesDiffer()
    {
        // VB: 1e-27 apart, which is the same Double
        var controller = new SensitivityController(600, 600);
        Assert.True(UserDataCheck.TryParse(controller.StartValue1, out var x1));
        Assert.True(UserDataCheck.TryParse(controller.StartValue2, out var x2));
        Assert.NotEqual(x1, x2);
    }
}
