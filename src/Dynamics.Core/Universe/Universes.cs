using Dynamics.Core.General;

namespace Dynamics.Core.Universe;

/// <summary>
/// Common part of all universes. Port of IUniverse and ClsUniverseAbstract; the diagrams are drawn by the UI.
/// </summary>
public abstract class UniverseBase
{
    private readonly List<StarConstellation> _constellations = [];
    private readonly Vector _globalCentreOfGravity = new();

    public Interval MathInterval { get; protected init; } = null!;

    /// <summary>Ranges of the phase portrait: ρ · zoom and |v| · VFactor.</summary>
    public Interval PhaseXInterval { get; protected init; } = null!;
    public Interval PhaseYInterval { get; } = new(0.0, 10.0);

    public GeneralParameter PositionParameter { get; protected init; } = null!;
    public GeneralParameter VelocityParameter { get; protected init; } = null!;
    public GeneralParameter MassParameter { get; protected init; } = null!;

    public IReadOnlyList<StarConstellation> Constellations => _constellations;

    public StarConstellation? ActiveConstellation { get; set; }

    public List<StarBase> ActiveStarCollection { get; } = [];

    /// <summary>A copy of the global centre of gravity (VB returned a new vector).</summary>
    public Vector GlobalCentreOfGravity
    {
        get
        {
            var copy = new Vector();
            copy.CopyFrom(_globalCentreOfGravity);
            return copy;
        }
    }

    public double MaxZoom { get; protected init; }

    public double DiagramZoom { get; set; } = 1;

    public double VFactor { get; set; }

    protected void AddConstellation(StarConstellation constellation) => _constellations.Add(constellation);

    public abstract void CreateConstellations();

    /// <summary>
    /// VB RedrawUniverse: moves all stars (and the new star) so that their centre of gravity is (0, 0),
    /// sets the zoom and resets the stars. The UI redraws.
    /// </summary>
    public void RedrawUniverse(StarBase? newStar, bool isZoom)
    {
        if (ActiveStarCollection.Count > 0)
        {
            var summand = new Vector();
            var newPosition = new Vector();
            var localCentreOfGravity = new Vector();
            double totalMass = 0;
            double newStarMass = 0;
            if (newStar is not null)
            {
                newStarMass = newStar.UserMass;
                totalMass = newStarMass;
            }

            foreach (var star in ActiveStarCollection)
            {
                totalMass += star.UserMass;
            }

            if (newStar is not null)
            {
                summand.CopyFrom(newStar.UserStartPosition);
                summand.Mult(-(newStarMass / totalMass));
                localCentreOfGravity.Add(summand);
                newStar.TotalMassOfOtherStars = totalMass - newStarMass;
            }

            foreach (var star in ActiveStarCollection)
            {
                summand.CopyFrom(star.UserStartPosition);
                summand.Mult(-(star.UserMass / totalMass));
                localCentreOfGravity.Add(summand);
                star.TotalMassOfOtherStars = totalMass - star.UserMass;
            }

            if (newStar is not null)
            {
                newPosition.CopyFrom(newStar.UserStartPosition);
                newPosition.Add(localCentreOfGravity);
                newStar.UserStartPosition.CopyFrom(newPosition);
            }

            foreach (var star in ActiveStarCollection)
            {
                newPosition.CopyFrom(star.UserStartPosition);
                newPosition.Add(localCentreOfGravity);
                star.UserStartPosition.CopyFrom(newPosition);
            }

            _globalCentreOfGravity.Add(localCentreOfGravity);

            double maxDistance = 0;
            if (newStar is not null)
            {
                maxDistance = newStar.UserStartPosition.Abs();
            }

            foreach (var star in ActiveStarCollection)
            {
                if (star.UserStartPosition.Abs() > maxDistance)
                {
                    maxDistance = star.UserStartPosition.Abs();
                }
            }

            DiagramZoom = maxDistance > 0 && isZoom ? MaxZoom / maxDistance : 1;
        }

        foreach (var star in ActiveStarCollection)
        {
            star.ResetIteration();
        }

        newStar?.ResetIteration();
    }

