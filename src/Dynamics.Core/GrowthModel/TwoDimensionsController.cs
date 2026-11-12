using Dynamics.Core.General;

namespace Dynamics.Core.GrowthModel;

/// <summary>Experiment of FrmTwoDimensions.</summary>
public enum TwoDimensionsMode
{
    /// <summary>Several start points in the same cell, iterated step by step in different colors.</summary>
    Sensitivity,

    /// <summary>The start point is adapted so that the orbit reaches a target cell.</summary>
    Transitivity,
}

/// <summary>A cell of the raster (left bottom corner) with its ARGB color.</summary>
public sealed record MarkedCell(MathPoint Corner, int Color);

/// <summary>One step of the orbit: line from P to NextP; TargetCell if the target was reached.</summary>
public sealed record TwoDimensionsStep(MathPoint P, MathPoint NextP, MathPoint? TargetCell);

/// <summary>
/// UI-free logic of FrmTwoDimensions. Port of ClsTwoDimensionsController: the function is iterated in x and y
/// (power 5); a raster of 5 × 5 pixels shows the measuring accuracy of the experimenter.
/// </summary>
public sealed class TwoDimensionsController : GrowthControllerBase
{
    /// <summary>VB: iteration depth 5 is optimal due to experiments.</summary>
    public const int StandardPower = 5;

    /// <summary>VB: raster size in pixels.</summary>
    public const int RasterSize = 5;

    /// <summary>VB: in the transitivity mode the iteration stops after 100'000 steps at the latest.</summary>
    public const int MaxTransitivitySteps = 100000;

    // VB brushes: Black, Blue, Magenta, Red, YellowGreen
    public static readonly int[] ExperimentColors =
        [unchecked((int)0xFF000000), unchecked((int)0xFF0000FF), unchecked((int)0xFFFF00FF), unchecked((int)0xFFFF0000), unchecked((int)0xFF9ACD32)];

    public static readonly int Blue = unchecked((int)0xFF0000FF);
    public static readonly int Red = unchecked((int)0xFFFF0000);

    private readonly List<MarkedCell> _cells = [];
    private readonly MathPoint _iterationPoint = new(0, 0);
    private MathPoint _storedCell = new(0, 0);
    private bool _isMouseDown;
    private int _remaining;
    private bool _stopRequested;

    public TwoDimensionsController(int diagramSize) : base(includeMandelbrot: false)
    {
        DiagramSize = diagramSize;
        SelectSystem(0);
    }

    public int DiagramSize { get; }

    public TwoDimensionsMode Mode { get; private set; } = TwoDimensionsMode.Sensitivity;

    /// <summary>VB: CboExperiment, 0 .. 4.</summary>
    public int Experiment { get; private set; }

    public bool IsEditable => Status != IterationStatus.Running;

    // Text boxes
    public string Parameter { get; set; } = "";
    public string X { get; set; } = "";
    public string Y { get; set; } = "";

    /// <summary>Size of a cell in mathematical units.</summary>
    public double DeltaX { get; private set; }

    public CoordinateTransform Transform { get; private set; } = null!;

    public bool IsStartCellMarked { get; private set; }
    public bool IsTargetCellMarked { get; private set; }

    /// <summary>The definitively marked cells (start blue, target red).</summary>
    public IReadOnlyList<MarkedCell> Cells => _cells;

    /// <summary>The cell under the mouse while the button is pressed (VB drew it on the picture box).</summary>
    public MarkedCell? HoverCell { get; private set; }

    /// <summary>VB: MyBrush, the color of the orbit.</summary>
    public int Color => Mode == TwoDimensionsMode.Sensitivity ? ExperimentColors[Experiment] : Blue;

    /// <summary>VB: SetDS (InitializeMe, SetDefaultUserData, ResetIteration).</summary>
    public void SelectSystem(int index)
    {
        CreateDS(index);
        DS.Power = StandardPower;
        var range = DS.ValueParameter.Range;
        Transform = new CoordinateTransform(DiagramSize, DiagramSize, range, range);

        // VB: CDec(Rastersize / Diagramsize) is a Double division
        DeltaX = ((double)RasterSize / DiagramSize) * range.IntervalWidth;
        Mode = TwoDimensionsMode.Sensitivity;

        SetDefaultUserData();
        ResetIteration();
    }

