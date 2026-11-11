using Dynamics.Core.General;

namespace Dynamics.Core.Universe;

/// <summary>Colors of the stars (System.Drawing brushes of VB as ARGB, with the key of their name).</summary>
public static class StarColors
{
    public static readonly IReadOnlyList<(string Name, int Argb)> All =
    [
        ("Gold", unchecked((int)0xFFFFD700)),
        ("LightGray", unchecked((int)0xFFD3D3D3)),
        ("Yellow", unchecked((int)0xFFFFFF00)),
        ("SkyBlue", unchecked((int)0xFF87CEEB)),
        ("Red", unchecked((int)0xFFFF0000)),
        ("GreenYellow", unchecked((int)0xFFADFF2F)),
        ("Tomato", unchecked((int)0xFFFF6347)),
        ("Orange", unchecked((int)0xFFFFA500)),
        ("Cyan", unchecked((int)0xFF00FFFF)),
        ("AquaMarine", unchecked((int)0xFF7FFFD4)),
        ("Snow", unchecked((int)0xFFFFFAFA)),
        ("Magenta", unchecked((int)0xFFFF00FF)),
        ("LightBlue", unchecked((int)0xFFADD8E6)),
    ];

    public static int Argb(string name) => All.First(c => c.Name == name).Argb;
}

/// <summary>
/// A star or planet. Port of IStar and ClsStarAbstract; the movement of all stars is computed together by
/// <see cref="UniverseBase.IterationStep"/>; the UI draws.
/// </summary>
public abstract class StarBase
{
    // Runge-Kutta parameters: u1 = position x, v1 = velocity x, u2 = position y, v2 = velocity y
    protected decimal U1;
    protected decimal V1;
    protected decimal U2;
    protected decimal V2;

    public UniverseBase Universe { get; set; } = null!;

    /// <summary>In earth masses, set by default.</summary>
    public decimal OriginalMass { get; set; }

    /// <summary>In earth masses, set by the user.</summary>
    public decimal UserMass { get; set; }

    public Vector OriginalStartPosition { get; } = new();
    public Vector UserStartPosition { get; } = new();
    public Vector ActualPosition { get; } = new();
    public Vector NewPosition { get; } = new();
    public Vector OriginalStartVelocity { get; } = new();
    public Vector UserStartVelocity { get; } = new();
    public Vector ActualVelocity { get; } = new();

    public decimal TotalMassOfOtherStars { get; set; }

    /// <summary>Key of the color name and ARGB value.</summary>
    public string ColorName { get; set; } = "Gold";
    public int Color => StarColors.Argb(ColorName);

    public string Name { get; set; } = "";
    public int Size { get; set; }
    public decimal ActualStepWidth { get; set; }
    public decimal ProposedStepWidth { get; set; }

    /// <summary>The Runge-Kutta state (u1, v1, u2, v2).</summary>
    public (decimal U1, decimal V1, decimal U2, decimal V2) State => (U1, V1, U2, V2);

    public void ResetIteration()
    {
        ActualPosition.CopyFrom(UserStartPosition);
        U1 = ActualPosition.X;
        U2 = ActualPosition.Y;
        ActualVelocity.CopyFrom(UserStartVelocity);
        V1 = ActualVelocity.X;
        V2 = ActualVelocity.Y;
    }

    public void SetDefaultUserData()
    {
        UserStartPosition.CopyFrom(OriginalStartPosition);
        ActualPosition.CopyFrom(OriginalStartPosition);
        UserMass = OriginalMass;
        UserStartVelocity.CopyFrom(OriginalStartVelocity);
        ActualVelocity.CopyFrom(UserStartVelocity);
        TotalMassOfOtherStars = 0;
        ActualStepWidth = ProposedStepWidth;
    }

    /// <summary>VB SetUserStartPosition: the mouse position (mathematical coordinates of the diagram) divided by the zoom.</summary>
    public void SetUserStartPosition(MathPoint mouse)
    {
        UserStartPosition.X = mouse.X / Universe.DiagramZoom;
        UserStartPosition.Y = mouse.Y / Universe.DiagramZoom;
        ActualPosition.CopyFrom(UserStartPosition);
    }

    /// <summary>The position in the diagram (VB: DrawStar).</summary>
    public MathPoint DrawPosition => new(ActualPosition.X * Universe.DiagramZoom, ActualPosition.Y * Universe.DiagramZoom);

    /// <summary>
    /// Sets the result of a Runge-Kutta step (see <see cref="UniverseBase.IterationStep"/>); the velocity at the
    /// start of the step is kept for the pulse (as VB), the new position is taken over by MoveActualPosition.
    /// </summary>
    internal void SetStepResult(decimal u1, decimal v1, decimal u2, decimal v2)
    {
        ActualVelocity.X = V1;
        ActualVelocity.Y = V2;
        U1 = u1;
        V1 = v1;
        U2 = u2;
        V2 = v2;
        NewPosition.X = U1;
        NewPosition.Y = U2;
    }