    /// <summary>
    /// One step of the classical Runge-Kutta method for all stars together; the new positions are taken over by
    /// MoveActualPosition. Corrected: VB stepped each star on its own with the accelerations of the actual positions
    /// in all four stages, so the velocity was advanced as with the Euler method: order 1 instead of 4.
    /// </summary>
    public void IterationStep()
    {
        var stars = ActiveStarCollection;
        var n = stars.Count;
        if (n == 0)
        {
            return;
        }

        // all stars have the same step width (the track bar); VB used each star's own
        var h = stars[0].ActualStepWidth;
        var u1 = new double[n];
        var v1 = new double[n];
        var u2 = new double[n];
        var v2 = new double[n];
        for (var i = 0; i < n; i++)
        {
            (u1[i], v1[i], u2[i], v2[i]) = stars[i].State;
        }

        // k: derivatives of the positions (velocities), a: derivatives of the velocities (accelerations)
        var kx = new double[4, n];
        var ky = new double[4, n];
        var ax = new double[4, n];
        var ay = new double[4, n];
        var xs = new double[n];
        var ys = new double[n];
        var vxs = new double[n];
        var vys = new double[n];

        for (var stage = 0; stage < 4; stage++)
        {
            for (var i = 0; i < n; i++)
            {
                if (stage == 0)
                {
                    (xs[i], vxs[i], ys[i], vys[i]) = (u1[i], v1[i], u2[i], v2[i]);
                }
                else if (stage < 3)
                {
                    xs[i] = u1[i] + h * kx[stage - 1, i] / 2;
                    vxs[i] = v1[i] + h * ax[stage - 1, i] / 2;
                    ys[i] = u2[i] + h * ky[stage - 1, i] / 2;
                    vys[i] = v2[i] + h * ay[stage - 1, i] / 2;
                }
                else
                {
                    xs[i] = u1[i] + h * kx[2, i];
                    vxs[i] = v1[i] + h * ax[2, i];
                    ys[i] = u2[i] + h * ky[2, i];
                    vys[i] = v2[i] + h * ay[2, i];
                }
            }

            for (var i = 0; i < n; i++)
            {
                kx[stage, i] = vxs[i];
                ky[stage, i] = vys[i];
                (ax[stage, i], ay[stage, i]) = stars[i].Acceleration(xs[i], ys[i], stars, xs, ys);
            }
        }

        for (var i = 0; i < n; i++)
        {
            stars[i].SetStepResult(
                u1[i] + h * (kx[0, i] + 2 * kx[1, i] + 2 * kx[2, i] + kx[3, i]) / 6,
                v1[i] + h * (ax[0, i] + 2 * ax[1, i] + 2 * ax[2, i] + ax[3, i]) / 6,
                u2[i] + h * (ky[0, i] + 2 * ky[1, i] + 2 * ky[2, i] + ky[3, i]) / 6,
                v2[i] + h * (ay[0, i] + 2 * ay[1, i] + 2 * ay[2, i] + ay[3, i]) / 6);
        }
    }

    public void ClearUniverse()
    {
        DiagramZoom = 1;
        ActiveStarCollection.Clear();
        _globalCentreOfGravity.Reset();
    }

    public void ResetIteration()
    {
        foreach (var star in ActiveStarCollection)
        {
            star.ResetIteration();
        }
    }

    /// <summary>VB: the track bar of the step width (1..72) in four linear sections.</summary>
    public virtual int StepWidthToTrbValue(double stepWidth)
    {
        var h = stepWidth;
        return h switch
        {
            < 0.01 => Convert.ToInt32(1 + (h - 0.001) / 0.0005),
            < 0.1 => Convert.ToInt32(19 + (h - 0.01) / 0.005),
            < 1 => Convert.ToInt32(37 + (h - 0.1) / 0.05),
            _ => Convert.ToInt32(55 + (h - 1) / 0.5),
        };
    }

    public virtual double TrbValueToStepWidth(int value) => value switch
    {
        < 19 => 0.001 + (value - 1) * 0.0005,
        < 37 => 0.01 + (value - 19) * 0.005,
        < 55 => 0.1 + (value - 37) * 0.05,
        _ => 1 + (value - 55) * 0.5,
    };

    /// <summary>VB GetNewStar: a copy of the default star of the active constellation.</summary>
    public StarBase? GetNewStar(string name)
    {
        var template = ActiveConstellation?.Stars.FirstOrDefault(s => s.Name == name);
        if (template is null)
        {
            return null;
        }

        var star = template.CopyAsNewStar();
        star.SetDefaultUserData();
        star.Universe = this;
        star.ProposedStepWidth = template.ProposedStepWidth;
        return star;
    }
}

