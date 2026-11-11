using System.Globalization;
using Dynamics.Core.General;

namespace Dynamics.Core.StrangeAttractor;

/// <summary>The coordinate shown in the bifurcation diagram.</summary>
public enum AttractorCoordinate
{
    X,
    Y,
    Z,
}

/// <summary>The points of one column of the bifurcation diagram: parameter value and the cycle values per start point.</summary>
public sealed record BifurcationColumn(int P, decimal ParameterValue, IReadOnlyList<(int Color, IReadOnlyList<decimal> Values)> Cycles);

/// <summary>
/// UI-free logic of FrmBifurcation (Hopf bifurcation diagram of the strange attractors).
/// Port of ClsBifurcationController: for every pixel column p the parameter is set, each start point is iterated
/// 5000 steps until the cycle is reached and the next Height / 10 + 1 values of the chosen coordinate are drawn.
/// VB computed all columns at once; here the page asks for one column after the other.
/// </summary>
public sealed class BifurcationController
{
    public const int RunTimeUntilCycle = 5000;

    private readonly List<IterationPoint> _points =
    [
        new(0.1m, 0.1m, 0.1m, AttractorColors.Blue),
        new(-0.1m, -0.1m, 0.1m, AttractorColors.Red),
    ];

    private int _p;
    private int _lengthOfCycle;

    public BifurcationController(int width, int height)
    {
        Width = width;
        Height = height;
        AttractorTypes = StrangeAttractorController.FindAttractorTypes();

        // VB: CboSystem.SelectedIndex = 1
        SelectSystem(1);
    }

    public int Width { get; }
    public int Height { get; }

    public IReadOnlyList<Type> AttractorTypes { get; }

    public IEnumerable<string> AttractorNames => AttractorTypes.Select(t => Text.GetClassName("Cls" + t.Name));

    public int SelectedIndex { get; private set; }

    public IStrangeAttractor DS { get; private set; } = null!;

    public IterationStatus Status { get; private set; } = IterationStatus.Stopped;

    public bool IsEditable => Status == IterationStatus.Stopped;

    public AttractorCoordinate Coordinate { get; private set; } = AttractorCoordinate.X;

    // Text boxes: parameter range (A) and value range (X)
    public string AMin { get; private set; } = "";
    public string AMax { get; private set; } = "";
    public string XMin { get; private set; } = "";
    public string XMax { get; private set; } = "";
    public string DeltaA { get; private set; } = "";
    public string DeltaX { get; private set; } = "";

    /// <summary>The ranges of the picture (VB: ActualParameterRange, ActualValueRange).</summary>
    public Interval ActualParameterRange { get; private set; } = null!;
    public Interval ActualValueRange { get; private set; } = null!;

    public CoordinateTransform Transform { get; private set; } = null!;

    /// <summary>Allowed ranges of the area selection (VB: DiagramAreaSelector.XRange / YRange).</summary>
    private Interval _selectorXRange = null!;
    private Interval _selectorYRange = null!;

    /// <summary>Number of computed columns.</summary>
    public int Columns => _p;

    public GeneralParameter ValueParameter => Coordinate switch
    {
        AttractorCoordinate.X => DS.XValueParameter,
        AttractorCoordinate.Y => DS.YValueParameter,
        _ => DS.ZValueParameter,
    };

    /// <summary>VB: SetDS.</summary>
    public void SelectSystem(int index)
    {
        SelectedIndex = index;
        DS = (IStrangeAttractor)Activator.CreateInstance(AttractorTypes[index])!;

        // VB: InitializeMe
        ActualParameterRange = DS.DSParameter.Range;
        Coordinate = AttractorCoordinate.X;
        SetActualValueRange(Coordinate);
        _selectorXRange = ActualParameterRange;
        UpdateTransform();

        SetDefaultUserData();
        ResetIteration();
    }

