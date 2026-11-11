using Dynamics.Core.General;

namespace Dynamics.Core.StrangeAttractor;

/// <summary>Common part of all strange attractors. Port of ClsStrangeAttractorAbstract.</summary>
public abstract class StrangeAttractorBase : IStrangeAttractor
{
    protected readonly List<PointSet> MyStartPointSets = [];
    protected readonly MathPoint MyActualMathPoint = new();

    public GeneralParameter DSParameter { get; protected init; } = null!;

    public decimal DSParameterValue { get; set; }

    public GeneralParameter XValueParameter { get; protected init; } = null!;
    public GeneralParameter YValueParameter { get; protected init; } = null!;
    public GeneralParameter ZValueParameter { get; protected init; } = null!;

    public decimal StepWidth { get; set; }

    public IReadOnlyList<PointSet> StartPointSets => MyStartPointSets;

    public Interval MathInterval { get; protected init; } = null!;

    public IReadOnlyList<decimal> Splitpoints { get; protected init; } = [];

    public MathPoint ActualMathPoint
    {
        get => MyActualMathPoint;
        set => MyActualMathPoint.CopyFrom(value);
    }

    public NTuple ViewCorrection { get; } = new(2);

    public abstract void CreateStartPointSets();

    public abstract void SetIterationParameters();

    public abstract void IterationStep();

    protected static PointSet NewPointSet(string key, decimal proposedStepWidth, params (decimal X, decimal Y, decimal Z, int Color)[] points)
    {
        var set = new PointSet(Text.Get(key)) { ProposedStepWidth = proposedStepWidth };
        foreach (var (x, y, z, color) in points)
        {
            set.AddPoint(new IterationPoint(x, y, z, color));
        }

        return set;
    }

    protected void SetViewCorrection(decimal projection3D, decimal xy, decimal xz)
    {
        ViewCorrection[0] = projection3D;
        ViewCorrection[1] = xy;
        ViewCorrection[2] = xz;
    }
}

/// <summary>
/// Base of the strange attractors defined by a differential equation, solved with the classical
/// Runge-Kutta method in Decimal. Port of ClsRungeKuttaAttractorAbstract.
/// </summary>
public abstract class RungeKuttaAttractorBase : StrangeAttractorBase
{
    private readonly NTuple _z = new(2);
    private readonly NTuple _k = new(3);
    private readonly NTuple _l = new(3);
    private readonly NTuple _m = new(3);

    public override void IterationStep()
    {
        var u = MyActualMathPoint.X;
        var v = MyActualMathPoint.Y;
        var w = MyActualMathPoint.Z;
        var h = StepWidth;

        _z[0] = u;
        _z[1] = v;
        _z[2] = w;

        // First RK step
        _k[0] = F(_z);
        _l[0] = G(_z);
        _m[0] = H(_z);

        _z[0] = u + h * _k[0] / 2;
        _z[1] = v + h * _l[0] / 2;
        _z[2] = w + h * _m[0] / 2;

        // Second RK step
        _k[1] = F(_z);
        _l[1] = G(_z);
        _m[1] = H(_z);

        _z[0] = u + h * _k[1] / 2;
        _z[1] = v + h * _l[1] / 2;
        _z[2] = w + h * _m[1] / 2;

        // Third RK step
        _k[2] = F(_z);
        _l[2] = G(_z);
        _m[2] = H(_z);

        _z[0] = u + h * _k[2];
        _z[1] = v + h * _l[2];
        _z[2] = w + h * _m[2];

        // Fourth RK step
        _k[3] = F(_z);
        _l[3] = G(_z);
        _m[3] = H(_z);

        // Final step
        u += h * (_k[0] + 2 * _k[1] + 2 * _k[2] + _k[3]) / 6;
        v += h * (_l[0] + 2 * _l[1] + 2 * _l[2] + _l[3]) / 6;
        w += h * (_m[0] + 2 * _m[1] + 2 * _m[2] + _m[3]) / 6;

        MyActualMathPoint.X = u;
        MyActualMathPoint.Y = v;
        MyActualMathPoint.Z = w;
    }

    /// <summary>Not used for Runge-Kutta attractors.</summary>
    public override void SetIterationParameters()
    {
    }

    public abstract decimal F(NTuple z);
    public abstract decimal G(NTuple z);
    public abstract decimal H(NTuple z);
}