/// <summary>Universe with the real gravitational constant (units: earth masses, AU, days). Port of ClsNewtonUniverse.</summary>
public class NewtonUniverse : UniverseBase
{
    public NewtonUniverse()
    {
        MathInterval = new Interval(-32.0, 32.0);
        PhaseXInterval = new Interval(0.0, 30.0);
        PositionParameter = new GeneralParameter(1, "Position", new Interval(-32.0, 32.0), GeneralParameter.ParameterType.Variable);
        VelocityParameter = new GeneralParameter(2, "Velocity", new Interval(-1.0, 1.0), GeneralParameter.ParameterType.Variable);
        MassParameter = new GeneralParameter(3, "Mass", new Interval(0.05, 500000.0), GeneralParameter.ParameterType.Variable);
        MaxZoom = 25;
    }

    private NewtonStar Star(string name, string color, double mass, int size, double perihel, double argumentPerihel,
        double perihelVelocity, double proposedStepWidth)
    {
        var star = new NewtonStar
        {
            Name = name,
            ColorName = color,
            OriginalMass = mass,
            Size = size,
            Perihel = perihel,
            ArgumentPerihel = argumentPerihel,
            PerihelVelocity = perihelVelocity,
        };
        star.SetDefaultParameterByPerihelData();
        star.SetDefaultUserData();
        star.Universe = this;
        star.ProposedStepWidth = proposedStepWidth;
        return star;
    }

    /// <summary>VB: CDec(x · Tau) with Tau = 1, computed in Double.</summary>
    private static double V(double velocity) => velocity * 1.0;

    private static double Angle(double angle) => angle;

    public override void CreateConstellations()
    {
        var ourPlanetSystem = new StarConstellation(Text.Get("OurPlanetSystem"), 1.2, 500);
        var ourPlanetSystemDisturbed = new StarConstellation(Text.Get("OurPlanetSystemDisturbed"), 2, 500);
        var innerPlanets = new StarConstellation(Text.Get("InnerPlanets"), 0.05, 500);
        var innerPlanetsDisturbed = new StarConstellation(Text.Get("InnerPlanetsDisturbed"), 1, 500);
        FillOurPlanetSystem(ourPlanetSystem, ourPlanetSystemDisturbed, innerPlanets, innerPlanetsDisturbed);
        AddConstellation(ourPlanetSystem);
        AddConstellation(ourPlanetSystemDisturbed);
        AddConstellation(innerPlanets);
        AddConstellation(innerPlanetsDisturbed);

        AddConstellation(Fill3SymmetricBodies(new StarConstellation(Text.Get("3SymmetricBodies"), 1.5, 1000), false));
        AddConstellation(Fill3SymmetricBodies(new StarConstellation(Text.Get("3SymmetricBodiesDisturbed"), 2, 1000), true));
        AddConstellation(Fill4SymmetricBodies(new StarConstellation(Text.Get("4SymmetricBodies"), 3, 1000), false));
        AddConstellation(Fill4SymmetricBodies(new StarConstellation(Text.Get("4SymmetricBodiesDisturbed"), 7, 1000), true));
        AddConstellation(Fill6SymmetricBodies(new StarConstellation(Text.Get("6SymmetricBodies"), 3, 1000), false));
        AddConstellation(Fill6SymmetricBodies(new StarConstellation(Text.Get("6SymmetricBodiesDisturbed"), 4, 1000), true));
        AddConstellation(FillNetflix3Body(new StarConstellation(Text.Get("Netflix3Body"), 1.5, 1000)));
    }

    private StarConstellation Fill3SymmetricBodies(StarConstellation c, bool isDisturbed)
    {
        var sun = Text.Get("Sun");
        c.AddStar(Star(sun + " 1", "Gold", 333000, 5, 15, 0, V(0.002389), 1));
        c.AddStar(Star(sun + " 2", "Gold", 333000, 5, 15, Angle(2 * Math.PI / 3), V(0.002389), 1));
        c.AddStar(Star(sun + " 3", isDisturbed ? "GreenYellow" : "Gold", 333000, 5, isDisturbed ? 14.999999 : 15,
            Angle(4 * Math.PI / 3), V(0.002389), 1));
        return c;
    }