    /// <summary>VB: SetActualValueRange (OptX / OptY / OptZ).</summary>
    public void SetActualValueRange(AttractorCoordinate coordinate)
    {
        Coordinate = coordinate;
        ActualValueRange = ValueParameter.Range;
        XMax = ActualValueRange.B.ToString(CultureInfo.CurrentCulture);
        XMin = ActualValueRange.A.ToString(CultureInfo.CurrentCulture);
        _selectorYRange = ActualValueRange;
        SetDelta();
    }

    /// <summary>VB: SetDefaultUserData.</summary>
    public void SetDefaultUserData()
    {
        ActualParameterRange = DS.DSParameter.Range;
        SetActualValueRange(Coordinate);
        AMin = ActualParameterRange.A.ToString(CultureInfo.CurrentCulture);
        AMax = ActualParameterRange.B.ToString(CultureInfo.CurrentCulture);
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
            DeltaA = "Delta = " + (aMax - aMin).ToString(CultureInfo.CurrentCulture);
        }

        if (UserDataCheck.TryParse(XMax, out var xMax) && UserDataCheck.TryParse(XMin, out var xMin))
        {
            DeltaX = "Delta = " + (xMax - xMin).ToString(CultureInfo.CurrentCulture);
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
        var errorX = UserDataCheck.CheckInterval(XMin, XMax, ValueParameter.Range, out var valueRange);
        if (errorA is not null || errorX is not null)
        {
            return string.Join(Environment.NewLine, new[] { errorA, errorX }.OfType<string>());
        }

        ActualParameterRange = parameterRange!;
        ActualValueRange = valueRange!;
        UpdateTransform();

        // VB: LengthOfCycle = CInt(PicDiagram.Height / 10)
        _lengthOfCycle = Convert.ToInt32(Height / 10.0);
        Status = IterationStatus.Running;
        return null;
    }

    /// <summary>
    /// VB: IterationLoop(p) for the next column; null when all columns are computed (then the status is
    /// Stopped and the page draws the split points).
    /// </summary>
    public BifurcationColumn? NextColumn()
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
        var parameterValue = ActualParameterRange.A + (ActualParameterRange.IntervalWidth * _p / Width);
        var cycles = Compute(DS, _points, parameterValue, Coordinate, _lengthOfCycle);
        return new BifurcationColumn(_p, parameterValue, cycles);
    }

    /// <summary>
    /// The values of one column: each start point is iterated RunTimeUntilCycle - 1 steps, then
    /// lengthOfCycle + 1 values of the coordinate are recorded (VB loop counters).
    /// </summary>
    public static IReadOnlyList<(int Color, IReadOnlyList<decimal> Values)> Compute(
        IStrangeAttractor ds, IReadOnlyList<IterationPoint> points, decimal parameterValue,
        AttractorCoordinate coordinate, int lengthOfCycle)
    {
        ds.DSParameterValue = parameterValue;

        // Corrected: VB never called SetIterationParameters here, so the linear map had only zero entries.
        // For the Runge-Kutta attractors it does nothing.
        ds.SetIterationParameters();
        var result = new List<(int, IReadOnlyList<decimal>)>();
        foreach (var point in points)
        {
            ds.ActualMathPoint = point.ActualPoint;
            for (var n = 1; n <= RunTimeUntilCycle - 1; n++)
            {
                ds.IterationStep();
            }

            var values = new List<decimal>(lengthOfCycle + 1);
            for (var n = RunTimeUntilCycle; n <= RunTimeUntilCycle + lengthOfCycle; n++)
            {
                ds.IterationStep();
                values.Add(coordinate switch
                {
                    AttractorCoordinate.X => ds.ActualMathPoint.X,
                    AttractorCoordinate.Y => ds.ActualMathPoint.Y,
                    _ => ds.ActualMathPoint.Z,
                });
            }

            result.Add((point.Color, values));
        }

        return result;
    }

    /// <summary>VB: CyclePoint.Y = Max(A, Min(x, B)).</summary>
    public decimal ClampToValueRange(decimal value) => Math.Max(ActualValueRange.A, Math.Min(value, ActualValueRange.B));

    /// <summary>VB: DrawSplitPoints; green lines, the last one red, if the split point is in the parameter range.</summary>
    public IEnumerable<(decimal Value, bool IsLast)> SplitLines()
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
