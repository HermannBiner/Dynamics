using Dynamics.Core.General;

namespace Dynamics.Core.GrowthModel;

/// <summary>Presentation in FrmIteration.</summary>
public enum IterationPresentation
{
    /// <summary>The iteration is shown on the graph of the function (with the 45° line).</summary>
    FunctionGraph,

    /// <summary>The iteration steps on the x-axis, the iterated values on the y-axis.</summary>
    TimeAxis,
}

/// <summary>What the page draws after an iteration call.</summary>
public sealed record IterationDrawing(bool ClearPicture, IReadOnlyList<Segment> Lines, Segment? TargetLine);

/// <summary>
/// UI-free logic of FrmIteration. Port of ClsIterationController: iteration of a unimodal function on the function
/// graph (cobweb) or on a time axis, value list and protocol, start values for a protocol or a target value.
/// </summary>
public sealed class IterationController : GrowthControllerBase
{
    public const int XStretchingDefault = 2;
    public const double TargetValuePrecision = 0.001;

    private readonly List<string> _valueList = [];
    private readonly List<string> _protocol = [];
    private double _x;
    private double _targetValue;
    private bool _hasTarget;
    private int _signProtocol;

    public IterationController(int diagramSize) : base(includeMandelbrot: false)
    {
        DiagramSize = diagramSize;
        SelectSystem(0);
    }

    public int DiagramSize { get; }

    public IterationPresentation Presentation { get; private set; } = IterationPresentation.FunctionGraph;

    /// <summary>The iteration runs synchronously; the inputs stay usable (as in VB).</summary>
    public bool IsEditable => true;

    // Text boxes, track bar (1 .. 1000) and iteration depth (1 .. 8)
    public string ParameterA { get; private set; } = "";
    public string StartValue { get; set; } = "";
    public string XStretching { get; set; } = "";
    public string TargetValue { get; set; } = "";
    public string TargetProtocol { get; set; } = "";
    public int TrackBar { get; private set; }
    public int Power { get; private set; } = 1;

    public IReadOnlyList<string> ValueList => _valueList;
    public IReadOnlyList<string> Protocol => _protocol;

    /// <summary>Number of iteration steps.</summary>
    public int N { get; private set; }

    public CoordinateTransform Transform { get; private set; } = null!;

    /// <summary>VB: SetDS (InitializeMe, SetDefaultUserData, ResetIteration).</summary>
    public void SelectSystem(int index)
    {
        CreateDS(index);
        var range = DS.ValueParameter.Range;
        Transform = new CoordinateTransform(DiagramSize, DiagramSize, range, range);
        _signProtocol = Math.Sign(DS.ChaoticParameterValue);
        Power = 1;
        Presentation = IterationPresentation.FunctionGraph;
        XStretching = Format(XStretchingDefault);

        SetDefaultUserData();
        ResetIteration();
    }

    /// <summary>VB: SetDefaultUserData.</summary>
    public void SetDefaultUserData()
    {
        StartValue = Format(DS.ValueParameter.DefaultValue);
        XStretching = Format(XStretchingDefault);
        TargetValue = "0";
        _targetValue = 0;
        _hasTarget = false;
        TargetProtocol = "";
        SetPower(1);

        DS.ParameterA = DS.DSParameter.DefaultValue;
        TrackBar = TrackBarOf(DS.ParameterA);
        ParameterA = Format(DS.DSParameter.DefaultValue);
    }

    /// <summary>VB: BtnDefault.</summary>
    public void DefaultUserData()
    {
        SetDefaultUserData();
        ResetIteration();
    }

    /// <summary>VB: ResetIteration; the page redraws the picture (PrepareDiagram).</summary>
    public void ResetIteration()
    {
        _valueList.Clear();
        _protocol.Clear();
        N = 0;
        Status = IterationStatus.Stopped;
    }

    /// <summary>VB: SetPresentation (OptFunctionGraph / OptTimeAxis), followed by ResetIteration.</summary>
    public void SetPresentation(IterationPresentation presentation)
    {
        Presentation = presentation;
        XStretching = Format(XStretchingDefault);
        ResetIteration();
    }