    /// <summary>
    /// VB: SetDefaultUserData. Start point at 41.4 % / 40.7 % of the value range (VB computed it in Double and showed e.g.
    /// −0.17200000000000004 for the parabola; rounded to 6 decimals here).
    /// </summary>
    public void SetDefaultUserData()
    {
        var range = DS.ValueParameter.Range;
        Parameter = Format(DS.ChaoticParameterValue);
        X = Format(Math.Round(range.A + range.IntervalWidth * 0.414, 6));
        Y = Format(Math.Round(range.A + range.IntervalWidth * 0.407, 6));
        Experiment = 0;
    }

    /// <summary>VB: BtnDefault.</summary>
    public void DefaultUserData()
    {
        ResetIteration();
        SetDefaultUserData();
    }

    /// <summary>VB: CboExperiment → SetBrush: the next start takes the start point from the text boxes again.</summary>
    public void SetExperiment(int experiment)
    {
        Experiment = experiment;
        SetBrush();
    }

    private void SetBrush()
    {
        if (Status != IterationStatus.Running)
        {
            Status = IterationStatus.Interrupted;
        }
    }

    /// <summary>VB: OptSensitivity / OptTransitivity → SetFunction.</summary>
    public void SetMode(TwoDimensionsMode mode)
    {
        Mode = mode;
        SetBrush();
        ResetIteration();
    }

    /// <summary>VB: ResetIteration; the page redraws the raster.</summary>
    public void ResetIteration()
    {
        Status = IterationStatus.Stopped;
        IsStartCellMarked = false;
        IsTargetCellMarked = false;
        HoverCell = null;
        _isMouseDown = false;
        _cells.Clear();
    }

    /// <summary>VB: PrepareDiagram, the positions of the raster lines (parallel to both axes).</summary>
    public IReadOnlyList<double> RasterLines()
    {
        var range = DS.ValueParameter.Range;
        // VB: Do ... Loop Until i · DeltaX >= width; that drew the border line only because DeltaX was rounded down in
        // Decimal. Here the lines up to and including the border.
        var count = (int)Math.Round(range.IntervalWidth / DeltaX);
        return Enumerable.Range(0, count + 1).Select(i => range.A + (i * DeltaX)).ToList();
    }

    // SECTOR SET START AND TARGET

    /// <summary>VB: MouseDown, pixel coordinates of the picture (0 .. size).</summary>
    public void MouseDown(double pixelX, double pixelY)
    {
        if (Status is IterationStatus.Stopped or IterationStatus.Interrupted && !IsTargetCellMarked)
        {
            _isMouseDown = true;
            MouseMove(pixelX, pixelY);
        }
    }

    /// <summary>VB: MouseMove, the cell under the mouse is marked temporarily.</summary>
    public void MouseMove(double pixelX, double pixelY)
    {
        if (!_isMouseDown)
        {
            return;
        }

        // Corrected: VB moved the mouse position by (−2, −7) for the WinForms hand cursor
        var cell = CellAt(pixelX, pixelY);
        _storedCell = cell;
        if (!IsStartCellMarked)
        {
            HoverCell = new MarkedCell(cell, Blue);
        }
        else if (!IsTargetCellMarked)
        {
            HoverCell = new MarkedCell(cell, Red);
        }
    }

    /// <summary>VB: MouseUp: marks the start cell or the target cell; returns the error message.</summary>
    public string? MouseUp()
    {
        if (!_isMouseDown)
        {
            return null;
        }

        _isMouseDown = false;
        HoverCell = null;
        string? error = null;
        var midX = Math.Round(_storedCell.X + DeltaX / 2, 3);
        var midY = Math.Round(_storedCell.Y + DeltaX / 2, 3);
        if (!IsStartCellMarked)
        {
            _cells.Add(new MarkedCell(Copy(_storedCell), Blue));
            IsStartCellMarked = true;
            X = Format(midX);
            Y = Format(midY);
            if (Mode == TwoDimensionsMode.Sensitivity)
            {
                IsTargetCellMarked = true;
            }
        }
        else if (!IsTargetCellMarked)
        {
            _cells.Add(new MarkedCell(Copy(_storedCell), Red));
            IsTargetCellMarked = true;
            error = CheckUserData(out _, out var x, out var y);
            if (error is null)
            {
                // Adapt the start point for the target point
                X = Format(DS.CalculateStartValueForTargetValue(x, midX));
                Y = Format(DS.CalculateStartValueForTargetValue(y, midY));
            }
        }

        return error;
    }

