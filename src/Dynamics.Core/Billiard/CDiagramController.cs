using System.Globalization;
using Dynamics.Core.General;

namespace Dynamics.Core.Billiard;

/// <summary>One column of the C-diagram: the parameter c and the iterated pairs (t, alfa).</summary>
public sealed record CDiagramColumn(int P, decimal C, IReadOnlyList<DecValuePair> Pairs);

/// <summary>
/// UI-free logic of FrmCDiagramBilliard: for every pixel column the table parameter c is set and Height + 1 pairs
/// (t, alfa) are iterated; the chosen value is drawn. Port of ClsCDiagramController; VB computed all columns at once,
/// here the page asks for one column after the other.
/// </summary>
public sealed class CDiagramController
{
    public const int PositionMaximum = 119;

    private IBilliardBall _ball = null!;
    private int _p;
    private Interval _selectorXRange = null!;

    public CDiagramController(int width, int height)
    {
        Width = width;
        Height = height;
        TableTypes = BilliardTableController.FindTableTypes();

        // VB: CboFunction.SelectedIndex = 1
        SelectTable(1);
    }

    public int Width { get; }
    public int Height { get; }

    public IReadOnlyList<Type> TableTypes { get; }

    public IEnumerable<string> TableNames => TableTypes.Select(t => Text.GetClassName("Cls" + t.Name));

    public int SelectedIndex { get; private set; }

    public IBilliardTable DS { get; private set; } = null!;

    public IterationStatus Status { get; private set; } = IterationStatus.Stopped;

    public bool IsEditable => Status == IterationStatus.Stopped;

    /// <summary>Index into DS.ValueParameterList (VB CboValueParameter, default the last one: alfa).</summary>
    public int SelectedValueParameter { get; private set; }

    public GeneralParameter ValueParameter => DS.ValueParameterList[SelectedValueParameter];

    public string CMin { get; private set; } = "";
    public string CMax { get; private set; } = "";
    public string VMin { get; private set; } = "";
    public string VMax { get; private set; } = "";
    public string DeltaC { get; private set; } = "";
    public string DeltaV { get; private set; } = "";

    /// <summary>VB TrbPositionStartValues (1..119): alfa of the start = A + value · width / 120.</summary>
    public int Position { get; set; } = 60;

    public Interval ActualParameterRange { get; private set; } = null!;
    public Interval ActualValueRange { get; private set; } = null!;

    public CoordinateTransform Transform { get; private set; } = null!;

    public int Columns => _p;

    /// <summary>VB: SetDS.</summary>
    public void SelectTable(int index)
    {
        SelectedIndex = index;
        DS = (IBilliardTable)Activator.CreateInstance(TableTypes[index])!;

        // VB: InitializeMe
        SelectedValueParameter = DS.ValueParameterList.Count - 1;
        _ball = DS.GetBilliardBall();
        ActualParameterRange = DS.DSParameter.Range;
        ActualValueRange = ValueParameter.Range;
        _selectorXRange = ActualParameterRange;
        UpdateTransform();

        SetDefaultUserData();
        ResetIteration();
    }

    /// <summary>VB: SetValueParameter (CboValueParameter).</summary>
    public void SetValueParameter(int index)
    {
        SelectedValueParameter = index;
        ActualValueRange = ValueParameter.Range;
        UpdateTransform();
        ResetIteration();
        SetDefaultUserData();
    }

    /// <summary>VB: SetDefaultUserData.</summary>
    public void SetDefaultUserData()
    {
        ActualParameterRange = DS.DSParameter.Range;
        ActualValueRange = ValueParameter.Range;
        CMin = ActualParameterRange.A.ToString(CultureInfo.CurrentCulture);
        CMax = ActualParameterRange.B.ToString(CultureInfo.CurrentCulture);
        VMin = ActualValueRange.A.ToString(CultureInfo.CurrentCulture);
        VMax = ActualValueRange.B.ToString(CultureInfo.CurrentCulture);
        SetDelta();
        Position = 60;
    }