    /// <summary>VB: TrbParameterA_Scroll → SetParameterA; the page redraws the function graph.</summary>
    public void SetTrackBar(int value)
    {
        TrackBar = value;
        DS.ParameterA = ParameterOf(value);
        ParameterA = Format(DS.ParameterA);
    }

    /// <summary>VB: TxtParameterA_LostFocus → SetTrbA. Returns the error message.</summary>
    public string? SetParameterA(string text)
    {
        ParameterA = text;
        var error = CheckUserData(out var a, out _, out _, out _);
        if (error is null)
        {
            DS.ParameterA = a;
            TrackBar = TrackBarOf(a);
        }

        return error;
    }

    /// <summary>VB: CboIterationDepth → SetExponent; the page redraws the function graph.</summary>
    public void SetPower(int power)
    {
        Power = power;
        DS.Power = power;
    }

    /// <summary>The coordinate axes through (0, 0) and, on the function graph, the 45° line (VB: DrawCoordinateSystem).</summary>
    public IReadOnlyList<Segment> CoordinateSystem()
    {
        var range = DS.ValueParameter.Range;
        var lines = new List<Segment>();
        if (range.IsNumberInInterval(0.0))
        {
            lines.Add(new Segment(new MathPoint(range.A, 0), new MathPoint(range.B, 0)));
            lines.Add(new Segment(new MathPoint(0, range.A), new MathPoint(0, range.B)));
        }

        if (Presentation == IterationPresentation.FunctionGraph)
        {
            lines.Add(new Segment(new MathPoint(range.A, range.A), new MathPoint(range.B, range.B)));
        }

        return lines;
    }

    /// <summary>VB: DrawFunctionGraph, the graph of FN as a polyline with one point per pixel.</summary>
    public IReadOnlyList<MathPoint> FunctionGraph()
    {
        var range = DS.ValueParameter.Range;
        var deltaX = range.IntervalWidth / DiagramSize;
        var points = new List<MathPoint>(DiagramSize + 2);
        var x = range.A;
        points.Add(new MathPoint(x, DS.FN(x)));
        for (var m = 0; m <= DiagramSize + 2 && x < range.B; m++)
        {
            x = Math.Min(x + deltaX, range.B);
            points.Add(new MathPoint(x, DS.FN(x)));
        }

        return points;
    }

    /// <summary>
    /// VB: StartIteration(EndOfLoop), BtnNextStep (1), BtnNext10Steps (10) or, on the time axis, the next diagram.
    /// Returns the error message or what to draw.
    /// </summary>
    public (string? Error, IterationDrawing? Drawing) Iterate(int endOfLoop)
    {
        DS.Power = Power;
        var clearPicture = false;
        if (Status == IterationStatus.Stopped)
        {
            var error = CheckUserData(out var a, out var x, out _, out _);
            if (error is not null)
            {
                return (error, null);
            }

            DS.ParameterA = a;
            _x = x;
            Status = IterationStatus.Ready;
            clearPicture = true;
        }

        if (Status != IterationStatus.Ready)
        {
            return (null, null);
        }

        IterationDrawing drawing;
        if (Presentation == IterationPresentation.FunctionGraph)
        {
            drawing = new IterationDrawing(clearPicture, IterationLoop(endOfLoop), null);
        }
        else
        {
            // A new diagram, continuing the orbit
            ResetIteration();
            drawing = ShowFullDiagram();
        }

        Status = IterationStatus.Ready;
        return (null, drawing);
    }

    /// <summary>VB: IterationLoop, the cobweb between the 45° line and the function graph.</summary>
    private List<Segment> IterationLoop(int endOfLoop)
    {
        var lines = new List<Segment>(2 * endOfLoop);
        var p = new MathPoint(_x, DS.FN(_x));
        var g = new MathPoint(_x, _x);
        for (var i = 0; i < endOfLoop; i++)
        {
            N += 1;
            UpdateLists(_x);

            // 45° line → function graph → 45° line
            lines.Add(new Segment(Copy(g), Copy(p)));
            g.X = DS.FN(_x);
            g.Y = g.X;
            lines.Add(new Segment(Copy(p), Copy(g)));

            _x = DS.FN(_x);
            p.X = _x;
            p.Y = DS.FN(_x);
        }

        return lines;
    }

