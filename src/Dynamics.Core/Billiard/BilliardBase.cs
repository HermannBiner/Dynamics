using System.Globalization;
using Dynamics.Core.General;

namespace Dynamics.Core.Billiard;

/// <summary>
/// Common logic of all billiard balls. Port of ClsBilliardBallAbstract plus the identical parts of the three
/// VB ball classes (iteration step, value pairs, reflection, movement on a segment, start position and angle).
/// </summary>
public abstract class BilliardBallBase : IBilliardBall
{
    /// <summary>Size of the ball in pixels (VB: Size).</summary>
    public const int BallSize = 4;

    /// <summary>Size of the point in the phase portrait (VB: p).</summary>
    public const int PhasePointSize = 2;

    private MathPoint _userStartposition = new(0, 0);
    private MathPoint _userEndposition = new(0, 0);
    private PhasePoint? _lastPhasePoint;
    private string? _lastProtocol;

    /// <summary>Parameter of the actual hit point (see mathematical documentation).</summary>
    protected double T;

    /// <summary>Angle between the actual moving direction and the positive x-axis.</summary>
    protected double MyPhi;

    protected bool MyIsProtocol;

    public double A { get; set; }
    public double B { get; set; }

    public Interval MathInterval { get; set; } = new(-1.0, 1.0);
    public Interval TValueRange { get; set; } = null!;
    public Interval AlfaValueRange { get; set; } = null!;
    public Interval ParameterRange { get; set; } = null!;

    public int Color { get; set; } = Argb.Black;
    public string ColorName { get; set; } = "Black";
    public int Speed { get; set; } = 5;
    public bool IsProtocol { set => MyIsProtocol = value; }
    public bool HasPhasePortrait { get; set; }
    public bool HasValueProtocol { get; set; } = true;

    public bool IsStartParameterSet { get; set; }
    public bool IsStartangleSet { get; set; }

    public MathPoint UserStartposition => _userStartposition;
    public MathPoint UserEndposition => _userEndposition;

    public double Phi => MyPhi;

    public double Startparameter
    {
        get => T;
        set
        {
            T = value;
            _userStartposition = PositionFromT(T);
            IsStartParameterSet = true;
        }
    }

    public double Startangle
    {
        set
        {
            // The angle between the tangent in the hit point and the positive x-axis
            var psi = MathHelperAngles.AngleInNullTwoPi(CalculatePsi(T));

            // the direction of the next movement
            MyPhi = psi + value;
            var nextT = ParameterOfNextHitPoint(T, MyPhi);
            _userEndposition = PositionFromT(nextT);
            IsStartangleSet = true;
        }
    }

    public virtual double SetUserStartposition(MathPoint mouse)
    {
        T = MathHelperAngles.CalculateAngleOfDirection(mouse.X, mouse.Y);
        _userStartposition = PositionFromT(T);
        return T;
    }

    public abstract double SetUserEndposition(MathPoint mouse);

    public abstract void SetFirstUserStartposition();

    protected void SetUserPositions(MathPoint? start, MathPoint? end)
    {
        if (start is not null)
        {
            _userStartposition = start;
        }

        if (end is not null)
        {
            _userEndposition = end;
        }
    }

    public BilliardStep IterationStep()
    {
        // Start point and end point of the actual part of the orbit
        var startpoint = PositionFromT(T);
        var nextT = ParameterOfNextHitPoint(T, MyPhi);
        var endpoint = PositionFromT(nextT);

        // The ball moves between these points
        var positions = MoveOnSegment(startpoint, endpoint);

        // The end point is the start point of the following part; the angle of the following movement
        T = nextT;
        _lastPhasePoint = null;
        _lastProtocol = null;
        MyPhi = CalculateNextPhi(T, MyPhi);
        return new BilliardStep(startpoint, endpoint, positions, _lastPhasePoint, _lastProtocol);
    }

    public DecValuePair GetNextValuePair(DecValuePair actualPair)
    {
        T = actualPair.X;
        var alfa = actualPair.Y;
        var psi = MathHelperAngles.AngleInNullTwoPi(CalculatePsi(T));
        MyPhi = psi + alfa;
        var nextT = ParameterOfNextHitPoint(T, MyPhi);
        MyPhi = CalculateNextPhi(nextT, MyPhi);
        alfa = CalculateAlfa(nextT, MyPhi);
        return new DecValuePair(nextT, alfa);
    }

