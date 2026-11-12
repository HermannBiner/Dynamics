using Dynamics.Core.General;

namespace Dynamics.Core.GrowthModel;

/// <summary>The lines of one sensitivity diagram: the two orbits (red, green) or their difference (blue).</summary>
public sealed record SensitivityDrawing(IReadOnlyList<Segment> Orbit1, IReadOnlyList<Segment> Orbit2, IReadOnlyList<Segment> Difference);

/// <summary>
/// UI-free logic of FrmSensitivity. Port of ClsSensitivityController: two orbits with nearly the same start value are
/// drawn over a time axis, as single orbits or as their difference.
/// </summary>
public sealed class SensitivityController : GrowthControllerBase
{
    public const int XStretchingDefault = 2;

    private readonly List<string> _valueList1 = [];
    private readonly List<string> _valueList2 = [];

    public SensitivityController(int width, int height) : base(includeMandelbrot: false)
    {
        Width = width;
        Height = height;
        SelectSystem(0);
    }

    public int Width { get; }
    public int Height { get; }

    // Text boxes, iteration depth (1 .. 12), presentation
    public string Parameter { get; set; } = "";
    public string StartValue1 { get; set; } = "";
    public string StartValue2 { get; set; } = "";
    public string XStretching { get; set; } = "";
    public int Power { get; private set; } = 1;

    /// <summary>VB: OptDifference (checked in the designer) or OptSingleOrbit.</summary>
    public bool IsDifference { get; private set; } = true;

    public IReadOnlyList<string> ValueList1 => _valueList1;
    public IReadOnlyList<string> ValueList2 => _valueList2;

    /// <summary>Number of iteration steps (accumulates over several starts, as in VB).</summary>
    public int N { get; private set; }

    /// <summary>VB: x-axis from 1 to the width in pixels, y-axis the value range.</summary>
    public CoordinateTransform Transform { get; private set; } = null!;

    /// <summary>VB: SetDS + InitializeMe.</summary>
    public void SelectSystem(int index)
    {
        CreateDS(index);
        Power = 1;
        DS.Power = 1;
        Transform = new CoordinateTransform(Width, Height, new Interval(1.0, Width), DS.ValueParameter.Range);
        SetDefaultUserData();
        ResetIteration();
    }

    /// <summary>VB: SetDefaultUserData.</summary>
    public void SetDefaultUserData()
    {
        Parameter = Format(DS.ChaoticParameterValue);
        StartValue1 = Format(0.1);
        // VB: 0.100000000000000000000000001 (1e-27 apart in Decimal); in Double that is 0.1 itself, so 1e-12 apart
        StartValue2 = Format(0.100000000001);
        XStretching = Format(XStretchingDefault);
    }

    /// <summary>VB: BtnDefault.</summary>
    public void DefaultUserData()
    {
        ResetIteration();
        SetDefaultUserData();
    }

    /// <summary>VB: CboIterationDepth (followed by ResetIteration).</summary>
    public void SetPower(int power)
    {
        Power = power;
        ResetIteration();
    }

    /// <summary>VB: OptDifference / OptSingleOrbit (followed by ResetIteration).</summary>
    public void SetDifference(bool isDifference)
    {
        IsDifference = isDifference;
        ResetIteration();
    }

    /// <summary>VB: ResetIteration; the page clears the picture.</summary>
    public void ResetIteration()
    {
        _valueList1.Clear();
        _valueList2.Clear();
        N = 0;
    }

    /// <summary>VB: StartIteration. Only for chaotic behaviour; returns the message or the lines to draw.</summary>
    public (string? Error, SensitivityDrawing? Drawing) Start()
    {
        var error = Join(
            UserDataCheck.CheckValue(Parameter, DS.DSParameter.Range, out var a),
            UserDataCheck.CheckValue(StartValue1, DS.ValueParameter.Range, out var x1),
            UserDataCheck.CheckValue(StartValue2, DS.ValueParameter.Range, out var x2),
            UserDataCheck.CheckValue(XStretching, new Interval(1.0, 10.0), out var stretching));
        if (error is not null)
        {
            return (error, null);
        }

        DS.ParameterA = a;
        DS.Power = Power;
        if (!DS.IsBehaviourChaotic())
        {
            return (Text.Get("NonChaoticBehaviour"), null);
        }

        return (null, IterationLoop(x1, x2, Convert.ToInt32(stretching)));
    }

    /// <summary>VB: IterationLoop, one step per xStretching pixels until the right border.</summary>
    private SensitivityDrawing IterationLoop(double x1, double x2, int xStretching)
    {
        var orbit1 = new List<Segment>();
        var orbit2 = new List<Segment>();
        var difference = new List<Segment>();
        var a = DS.ValueParameter.Range.A;
        double p = 0;
        do
        {
            var next1 = DS.FN(x1);

            // Corrected: VB took FN(x1) as the next point of the second orbit as well, so the second orbit
            // jumped to the first one and the difference fell to 0 after every step.
            var next2 = DS.FN(x2);
            var p1 = new MathPoint(p * xStretching, x1);
            var nextP1 = new MathPoint((p + 1) * xStretching, next1);
            var p2 = new MathPoint(p * xStretching, x2);
            var nextP2 = new MathPoint((p + 1) * xStretching, next2);

            N += 1;
            _valueList1.Add(Format(x1));
            _valueList2.Add(Format(x2));

            if (IsDifference)
            {
                difference.Add(new Segment(
                    new MathPoint(p1.X, a + Math.Abs(p2.Y - p1.Y)),
                    new MathPoint(nextP1.X, a + Math.Abs(nextP2.Y - nextP1.Y))));
            }
            else
            {
                orbit1.Add(new Segment(p1, nextP1));
                orbit2.Add(new Segment(p2, nextP2));
            }

            x1 = next1;
            x2 = next2;
            p += 1;
        }
        while (p * xStretching < Width);

        return new SensitivityDrawing(orbit1, orbit2, difference);
    }
}
