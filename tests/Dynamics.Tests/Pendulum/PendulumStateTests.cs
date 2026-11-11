using Dynamics.Core.Pendulum;

namespace Dynamics.Tests.Pendulum;

/// <summary>The state the UI draws from (VB drew it directly inside the pendulum classes).</summary>
public class PendulumStateTests
{
    private static T Create<T>(PhasePortraitType type = PhasePortraitType.Independent) where T : IPendulum, new()
    {
        var p = new T { IsMainDS = true, StepWidth = 10, PhasePortraitType = type };
        p.SetDefaultUserData();
        return p;
    }

    [Fact]
    public void DoublePendulumHasTwoArmsAndTracesTheLowerBob()
    {
        var p = Create<DoublePendulum>();
        var arms = p.GetArms();

        Assert.Equal(2, arms.Count);
        Assert.Equal(DrawRole.Primary, arms[0].Role);
        Assert.Same(arms[0].To, arms[1].From);

        p.IterationStep(false);
        var (from, to) = p.GetTrackSegment();
        Assert.Same(p.GetArms()[1].To, to);
        Assert.NotEqual((from.X, from.Y), (to.X, to.Y));
    }

    [Fact]
    public void CombinedPendulumHangsAtY0AndHasEquilibriumCurve()
    {
        var p = Create<CombinedPendulum>();
        var arm = Assert.Single(p.GetArms());

        Assert.Equal(p.Y0, arm.From.Y);
        Assert.True(p.Y0 > 0);
        Assert.Equal(501, p.GetBackgroundCurve().Count);
        Assert.Empty(new DoublePendulum().GetBackgroundCurve());
    }

    [Fact]
    public void PhasePortraitOutputDependsOnType()
    {
        var independent = Create<ShakedPendulum>();
        independent.IterationStep(false);
        Assert.Equal(2, independent.GetPhasePortraitPoints().Count);
        Assert.Null(independent.GetPhasePortraitTangent());

        var torus = Create<ShakedPendulum>(PhasePortraitType.TorusOrCylinder);
        torus.IterationStep(false);
        Assert.True(Assert.Single(torus.GetPhasePortraitPoints()).IsPersistent);
        Assert.NotNull(torus.GetPhasePortraitTangent());
        Assert.Equal(-1m, torus.UInterval.A);
    }

    [Fact]
    public void PoincareSectionOnlyWhenSignChanges()
    {
        var p = Create<DoublePendulum>(PhasePortraitType.PoincareSection);
        for (var i = 0; i < 2000; i++)
        {
            p.IterationStep(false);
            Assert.Equal(p.IsSignumChanged ? 1 : 0, p.GetPhasePortraitPoints().Count);
        }
    }

    [Fact]
    public void SetAdditionalParametersResetsStartFlagsOfMainPendulumOnly()
    {
        var main = Create<DoublePendulum>();
        main.IsStartparameter1Set = true;
        main.SetAdditionalParameters();
        Assert.False(main.IsStartparameter1Set);

        var shadow = (DoublePendulum)main.GetCopy();
        shadow.SetDefaultUserData();
        shadow.IsStartparameter1Set = true;
        shadow.SetAdditionalParameters();
        Assert.True(shadow.IsStartparameter1Set);
        Assert.Null(shadow.StartEnergyRange);
    }

    [Fact]
    public void CombinedPendulumHasNoConstants() => Assert.Null(Create<CombinedPendulum>().CalculationConstants);
}