    private StarConstellation Fill4SymmetricBodies(StarConstellation c, bool isDisturbed)
    {
        var sun = Text.Get("Sun");
        c.AddStar(Star(sun + " 1", "Gold", 333000, 5, 20, 0, V(0.003185), 4));
        c.AddStar(Star(sun + " 2", "Gold", 333000, 5, 20, Angle(Math.PI / 2), V(0.003185), 4));
        c.AddStar(Star(sun + " 3", "Gold", 333000, 5, 20, Angle(Math.PI), V(0.003185), 4));
        c.AddStar(Star(sun + " 4", isDisturbed ? "GreenYellow" : "Gold", 333000, 5, isDisturbed ? (19.99999 * 1.0) : 20,
            Angle(3 * Math.PI / 2), 0.003185, 4));
        return c;
    }

    private StarConstellation Fill6SymmetricBodies(StarConstellation c, bool isDisturbed)
    {
        var sun = Text.Get("Sun");
        c.AddStar(Star(sun + " 1", "Gold", 165000, 5, 15, 0, V(0.003185), 1));
        c.AddStar(Star(sun + " 2", "Gold", 165000, 5, 15, Angle(Math.PI / 3), V(0.003185), 1));
        c.AddStar(Star(sun + " 3", "Gold", 165000, 5, 15, Angle(2 * Math.PI / 3), V(0.003185), 1));
        c.AddStar(Star(sun + " 4", "Gold", 165000, 5, 15, Angle(Math.PI), V(0.003185), 1));
        c.AddStar(Star(sun + " 5", "Gold", 165000, 5, 15, Angle(4 * Math.PI / 3), V(0.003185), 1));
        c.AddStar(Star(sun + " 6", isDisturbed ? "GreenYellow" : "Gold", 165000, 5, isDisturbed ? 14.999999 : 15,
            Angle(5 * Math.PI / 3), V(0.003185), 1));
        return c;
    }

    private void FillOurPlanetSystem(StarConstellation our, StarConstellation ourDisturbed, StarConstellation inner, StarConstellation innerDisturbed)
    {
        // The sun is the same star in all four constellations (as in VB)
        var sun = Star(Text.Get("Sun"), "Gold", 332943, 5, 0, 0, 0, 1);
        var mercury = Star(Text.Get("Mercury"), "LightGray", 0.055, 1, 0.3075, 1.3519, V(0.034063), 0.01);
        var venus = Star(Text.Get("Venus"), "Yellow", 0.815, 2, 0.7184, 2.2956, V(0.020364), 0.02);
        var earth = Star(Text.Get("Earth"), "SkyBlue", 0.9833, 2, 0.9833, 1.7967, V(0.017494), 0.03);
        var mars = Star(Text.Get("Mars"), "Red", 0.107, 2, 1.3814, 5.865, V(0.015305), 0.04);
        var jupiter = Star(Text.Get("Jupiter"), "GreenYellow", 317.8, 3, 4.9501, 0.2575, V(0.007924), 0.5);
        var strangeJupiter = Star(Text.Get("Jupiter"), "GreenYellow", 31800, 4, 5, 0.7, V(0.004813), 0.5);
        var saturn = Star(Text.Get("Saturn"), "Tomato", 95.2, 3, 9.0481, 1.6132, V(0.005879), 1.5);
        var uranus = Star(Text.Get("Uranus"), "Orange", 14.5, 3, 18.3755, 2.9839, V(0.004106), 3);
        var neptune = Star(Text.Get("Neptune"), "Cyan", 16.8, 3, 29.7667, 0.7849, V(0.003176), 5);

        foreach (var star in new[] { sun, venus, earth, mars, jupiter, saturn, uranus, neptune })
        {
            our.AddStar(star);
        }

        foreach (var star in new[] { sun, venus, earth, mars, strangeJupiter, saturn, uranus, neptune })
        {
            ourDisturbed.AddStar(star);
        }

        foreach (var star in new[] { sun, mercury, venus, earth, mars })
        {
            inner.AddStar(star);
        }

        foreach (var star in new[] { sun, mercury, venus, earth, mars, strangeJupiter })
        {
            innerDisturbed.AddStar(star);
        }
    }

