using Dynamics.Core.Universe;

namespace Dynamics.Tests.Universe;

/// <summary>
/// The corrected Runge-Kutta step of the universe (all stars together, accelerations of the stage positions).
/// VB used the actual positions in all four stages: the velocity was advanced as with Euler, order 1.
/// </summary>
public class UniverseRungeKuttaTests
{
    /// <summary>Two equal stars of the normed universe (G = τ² = 0.0025) on a circular orbit around their centre.</summary>
    private static NormedUniverse TwoStars(double stepWidth)
    {
        var universe = new NormedUniverse();

        // circular orbit: v² / r = G m / (2r)² with r = 0.5, m = 1 → v = sqrt(G / 2) / ... = sqrt(0.0025 / 2)
        var v = Math.Sqrt(0.0025 * 1 / (4 * 0.5));
        foreach (var (x, vy) in new[] { (-0.5, -v), (0.5, v) })
        {
            var star = new NormedStar { Universe = universe, OriginalMass = 1 };
            star.OriginalStartPosition.X = x;
            star.OriginalStartVelocity.Y = vy;
            star.SetDefaultUserData();
            star.ActualStepWidth = stepWidth;
            star.ResetIteration();
            universe.ActiveStarCollection.Add(star);
        }

        return universe;
    }

    private static void Run(NormedUniverse universe, int steps)
    {
        for (var i = 0; i < steps; i++)
        {
            universe.IterationStep();
            foreach (var star in universe.ActiveStarCollection)
            {
                star.MoveActualPosition();
            }
        }
    }

    /// <summary>Kinetic energy and the potential -G m1 m2 / d of the two stars.</summary>
    private static double Energy(NormedUniverse universe)
    {
        var a = universe.ActiveStarCollection[0].State;
        var b = universe.ActiveStarCollection[1].State;
        var kinetic = (a.V1 * a.V1 + a.V2 * a.V2 + b.V1 * b.V1 + b.V2 * b.V2) / 2;
        var d = Math.Sqrt(Math.Pow(a.U1 - b.U1, 2) + Math.Pow(a.U2 - b.U2, 2));
        return kinetic - 0.0025 / d;
    }

    [Fact]
    public void EnergyIsConserved()
    {
        var universe = TwoStars(1);
        var e0 = Energy(universe);
        Run(universe, 500); // more than a full revolution (period about 2π · 0.5 / v ≈ 89)

        // RK4 loses about (ωh)⁶ / 72 per step with ωh ≈ 0.07: about 1e-6 after 500 steps (measured 1.7e-6);
        // the VB scheme (same acceleration in all stages) lost 51 % (checked with a separate program).
        var drift = Math.Abs((Energy(universe) - e0) / e0);
        Assert.True(drift < 5e-6, $"relative energy drift {drift:E2}");
    }

    [Fact]
    public void CircularOrbitKeepsItsRadius()
    {
        var universe = TwoStars(0.5);
        Run(universe, 400);
        var s = universe.ActiveStarCollection[1].State;
        var r = Math.Sqrt(s.U1 * s.U1 + s.U2 * s.U2);
        Assert.Equal(0.5, r, 6);
    }

    [Fact]
    public void MethodHasOrderFour()
    {
        // The position after the time T = 80 with h, h/2 and a reference with h/16
        static double X(double h)
        {
            var universe = TwoStars(h);
            Run(universe, (int)(80 / h));
            return universe.ActiveStarCollection[1].State.U1;
        }

        var reference = X(0.25);
        var error1 = Math.Abs(X(4) - reference);
        var error2 = Math.Abs(X(2) - reference);

        // Halving h reduces the error by about 2^4 = 16 (VB scheme: 1.6, i.e. order 1)
        Assert.InRange(error1 / error2, 12, 24);
    }
}
