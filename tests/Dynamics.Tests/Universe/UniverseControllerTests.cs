using System.Globalization;
using Dynamics.Core.General;
using Dynamics.Core.Universe;

namespace Dynamics.Tests.Universe;

public class UniverseControllerTests
{
    public UniverseControllerTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
    }

    [Fact]
    public void ControllerStartsWithTheNewtonUniverse()
    {
        var controller = new UniverseController();
        Assert.Equal([typeof(NewtonUniverse), typeof(NormedUniverse)], controller.UniverseTypes);
        Assert.Equal(11, controller.DS.Constellations.Count);
        Assert.Empty(controller.DS.ActiveStarCollection);
        Assert.NotNull(controller.Start());
    }

    [Fact]
    public void TakeOverAndIterate()
    {
        var controller = new UniverseController { IsConservationLaws = true, IsPhasePortrait = true };
        controller.SelectConstellation(2); // inner planets
        controller.TakeOverConstellation();
        Assert.Equal(5, controller.DS.ActiveStarCollection.Count);

        // proposed step width 0.05 → track bar 27 → 0.05
        Assert.Equal(27, controller.StepWidthTrackBar);
        Assert.Equal(0.05m, controller.StepWidth);

        Assert.Null(controller.Start());
        UniverseStep? step = null;
        for (var i = 0; i < 10; i++)
        {
            step = controller.Step();
        }

        Assert.Equal(5, step!.Tracks.Count);
        Assert.Equal(5, step.PhasePoints.Count);
        Assert.Equal(ConservationState.Ok, controller.Energy.State);
        Assert.Equal(ConservationState.Ok, controller.AngularMomentum.State);
        Assert.Equal(50, controller.Steps);
    }

    [Fact]
    public void NewStarIsPlacedAndSaved()
    {
        var controller = new UniverseController();
        controller.SelectConstellation(2);
        controller.TakeOverConstellation();
        controller.SelectedDefaultStar = 3; // earth
        controller.NewStarOrCancel();
        var star = controller.NewStar!;
        Assert.Equal("SkyBlue", star.ColorName);
        Assert.Equal(3, controller.SelectedColor);

        // The mouse sets the position (divided by the zoom)
        Assert.True(controller.MouseDown(new MathPoint(10, 5)));
        Assert.Equal(10 / controller.DS.DiagramZoom, star.UserStartPosition.X);
        controller.MouseUp();

        controller.SetMassFromTrackBar(10);
        Assert.Equal(star.OriginalMass * 1.2m, star.UserMass);
        controller.SetVelocityFromTrackBar(0);
        Assert.Equal(star.OriginalStartVelocity.X, star.UserStartVelocity.X);

        Assert.Null(controller.SaveStar());
        Assert.Null(controller.NewStar);
        Assert.Equal(6, controller.DS.ActiveStarCollection.Count);
    }

    [Fact]
    public void CancelDiscardsTheNewStar()
    {
        var controller = new UniverseController();
        controller.TakeOverConstellation();
        controller.NewStarOrCancel();
        Assert.NotNull(controller.NewStar);
        controller.NewStarOrCancel();
        Assert.Null(controller.NewStar);
        Assert.Equal(8, controller.DS.ActiveStarCollection.Count);
    }

    [Fact]
    public void StepWidthTrackBarAsInVB()
    {
        var universe = new NewtonUniverse();

        // 1.2 is not on the scale: CInt(55.4) = 55 → 1 (VB, the planet system runs with step width 1)
        Assert.Equal(55, universe.StepWidthToTrbValue(1.2m));
        Assert.Equal(1m, universe.TrbValueToStepWidth(55));
        Assert.Equal(0.001m, universe.TrbValueToStepWidth(1));
        Assert.Equal(9.5m, universe.TrbValueToStepWidth(72));
    }

    [Fact]
    public void OrbitOfTheEarthIsAnEllipseThroughItsStart()
    {
        // The earth around the sun: the orbit goes through the start (perihelion) and is nearly a circle
        var universe = new NewtonUniverse();
        universe.CreateConstellations();
        universe.ActiveConstellation = universe.Constellations[2];
        foreach (var s in universe.ActiveConstellation.Stars)
        {
            s.SetDefaultUserData();
            universe.ActiveStarCollection.Add(s);
        }

        universe.RedrawUniverse(null, false);
        var earth = universe.ActiveStarCollection[3];
        var orbit = earth.GetOrbit();
        Assert.NotEmpty(orbit);

        var start = earth.UserStartPosition;
        var nearest = orbit.Min(p => Math.Sqrt(Math.Pow((double)(p.X - start.X), 2) + Math.Pow((double)(p.Y - start.Y), 2)));
        Assert.True(nearest < 0.02, $"nearest orbit point {nearest}");

        var radii = orbit.Select(p => Math.Sqrt((double)(p.X * p.X + p.Y * p.Y))).ToList();
        Assert.InRange(radii.Min(), 0.97, 1.0);
        Assert.InRange(radii.Max(), 1.0, 1.03);
    }
}