    private StarConstellation FillNetflix3Body(StarConstellation c)
    {
        var sun = Text.Get("Sun");
        c.AddStar(Star(sun + " 1", "Gold", 333000, 5, 15, 0, V(0.002389), 1));
        c.AddStar(Star(sun + " 2", "Gold", 333000, 5, 15, Angle(2 * Math.PI / 3), V(0.002389), 1));
        c.AddStar(Star(sun + " 3", "Gold", 333000, 5, 15, Angle(4 * Math.PI / 3), V(0.002389), 1));
        c.AddStar(Star(Text.Get("Planet"), "LightBlue", 1, 4, 13, Angle(2 * Math.PI / 3), V(0.01), 1));
        return c;
    }
}

/// <summary>Normed universe (G = 1) with the periodic three body orbits. Port of ClsNormedUniverse.</summary>
public class NormedUniverse : UniverseBase
{
    private const double Tau = 0.05;

    public NormedUniverse()
    {
        MathInterval = new Interval(-2.0, 2.0);
        PhaseXInterval = new Interval(0.0, 2.0);
        PositionParameter = new GeneralParameter(1, "Position", new Interval(-2.0, 2.0), GeneralParameter.ParameterType.Variable);
        VelocityParameter = new GeneralParameter(2, "Velocity", new Interval(-1.0, 1.0), GeneralParameter.ParameterType.Variable);
        MassParameter = new GeneralParameter(3, "Mass", new Interval(0.00001, 2.0), GeneralParameter.ParameterType.Variable);
        MaxZoom = 1.5;
    }

    /// <summary>VB: CDec(factor · velocity · Tau), computed in Double.</summary>
    private static double V(double velocity) => velocity * Tau;

    public override void CreateConstellations()
    {
        // Initial velocities (vx, vy) of star 1 and 2; star 3 has -2 times them
        (string Name, double StepWidth, double Vx, double Vy)[] periodic =
        [
            ("IA1", 0.01, 0.3471168881, 0.5327249454),
            ("IA2", 0.03, 0.3068934205, 0.125506567),
            ("IA3", 0.01, 0.6150407229, 0.5226158545),
            ("IB1", 0.01, 0.4644451728, 0.3960600146),
            ("IB2", 0.01, 0.4059155671, 0.230163126),
            ("IIC1", 0.01, 0.2827020949, 0.3272089716),
        ];

        foreach (var (name, stepWidth, vx, vy) in periodic)
        {
            var c = new StarConstellation(name, stepWidth, 100);
            c.AddStar(NewStar(1, "LightBlue", 1, -1, 0, V(vx), V(vy)));
            c.AddStar(NewStar(2, "GreenYellow", 1, 1, 0, V(vx), V(vy)));
            c.AddStar(NewStar(3, "Gold", 1, 0, 0, V(-2 * vx), V(-2 * vy)));
            AddConstellation(c);
        }

        var satellite = new StarConstellation(Text.Get("Satellite"), 0.02, 100);
        satellite.AddStar(NewStar(1, "LightBlue", 1, -1, 0, 0, V(0.8)));

        // VB-compatible: ActualPosition.X = 0.5 was set before SetDefaultUserData, so the star starts at (1, 0)
        satellite.AddStar(NewStar(2, "GreenYellow", 2, 1, 0, 0, V(-0.4)));
        var satelliteStar = NewStar(3, "Gold", 0.0001, -0.8, 0, V(0 * 0.05), V(0.1));
        satelliteStar.Name = Text.Get("Satellite");
        satellite.AddStar(satelliteStar);
        AddConstellation(satellite);
    }

    private NormedStar NewStar(int number, string color, double mass, double x, double y, double vx, double vy)
    {
        var star = new NormedStar
        {
            Name = Text.Get("Star") + " " + number,
            ColorName = color,
            OriginalMass = mass,
            Size = 2,
        };
        star.OriginalStartPosition.X = x;
        star.OriginalStartPosition.Y = y;
        star.UserStartPosition.CopyFrom(star.OriginalStartPosition);
        star.ActualPosition.CopyFrom(star.OriginalStartPosition);
        star.OriginalStartVelocity.X = vx;
        star.OriginalStartVelocity.Y = vy;
        star.UserStartVelocity.CopyFrom(star.OriginalStartVelocity);
        star.SetDefaultUserData();
        star.Universe = this;
        star.ProposedStepWidth = 0.01;
        return star;
    }
}