    /// <summary>VB DrawPhaseportrait: ρ · zoom and |v| · VFactor.</summary>
    public MathPoint PhasePortraitPoint => new(
        (decimal)Math.Sqrt((double)(U1 * U1 + U2 * U2)) * Universe.DiagramZoom,
        (decimal)Math.Sqrt((double)(V1 * V1 + V2 * V2)) * Universe.VFactor);

    /// <summary>
    /// The acceleration of this star at (x, y) caused by the other stars at the positions xs, ys
    /// (index as in <paramref name="stars"/>; this star is skipped).
    /// </summary>
    public abstract (decimal Ax, decimal Ay) Acceleration(decimal x, decimal y, IReadOnlyList<StarBase> stars, decimal[] xs, decimal[] ys);

    /// <summary>VB MoveActualPosition: the track from the actual to the new position (drawn with width 2), then the move.</summary>
    public (MathPoint From, MathPoint To) MoveActualPosition()
    {
        var zoom = Universe.DiagramZoom;
        var track = (new MathPoint(ActualPosition.X * zoom, ActualPosition.Y * zoom), new MathPoint(NewPosition.X * zoom, NewPosition.Y * zoom));
        ActualPosition.X = NewPosition.X;
        ActualPosition.Y = NewPosition.Y;
        return track;
    }

    public decimal GetAngularMomentum() => UserMass * (U1 * V2 - U2 * V1);

    public Vector GetPulse() => new(UserMass * ActualVelocity.X, UserMass * ActualVelocity.Y);

    public abstract decimal GetEnergy();

    /// <summary>VB DrawOrbit: the points of the predicted orbit in the diagram.</summary>
    public abstract IReadOnlyList<MathPoint> GetOrbit();


    /// <summary>A copy of the star with the original and user data (VB: GetNewStar).</summary>
    public StarBase CopyAsNewStar()
    {
        var star = CreateEmpty();
        star.Name = Name;
        star.Size = Size;
        star.ColorName = ColorName;
        star.OriginalMass = OriginalMass;
        star.OriginalStartPosition.CopyFrom(OriginalStartPosition);
        star.UserStartPosition.CopyFrom(UserStartPosition);
        star.ActualPosition.CopyFrom(ActualPosition);
        star.OriginalStartVelocity.CopyFrom(OriginalStartVelocity);
        star.UserStartVelocity.CopyFrom(UserStartVelocity);
        star.ActualVelocity.CopyFrom(ActualVelocity);
        return star;
    }

    protected abstract StarBase CreateEmpty();
}

/// <summary>A star in the Newton universe. Port of ClsNewtonStar.</summary>
public class NewtonStar : StarBase
{
    private decimal _perihel;
    private decimal _argumentPerihel;
    private decimal _perihelVelocity;

    /// <summary>See mathematical documentation.</summary>
    protected virtual decimal GravConst => (decimal)(0.00000000088874 * (double)Tau * (double)Tau);

    public virtual decimal Tau => 1;

    public decimal Perihel { set => _perihel = value; }
    public decimal ArgumentPerihel { set => _argumentPerihel = value; }
    public decimal PerihelVelocity { set => _perihelVelocity = value; }

    protected override StarBase CreateEmpty() => new NewtonStar();

    /// <summary>Start position at the perihelion, velocity perpendicular to it.</summary>
    public void SetDefaultParameterByPerihelData()
    {
        var p = OriginalStartPosition;
        p.X = (decimal)((double)_perihel * Math.Cos((double)_argumentPerihel));
        p.Y = (decimal)((double)_perihel * Math.Sin((double)_argumentPerihel));
        if (p.Abs() > 0)
        {
            V1 = -_perihelVelocity * p.Y / p.Abs();
            V2 = _perihelVelocity * p.X / p.Abs();
        }
        else
        {
            V1 = _perihelVelocity * (decimal)Math.Sqrt(2);
            V2 = V1;
        }

        UserStartPosition.CopyFrom(OriginalStartPosition);
        ActualPosition.CopyFrom(OriginalStartPosition);
        OriginalStartVelocity.X = V1;
        OriginalStartVelocity.Y = V2;
        UserStartVelocity.CopyFrom(OriginalStartVelocity);
        ActualVelocity.CopyFrom(UserStartVelocity);
    }

