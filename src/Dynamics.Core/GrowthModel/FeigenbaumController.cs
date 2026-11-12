using Dynamics.Core.General;

namespace Dynamics.Core.GrowthModel;

/// <summary>One column of the Feigenbaum diagram: parameter a and the drawn values (index k = 0 .. 4 · height).</summary>
public sealed record FeigenbaumColumn(int P, double ParameterValue, IReadOnlyList<double> Values);

/// <summary>
/// UI-free logic of FrmFeigenbaum. Port of ClsFeigenbaumController: for every pixel column p the parameter a is set,
/// the critical point is iterated until the cycle is reached and the next 4 · height values are drawn.
/// VB computed all columns at once; here the page asks for one column after the other.
/// </summary>
public sealed class FeigenbaumController : GrowthControllerBase
{
    public const int RunTimeUntilCycle = 1000;

    private int _p;
    private Interval _selectorXRange = null!;
    private Interval _selectorYRange = null!;

    public FeigenbaumController(int width, int height) : base(includeMandelbrot: true)
    {
        Width = width;
        Height = height;
        SelectSystem(0);
    }

    public int Width { get; }
    public int Height { get; }

    public bool IsEditable => Status == IterationStatus.Stopped;

    /// <summary>VB: ChkColored (two colors alternating along the cycle).</summary>
    public bool IsColored { get; set; }

    /// <summary>VB: ChkSplitPoints, checked in the designer.</summary>
    public bool ShowSplitPoints { get; set; } = true;

    // Text boxes: parameter range (a) and value range (x)
    public string AMin { get; private set; } = "";
    public string AMax { get; private set; } = "";
    public string XMin { get; private set; } = "";
    public string XMax { get; private set; } = "";
    public string DeltaA { get; private set; } = "";
    public string DeltaX { get; private set; } = "";

    public Interval ActualParameterRange { get; private set; } = null!;
    public Interval ActualValueRange { get; private set; } = null!;

    public CoordinateTransform Transform { get; private set; } = null!;

    /// <summary>Number of computed columns.</summary>
    public int Columns => _p;

    /// <summary>VB: LengthOfCycle = 4 · PicDiagram.Height.</summary>
    public int LengthOfCycle => 4 * Height;

    /// <summary>VB: SetDS + InitializeMe.</summary>
    public void SelectSystem(int index)
    {
        CreateDS(index);
        DS.Power = 1;
        ActualParameterRange = DS.DSParameter.Range;
        ActualValueRange = DS.ValueParameter.Range;
        _selectorXRange = ActualParameterRange;
        _selectorYRange = ActualValueRange;
        UpdateTransform();

        SetDefaultUserData();
        ResetIteration();
    }

    /// <summary>VB: SetDefaultUserData. For a parameter range starting at 0 or more only the right half is shown.</summary>
    public void SetDefaultUserData()
    {
        ActualParameterRange = DS.DSParameter.Range;
        ActualValueRange = DS.ValueParameter.Range;
        AMin = ActualParameterRange.A >= 0
            ? Format(ActualParameterRange.A + ActualParameterRange.IntervalWidth / 2)
            : Format(ActualParameterRange.A);
        AMax = Format(ActualParameterRange.B);
        XMin = Format(ActualValueRange.A);
        XMax = Format(ActualValueRange.B);
        SetDelta();
    }

    /// <summary>VB: BtnDefault.</summary>
    public void DefaultUserData()
    {
        ResetIteration();
        SetDefaultUserData();
    }

    public void SetAMin(string text)
    {
        AMin = text;
        SetDelta();
    }

    public void SetAMax(string text)
    {
        AMax = text;
        SetDelta();
    }

    public void SetXMin(string text)
    {
        XMin = text;
        SetDelta();
    }

    public void SetXMax(string text)
    {
        XMax = text;
        SetDelta();
    }

    private void SetDelta()
    {
        if (UserDataCheck.TryParse(AMax, out var aMax) && UserDataCheck.TryParse(AMin, out var aMin))
        {
            DeltaA = "Delta = " + Format(aMax - aMin);
        }

        if (UserDataCheck.TryParse(XMax, out var xMax) && UserDataCheck.TryParse(XMin, out var xMin))
        {
            DeltaX = "Delta = " + Format(xMax - xMin);
        }
    }