    /// <summary>
    /// Left bottom corner of the cell at the pixel. Corrected: VB took Fix(x / DeltaX) · DeltaX, so for the parabola
    /// (range from −1) negative coordinates went to the wrong cell and the cells were not those of the raster.
    /// </summary>
    public MathPoint CellAt(double pixelX, double pixelY)
    {
        var range = DS.ValueParameter.Range;
        var x = range.A + (pixelX * range.IntervalWidth / DiagramSize);
        var y = range.B - (pixelY * range.IntervalWidth / DiagramSize);
        var cells = (int)Math.Ceiling(range.IntervalWidth / DeltaX) - 1;
        var kx = Math.Clamp((int)Math.Floor((x - range.A) / DeltaX), 0, cells);
        var ky = Math.Clamp((int)Math.Floor((y - range.A) / DeltaX), 0, cells);
        return new MathPoint(range.A + kx * DeltaX, range.A + ky * DeltaX);
    }

    // SECTOR ITERATION

    /// <summary>
    /// VB: StartIteration(EndOfLoop): BtnNextStep (1) and BtnNext10 (10) in the sensitivity mode, Start in the
    /// transitivity mode (up to 100'000 steps). Returns the error message; if the status is then Running, the page
    /// draws the start point and calls <see cref="Step"/>.
    /// </summary>
    public string? Begin(int endOfLoop)
    {
        if (Status == IterationStatus.Running)
        {
            return null;
        }

        string? error = null;
        if (Status is IterationStatus.Stopped or IterationStatus.Interrupted)
        {
            // New iteration, or new experiment with the start point of the text boxes.
            // Corrected: VB did not read the parameter for a new experiment; if the experiment was changed before
            // the first start, a stayed 0 and the orbit fell to a fixed point.
            error = CheckUserData(out var a, out var x, out var y);
            if (error is null)
            {
                DS.ParameterA = a;
                _iterationPoint.X = x;
                _iterationPoint.Y = y;
                Status = IterationStatus.Ready;
            }
        }

        if (Status == IterationStatus.Ready)
        {
            _remaining = Mode == TwoDimensionsMode.Transitivity ? MaxTransitivitySteps : endOfLoop;
            _stopRequested = false;
            Status = IterationStatus.Running;
        }

        return error;
    }

    /// <summary>The start point of the orbit (VB: point and circle in the color of the experiment).</summary>
    public MathPoint StartPoint => Copy(_iterationPoint);

    /// <summary>VB: BtnNext10 in the transitivity mode (Stop): the running orbit stops; otherwise a new start follows.</summary>
    public void Stop()
    {
        if (Status == IterationStatus.Running)
        {
            _stopRequested = true;
        }
        else
        {
            Status = IterationStatus.Stopped;
        }
    }

    /// <summary>One step of the running orbit (VB: IterationLoop); null when the orbit is finished.</summary>
    public TwoDimensionsStep? Step()
    {
        if (Status != IterationStatus.Running)
        {
            return null;
        }

        if (_remaining <= 0 || _stopRequested)
        {
            Status = IterationStatus.Ready;
            return null;
        }

        var p = Copy(_iterationPoint);
        var nextP = new MathPoint(DS.FN(p.X), DS.FN(p.Y));
        _iterationPoint.X = nextP.X;
        _iterationPoint.Y = nextP.Y;
        _remaining -= 1;

        MathPoint? targetCell = null;
        if (Mode == TwoDimensionsMode.Transitivity)
        {
            // Because of the limited calculation accuracy, 4 · DeltaX around the middle of the target cell
            if (Math.Abs(nextP.X - (_storedCell.X + DeltaX / 2)) <= 4 * DeltaX &&
                Math.Abs(nextP.Y - (_storedCell.Y + DeltaX / 2)) <= 4 * DeltaX)
            {
                _remaining = 0;
                targetCell = Copy(nextP);
            }
        }

        return new TwoDimensionsStep(p, nextP, targetCell);
    }

    private static MathPoint Copy(MathPoint point) => new(point.X, point.Y);

    private string? CheckUserData(out double a, out double x, out double y) => Join(
        UserDataCheck.CheckValue(Parameter, DS.DSParameter.Range, out a),
        UserDataCheck.CheckValue(X, DS.ValueParameter.Range, out x),
        UserDataCheck.CheckValue(Y, DS.ValueParameter.Range, out y));
}