    /// <summary>
    /// The Kepler orbit around the other stars (their total mass in the centre of gravity), as in VB DrawOrbit:
    /// points every 0.02 rad on the conic section through the start position.
    /// </summary>
    public override IReadOnlyList<MathPoint> GetOrbit()
    {
        var points = new List<MathPoint>();
        if (!(TotalMassOfOtherStars > 0 && UserStartPosition.Abs() > 0))
        {
            return points;
        }

        double others = (double)TotalMassOfOtherStars, mass = (double)UserMass, g = (double)GravConst;
        var sum = (double)(TotalMassOfOtherStars + UserMass); // VB: the sum is Decimal
        double l = (double)(UserMass * UserStartPosition.Abs() * UserStartVelocity.Abs());
        var e = (double)UserMass * Math.Pow((double)UserStartVelocity.Abs(), 2) / 2 -
                (double)(GravConst * UserMass) * Math.Pow(others, 3) / (Math.Pow(sum, 2) * (double)UserStartPosition.Abs());
        var p = Math.Pow((sum) * l, 2) / (g * Math.Pow(others, 3) * Math.Pow(mass, 2));
        var epsSquare = 1 + 2 * e * Math.Pow(l, 2) * Math.Pow(sum, 4) /
                        (Math.Pow(g, 2) * Math.Pow(mass, 3) * Math.Pow(others, 6));
        if (epsSquare < 0)
        {
            throw new ArithmeticException("NegativeSquareRoot");
        }

        var epsilon = (decimal)Math.Sqrt(epsSquare);
        var argumentStartPosition = MathHelperAngles.AngleInNullTwoPi(
            MathHelperAngles.CalculateAngleOfDirection(UserStartPosition.X, UserStartPosition.Y));
        var rho = (decimal)(p / (double)(1 + epsilon));

        var correctAlfa = UserStartVelocity.Abs() <
                          (decimal)Math.Sqrt((double)(GravConst * TotalMassOfOtherStars / UserStartPosition.Abs())) ? -1 : 1;

        decimal numCorrFactor = 0;
        if (rho != 0)
        {
            numCorrFactor = UserStartPosition.Abs() / rho;
        }

        var zoom = Universe.DiagramZoom;
        var end = argumentStartPosition + (decimal)(Math.PI - 0.01);
        for (var theta = argumentStartPosition - (decimal)(Math.PI + 0.01); theta <= end; theta += 0.02m)
        {
            rho = (decimal)(p / (1 + (double)(correctAlfa * epsilon) * Math.Cos((double)(theta - argumentStartPosition))));
            rho *= (1 + correctAlfa * epsilon) / (1 + epsilon);
            if (rho > 0)
            {
                // otherwise we are out of the definition range of e.g. the hyperbola
                points.Add(new MathPoint(
                    numCorrFactor * zoom * rho * (decimal)Math.Cos((double)theta),
                    numCorrFactor * zoom * rho * (decimal)Math.Sin((double)theta)));
            }
        }

        return points;
    }

    public override decimal GetEnergy()
    {
        var eKin = UserMass * (V1 * V1 + V2 * V2) / 2;
        var massFactor = (decimal)((double)(GravConst * UserMass) * Math.Pow((double)TotalMassOfOtherStars, 3) /
                                   Math.Pow((double)(TotalMassOfOtherStars + UserMass), 2));
        var ePot = -(decimal)((double)massFactor / Math.Max(1, Math.Sqrt((double)(U1 * U1 + U2 * U2))));
        return eKin + ePot;
    }

    /// <summary>Newton gravitation (VB G1/G2, with the same arithmetic): G · Σ m (r_other - r) / max(0.001, d³).</summary>
    public override (decimal Ax, decimal Ay) Acceleration(decimal x, decimal y, IReadOnlyList<StarBase> stars, decimal[] xs, decimal[] ys)
    {
        double sumX = 0, sumY = 0;
        for (var j = 0; j < stars.Count; j++)
        {
            if (ReferenceEquals(stars[j], this))
            {
                continue;
            }

            var distance = (decimal)Math.Sqrt(Math.Pow((double)(xs[j] - x), 2) + Math.Pow((double)(ys[j] - y), 2));
            var denominator = Math.Max(0.001, Math.Pow((double)distance, 3));
            sumX += (double)(stars[j].UserMass * (xs[j] - x)) / denominator;
            sumY += (double)(stars[j].UserMass * (ys[j] - y)) / denominator;
        }

        return ((decimal)(sumX * (double)GravConst), (decimal)(sumY * (double)GravConst));
    }
}

/// <summary>A star in the normed universe (G = 1, τ = 0.05). Port of ClsNormedStar.</summary>
public class NormedStar : NewtonStar
{
    /// <summary>VB: CDec(1 · Tau · Tau), computed in Decimal.</summary>
    protected override decimal GravConst => 1 * Tau * Tau;

    public override decimal Tau => 0.05m;

    protected override StarBase CreateEmpty() => new NormedStar();
}

/// <summary>A constellation of stars of a universe. Port of ClsStarConstellation.</summary>
public class StarConstellation(string name, decimal proposedStepWidth, decimal vFactor)
{
    private readonly List<StarBase> _stars = [];

    public string Name { get; } = name;
    public decimal ProposedStepWidth { get; } = proposedStepWidth;
    public decimal VFactor { get; } = vFactor;
    public IReadOnlyList<StarBase> Stars => _stars;

    public void AddStar(StarBase star) => _stars.Add(star);
}