    /// <summary>VB: BtnDefault.</summary>
    public void DefaultUserData()
    {
        ResetIteration();
        SetDefaultUserData();
    }

    public void SetCMin(string text)
    {
        CMin = text;
        SetDelta();
    }

    public void SetCMax(string text)
    {
        CMax = text;
        SetDelta();
    }

    public void SetVMin(string text)
    {
        VMin = text;
        SetDelta();
    }

    public void SetVMax(string text)
    {
        VMax = text;
        SetDelta();
    }

    private void SetDelta()
    {
        if (UserDataCheck.TryParse(CMax, out var cMax) && UserDataCheck.TryParse(CMin, out var cMin))
        {
            DeltaC = "Delta = " + (cMax - cMin).ToString(CultureInfo.CurrentCulture);
        }

        if (UserDataCheck.TryParse(VMax, out var vMax) && UserDataCheck.TryParse(VMin, out var vMin))
        {
            DeltaV = "Delta = " + (vMax - vMin).ToString(CultureInfo.CurrentCulture);
        }
    }

    private void UpdateTransform() => Transform = new CoordinateTransform(Width, Height, ActualParameterRange, ActualValueRange);

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
        var errorC = UserDataCheck.CheckInterval(CMin, CMax, DS.DSParameter.Range, out var parameterRange);
        var errorV = UserDataCheck.CheckInterval(VMin, VMax, ValueParameter.Range, out var valueRange);
        if (errorC is not null || errorV is not null)
        {
            return string.Join(Environment.NewLine, new[] { errorC, errorV }.OfType<string>());
        }

        ActualParameterRange = parameterRange!;
        ActualValueRange = valueRange!;
        UpdateTransform();
        Status = IterationStatus.Running;
        return null;
    }

    /// <summary>VB: IterationLoop(p) for the next column; null when all columns are computed (then Stopped).</summary>
    public CDiagramColumn? NextColumn()
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
        return ComputeColumn(DS, _ball, ActualParameterRange, _p, Width, Height, Position);
    }

    /// <summary>The value drawn for a pair: t (parameter ID 1) or alfa.</summary>
    public decimal DrawnValue(DecValuePair pair) => ValueParameter.ID == 1 ? pair.X : pair.Y;

    /// <summary>
    /// VB IterationLoop: c of column p, start pair (t = A + width / 3, alfa = A + position · width / 120),
    /// then Height + 1 pairs (each drawn before the next is computed).
    /// </summary>
    public static CDiagramColumn ComputeColumn(IBilliardTable table, IBilliardBall ball, Interval parameterRange,
        int p, int width, int height, int position)
    {
        table.C = parameterRange.A + (parameterRange.IntervalWidth * p / width);
        ball.A = table.A;
        ball.B = table.B;

        var tRange = table.ValueParameterList[0].Range;
        var alfaRange = table.ValueParameterList[1].Range;
        var pair = new DecValuePair(tRange.A + tRange.IntervalWidth / 3, alfaRange.A + position * alfaRange.IntervalWidth / 120);

        var pairs = new List<DecValuePair>(height + 1);
        for (var n = 0; n <= height; n++)
        {
            pairs.Add(new DecValuePair(pair.X, pair.Y));
            pair = ball.GetNextValuePair(pair);
        }

        return new CDiagramColumn(p, table.C, pairs);
    }

    /// <summary>Area selection whenever stopped (corrected selector, see the Julia sets).</summary>
    public bool SelectArea(int startX, int startY, int endX, int endY)
    {
        if (!IsEditable)
        {
            return false;
        }

        var selection = DiagramAreaSelector.Select(Transform, _selectorXRange, ValueParameter.Range, startX, startY, endX, endY);
        if (selection is null)
        {
            return false;
        }

        (CMin, CMax, VMin, VMax) = selection.Value;
        SetDelta();
        return true;
    }
}
