using Dynamics.Core.General;
using Dynamics.Core.Pendulum;

namespace Dynamics.Tests.Pendulum;

/// <summary>
/// The test modes replace the coupled equations by simple, independent oscillators,
/// so their energies must be conserved. VB had errors in the shaken and the combined pendulum.
/// </summary>
public class PendulumTestModeTests
{
    private const decimal g = 9.81m;

    [Fact]
    public void ShakedPendulumTestModeIsThreadPendulumOfLengthL()
    {
        var p = new ShakedPendulum { IsMainDS = true, StepWidth = 10 };
        p.SetDefaultUserData();
        var l = p.CalculationConstants![1];

        // Thread pendulum of length l: E = v^2/2 - (g/l)·cos(u)
        double Energy() => (double)(p.State.V2 * p.State.V2) / 2 - (double)(g / l) * Math.Cos((double)p.State.U2);

        var e0 = Energy();
        for (var i = 0; i < 2000; i++)
        {
            p.IterationStep(true);
        }

        // Measured relative drift 1e-9 (E0 = -13.9); with the VB error (length A instead of l) E is not conserved.
        var relativeDrift = Math.Abs((Energy() - e0) / e0);
        Assert.True(relativeDrift < 1e-8, $"relative drift {relativeDrift:E2}");
    }

    [Fact]
    public void CombinedPendulumTestModeIsSpringAndThreadPendulum()
    {
        var p = new CombinedPendulum { IsMainDS = true, StepWidth = 10 };
        p.SetDefaultUserData();
        var start = new NTuple(1);
        start[0] = 0.7m; // length l, rest length L0 = 0.5
        start[1] = 0.6m; // angle
        p.CalculationVariables = start;
        var omega = (double)p.GetAddParameterValue(p.AdditionalParameterValue);

        // Spring: E1 = v1^2/2 + Omega^2 (l - L0)^2 / 2; thread of length L0: E2 = v2^2/2 - (g/L0)·cos(Phi)
        double SpringEnergy() => (double)(p.State.V1 * p.State.V1) / 2 + omega * omega * Math.Pow((double)(p.State.U1 - 0.5m), 2) / 2;
        double ThreadEnergy() => (double)(p.State.V2 * p.State.V2) / 2 - (double)(g / 0.5m) * Math.Cos((double)p.State.U2);

        var spring0 = SpringEnergy();
        var thread0 = ThreadEnergy();
        for (var i = 0; i < 2000; i++)
        {
            p.IterationStep(true);
        }

        // Classical Runge-Kutta loses (Omega·h)^6 / 72 of the energy of a harmonic oscillator per step.
        var h = (double)p.StepWidthValue;
        var expectedLoss = 2000 * Math.Pow(omega * h, 6) / 72;
        var springLoss = (spring0 - SpringEnergy()) / spring0;
        Assert.InRange(springLoss, 0.9 * expectedLoss, 1.1 * expectedLoss);

        var threadDrift = Math.Abs((ThreadEnergy() - thread0) / thread0);
        Assert.True(threadDrift < 1e-8, $"thread relative drift {threadDrift:E2}");

        // The length really oscillates around L0 (in VB it was driven by sin(l) instead).
        Assert.InRange(p.State.U1, 0.29m, 0.71m);
    }
}