    /// <summary>VB MoveOnSegment: the positions of the moving ball; small rounding effects are corrected.</summary>
    private List<MathPoint> MoveOnSegment(MathPoint start, MathPoint end)
    {
        var positions = new List<MathPoint>();

        // The step width was defined by an experiment
        var stepwide = MathInterval.IntervalWidth * Speed / 1000;
        var i = 0;
        do
        {
            i++;
            var next = new MathPoint(
                start.X + (end.X - start.X) * i * stepwide,
                start.Y + (end.Y - start.Y) * i * stepwide);
            if (!IsBallInside(next))
            {
                next = new MathPoint(end.X, end.Y);
            }

            positions.Add(next);
        }
        while (!(i * stepwide > 1));

        return positions;
    }

    /// <summary>
    /// The next direction (reflection at the tangent in t). VB wrote the phase portrait and the protocol here,
    /// with the field T (in GetNextValuePair still the old t).
    /// </summary>
    private double CalculateNextPhi(double t, double phi)
    {
        var psi = MathHelperAngles.AngleInNullTwoPi(CalculatePsi(t));
        var nextPhi = MathHelperAngles.AngleInNullTwoPi(2 * psi - phi);

        var alfa = CalculateAlfa(t, nextPhi);
        if (HasPhasePortrait)
        {
            _lastPhasePoint = new PhasePoint(T, alfa);
            if (MyIsProtocol && HasValueProtocol)
            {
                _lastProtocol = Text.Get(ColorName) + " t/alfa = " + T.ToString(CultureInfo.CurrentCulture) + "/" +
                                alfa.ToString(CultureInfo.CurrentCulture);
            }
        }

        return nextPhi;
    }

    public double CalculateAlfa(double t, double phi)
    {
        var psi = MathHelperAngles.AngleInNullTwoPi(CalculatePsi(t));
        return MathHelperAngles.AngleInNullTwoPi(phi - psi);
    }

    /// <summary>The point of the table border with parameter t.</summary>
    public abstract MathPoint PositionFromT(double t);

    /// <summary>The angle between the tangent in the hit point t and the positive x-axis.</summary>
    protected abstract double CalculatePsi(double t);

    /// <summary>The parameter of the next hit point from t in direction phi.</summary>
    protected abstract double ParameterOfNextHitPoint(double t, double phi);

    /// <summary>VB: IsBallOnEllipse / IsBallInOval / IsBallOnStadium, used to correct the last position.</summary>
    protected abstract bool IsBallInside(MathPoint position);
}

/// <summary>Common part of all billiard tables. Port of ClsBilliardTableAbstract.</summary>
public abstract class BilliardTableBase : IBilliardTable
{
    private readonly List<GeneralParameter> _valueParameters = [];

    protected double MyA;
    protected double MyB;
    protected double MyC;

    protected BilliardTableBase()
    {
        // The DS parameter is the same for all billiard tables
        DSParameter = new GeneralParameter(3, "Parameter C", new Interval(0.5, 2.0), GeneralParameter.ParameterType.DS, 0.8);
    }

    public List<IBilliardBall> BilliardBallCollection { get; } = [];

    public Interval MathInterval { get; } = new(-1.0, 1.0);

    public GeneralParameter DSParameter { get; }

    public GeneralParameter TValueParameter { get; private set; } = null!;
    public GeneralParameter AlfaValueParameter { get; private set; } = null!;
    public IReadOnlyList<GeneralParameter> ValueParameterList => _valueParameters;

    public abstract double C { get; set; }
    public double A => MyA;
    public double B => MyB;

    protected void SetValueParameters(GeneralParameter t, GeneralParameter alfa)
    {
        TValueParameter = t;
        AlfaValueParameter = alfa;
        _valueParameters.Add(t);
        _valueParameters.Add(alfa);
    }

    public void ResetIteration() => BilliardBallCollection.Clear();

    public IBilliardBall GetBilliardBall()
    {
        var ball = CreateBall();
        ball.MathInterval = MathInterval;
        ball.AlfaValueRange = AlfaValueParameter.Range;
        ball.TValueRange = TValueParameter.Range;
        ball.ParameterRange = DSParameter.Range;
        ball.A = MyA;
        ball.B = MyB;
        ball.IsStartangleSet = false;
        ball.IsStartParameterSet = false;
        return ball;
    }

    protected abstract BilliardBallBase CreateBall();

    public abstract IReadOnlyList<TableShape> DrawBilliardtable();

    /// <summary>VB DrawCoordinateSystem through (0, 0): the axes over the whole diagram.</summary>
    protected IEnumerable<TableShape> CoordinateSystem()
    {
        yield return new TableLine(new MathPoint(MathInterval.A, 0), new MathPoint(MathInterval.B, 0), Argb.Black);
        yield return new TableLine(new MathPoint(0, MathInterval.A), new MathPoint(0, MathInterval.B), Argb.Black);
    }

    protected const int Blue = unchecked((int)0xFF0000FF);
    protected const int DarkBlue = unchecked((int)0xFF00008B);
}