    private void UpdateTransform() =>
        Transform = new CoordinateTransform(Width, Height, ActualParameterRange, ActualValueRange);

    /// <summary>VB: ResetIteration; the page clears the picture.</summary>
    public void ResetIteration()
    {
        Status = IterationStatus.Stopped;
        _p = 0;
    }

    /// <summary>VB: StartIteration (first part). Returns the error message, or null if the columns can be computed.</summary>
    public string? Start()
    {
        ResetIteration();

        var errorA = UserDataCheck.CheckInterval(AMin, AMax, DS.DSParameter.Range, out var parameterRange);
        var errorX = UserDataCheck.CheckInterval(XMin, XMax, DS.ValueParameter.Range, out var valueRange);
        if (errorA is not null || errorX is not null)
        {
            return Join(errorA, errorX);
        }

        ActualParameterRange = parameterRange!;
        ActualValueRange = valueRange!;
        UpdateTransform();
        Status = IterationStatus.Running;
        return null;
    }

    /// <summary>The next column (VB: IterationLoop(p)); null when all are computed (then the status is Stopped).</summary>
    public FeigenbaumColumn? NextColumn()
    {
        if (Status != IterationStatus.Running)
        {
            return null;
        }

        if (_p >= Width)
        {
            Status = IterationStatus.Stopped;
            return null;
        }

        _p++;
        return ComputeColumn(_p);
    }

    /// <summary>
    /// VB: IterationLoop(p). The critical point is iterated RunTimeUntilCycle − 1 times; then LengthOfCycle + 1 values
    /// are drawn, each before the next step.
    /// </summary>
    public FeigenbaumColumn ComputeColumn(int p)
    {
        DS.ParameterA = ActualParameterRange.A + ActualParameterRange.IntervalWidth * p / Width;
        var x = DS.CriticalPoint;
        for (var n = 1; n <= RunTimeUntilCycle - 1; n++)
        {
            x = DS.FN(x);
        }

        var values = new double[LengthOfCycle + 1];
        for (var k = 0; k <= LengthOfCycle; k++)
        {
            values[k] = x;
            x = DS.FN(x);
        }

        return new FeigenbaumColumn(p, DS.ParameterA, values);
    }

    /// <summary>
    /// The point VB drew for value k: the first one as it is (outside the picture it is not drawn),
    /// the following ones clamped to the value range.
    /// </summary>
    public double DrawnValue(int k, double value) =>
        k == 0 ? value : Math.Max(ActualValueRange.A, Math.Min(value, ActualValueRange.B));

    /// <summary>VB: SetColor(n) with n = RunTimeUntilCycle + k: blue for even n, green for odd n if colored.</summary>
    public bool IsGreen(int k) => IsColored && (RunTimeUntilCycle + k) % 2 == 1;

    /// <summary>VB: DrawSplitPoints; green lines, the last one (limit of the period doubling) red.</summary>
    public IEnumerable<(double Value, bool IsLast)> SplitLines()
    {
        var splitpoints = DS.Splitpoints;
        for (var i = 0; i < splitpoints.Count; i++)
        {
            if (DS.DSParameter.Range.IsNumberInInterval(splitpoints[i]) && ActualParameterRange.IsNumberInInterval(splitpoints[i]))
            {
                yield return (splitpoints[i], i == splitpoints.Count - 1);
            }
        }
    }

    /// <summary>Area selection whenever stopped (corrected selector, see the Julia sets).</summary>
    public bool SelectArea(int startX, int startY, int endX, int endY)
    {
        if (!IsEditable)
        {
            return false;
        }

        var selection = DiagramAreaSelector.Select(Transform, _selectorXRange, _selectorYRange, startX, startY, endX, endY);
        if (selection is null)
        {
            return false;
        }

        (AMin, AMax, XMin, XMax) = selection.Value;
        SetDelta();
        return true;
    }
}