    /// <summary>VB: ShowFullDiagram, the orbit over the time axis until the right border (or the target value).</summary>
    private IterationDrawing ShowFullDiagram()
    {
        var range = DS.ValueParameter.Range;

        // Corrected: VB drew the target line for a target value ≠ 0 but stopped only for a target value > 0
        // (never for the negative values of the parabola).
        Segment? targetLine = _hasTarget
            ? new Segment(new MathPoint(range.A, _targetValue), new MathPoint(range.B, _targetValue))
            : null;

        var stretching = Convert.ToInt32(UserDataCheck.TryParse(XStretching, out var s) ? s : XStretchingDefault);
        var u = range.A;
        var deltaU = range.IntervalWidth * stretching / DiagramSize;
        var p = new MathPoint(u, _x);
        var nextP = new MathPoint(u + deltaU, DS.FN(_x));
        var lines = new List<Segment>();
        do
        {
            N += 1;
            lines.Add(new Segment(Copy(p), Copy(nextP)));
            UpdateLists(_x);

            if (_hasTarget && Math.Abs(_x - _targetValue) < TargetValuePrecision)
            {
                u = range.B + 1;
            }

            p.X = nextP.X;
            p.Y = nextP.Y;
            _x = DS.FN(_x);
            u += deltaU;
            nextP.X = u;
            nextP.Y = DS.FN(_x);
        }
        while (u < range.B);

        return new IterationDrawing(true, lines, targetLine);
    }

    private static MathPoint Copy(MathPoint point) => new(point.X, point.Y);

    /// <summary>VB: UpdateListboxes; protocol 0 left of the middle of the value range, 1 otherwise.</summary>
    private void UpdateLists(double u)
    {
        var range = DS.ValueParameter.Range;
        _valueList.Add(Format(u));
        _protocol.Add(_signProtocol * u < _signProtocol * (range.A + (range.B - range.A) / 2) ? "0" : "1");
    }

    /// <summary>VB: CalculateStartvalueForProtocol. Returns the messages (errors and warnings).</summary>
    public string? CalculateStartValueForProtocol()
    {
        var error = CheckUserData(out var a, out _, out _, out _);
        if (error is not null)
        {
            return error;
        }

        ResetIteration();
        DS.ParameterA = a;
        var notChaotic = DS.IsBehaviourChaotic() ? null : Text.Get("NonChaoticBehaviour");

        // The power has to be 1 to produce the protocol
        SetPower(1);
        var tooLong = TargetProtocol.Length > 44 ? Text.Get("ProtocolTooLong") : null;
        if (!MathHelperUnimodal.CheckFormatDualNumber(TargetProtocol))
        {
            return Join(notChaotic, tooLong, Text.Get("InvalidDualNumberDigits"));
        }

        StartValue = Format(DS.CalculateStartValueForProtocol(TargetProtocol));
        return Join(notChaotic, tooLong);
    }

    /// <summary>
    /// VB: CalculateStartvalueForTargetValue: the start value is adapted minimally, so that the orbit approaches the
    /// target value. With invalid user data VB set the default user data.
    /// </summary>
    public string? CalculateStartValueForTargetValue()
    {
        var error = CheckUserData(out var a, out var x, out var target, out _);
        if (error is not null)
        {
            SetDefaultUserData();
            return error;
        }

        ResetIteration();
        DS.ParameterA = a;
        var notChaotic = DS.IsBehaviourChaotic() ? null : Text.Get("NonChaoticBehaviour");
        SetPower(1);
        _x = x;
        _targetValue = target;
        _hasTarget = true;
        StartValue = Format(DS.CalculateStartValueForTargetValue(x, target));
        return notChaotic;
    }

    /// <summary>VB: IsUserDataOK (parameter, start value, target value, stretching in [1, 10]).</summary>
    private string? CheckUserData(out double a, out double x, out double target, out double stretching)
    {
        var errorA = UserDataCheck.CheckValue(ParameterA, DS.DSParameter.Range, out a);
        var errorX = UserDataCheck.CheckValue(StartValue, DS.ValueParameter.Range, out x);
        var errorT = UserDataCheck.CheckValue(TargetValue, DS.ValueParameter.Range, out target);
        var errorS = UserDataCheck.CheckValue(XStretching, new Interval(1.0, 10.0), out stretching);
        return Join(errorA, errorX, errorT, errorS);
    }
}
