using System.Globalization;
using Dynamics.Core.General;

namespace Dynamics.Core.StrangeAttractor;

/// <summary>The projection of the diagram (VB: Opt3D, OptXY, OptXZ).</summary>
public enum AttractorProjection
{
    ThreeD,
    XY,
    XZ,
}

/// <summary>A line or a point of the coordinate cube (VB: DrawCoordinateCube); color index 0 black, 1 red.</summary>
public readonly record struct CubeLine(MathPoint A, MathPoint B, bool IsRed, int Width);

public readonly record struct CubePoint(MathPoint Point, bool IsRed, int Size);

/// <summary>A drawn point: the projected position in the diagram and its ARGB color.</summary>
public readonly record struct AttractorPoint(MathPoint Plane, int Color);

/// <summary>
/// UI-free logic of FrmStrangeAttractor. Port of ClsStrangeAttractorController: text boxes and track bars are
/// properties, the projections are computed here, the page draws the cube and the points.
/// </summary>
public sealed class StrangeAttractorController
{
    /// <summary>VB TrbStepWidth 1..9.</summary>
    public static readonly IReadOnlyList<decimal> StepWidths = [0.00001m, 0.00005m, 0.0001m, 0.0005m, 0.001m, 0.005m, 0.01m, 0.05m, 0.1m];

    public const int DSParameterTrackBarMaximum = 1000;

    private bool _isMouseDown;

    public StrangeAttractorController(int diagramSize)
    {
        DiagramSize = diagramSize;
        AttractorTypes = FindAttractorTypes();

        // VB: CboStrangeAttractor.SelectedIndex = 1
        SelectSystem(1);
    }

    /// <summary>VB found the attractors by reflection, so own classes appear automatically.</summary>
    public static IReadOnlyList<Type> FindAttractorTypes() => typeof(IStrangeAttractor).Assembly.GetTypes()
        .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IStrangeAttractor).IsAssignableFrom(t))
        .OrderBy(t => t.Name, StringComparer.Ordinal)
        .ToList();

    public int DiagramSize { get; }

    public IReadOnlyList<Type> AttractorTypes { get; }

    public IEnumerable<string> AttractorNames => AttractorTypes.Select(t => Text.GetClassName("Cls" + t.Name));

    public int SelectedIndex { get; private set; }

    public IStrangeAttractor DS { get; private set; } = null!;

    public PointSet IterationPointSet { get; private set; } = new("");

    public int SelectedPointSet { get; private set; }

    public IterationStatus Status { get; private set; } = IterationStatus.Stopped;

    /// <summary>Number of iteration steps of all points (VB: LblSteps).</summary>
    public int N { get; private set; }

    public AttractorProjection Projection { get; private set; } = AttractorProjection.ThreeD;

    public decimal Phi { get; private set; }
    public decimal Theta { get; private set; }

    public string PhiText => Phi.ToString("N5", CultureInfo.CurrentCulture);
    public string ThetaText => Theta.ToString("N5", CultureInfo.CurrentCulture);

    public CoordinateTransform Transform { get; private set; } = null!;

    // Text boxes of the start point(s), separated by "/"
    public string X { get; set; } = "";
    public string Y { get; set; } = "";
    public string Z { get; set; } = "";

    /// <summary>VB EnableTxt: only a single start point can be edited.</summary>
    public bool IsStartPointEditable => IterationPointSet.Count == 1 && IsStopped;

    public int StepWidthTrackBar { get; private set; } = 5;
    public int DSParameterTrackBar { get; private set; }
    public string DSParameterText { get; private set; } = "";
    public string StepWidthText => Text.Get("StepWidth") + ": " + StepWidths[StepWidthTrackBar - 1].ToString(CultureInfo.CurrentCulture);

    /// <summary>VB: BtnStart is enabled after TakeOverPointSet (and when interrupted).</summary>
    public bool CanStart { get; private set; }

    private bool IsStopped => Status == IterationStatus.Stopped;

    public bool CanTakeOver => IsStopped;

    public bool CanRotate => IsStopped && Projection == AttractorProjection.ThreeD;

    /// <summary>VB: SetDS.</summary>
    public void SelectSystem(int index)
    {
        SelectedIndex = index;
        DS = (IStrangeAttractor)Activator.CreateInstance(AttractorTypes[index])!;
        DS.CreateStartPointSets();
        InitializeMe();
        SetDefaultUserData();
        ResetIteration();
    }

    private void InitializeMe()
    {
        SetStartPointSet(0);
        Transform = new CoordinateTransform(DiagramSize, DiagramSize, DS.MathInterval, DS.MathInterval);
        Projection = AttractorProjection.ThreeD;
        Phi = (decimal)(-Math.PI / 15);
        Theta = (decimal)(Math.PI / 8);
    }

    /// <summary>VB: SetStartPointSet (CboStartpointSets).</summary>
    public void SetStartPointSet(int index)
    {
        SelectedPointSet = index;
        IterationPointSet = DS.StartPointSets[index];
        SetTrackBarFromStepWidth(IterationPointSet.ProposedStepWidth);
        SetTrackBarFromDSParameter(DS.DSParameterValue);

        X = string.Join("/", IterationPointSet.Points.Select(p => p.ActualPoint.X.ToString(CultureInfo.CurrentCulture)));
        Y = string.Join("/", IterationPointSet.Points.Select(p => p.ActualPoint.Y.ToString(CultureInfo.CurrentCulture)));
        Z = string.Join("/", IterationPointSet.Points.Select(p => p.ActualPoint.Z.ToString(CultureInfo.CurrentCulture)));
    }

    /// <summary>VB: SetDefaultUserData.</summary>
    public void SetDefaultUserData()
    {
        foreach (var point in IterationPointSet.Points)
        {
            point.SetDefaultUserData();
        }

        ResetIteration();
    }

    /// <summary>VB: ResetIteration. The page clears the drawing and draws the cube.</summary>
    public void ResetIteration()
    {
        N = 0;
        Status = IterationStatus.Stopped;
        foreach (var point in IterationPointSet.Points)
        {
            point.ResetIteration();
        }

        CanStart = false;
    }

    /// <summary>
    /// VB: TakeOverPointSet: a single start point is taken from the text boxes; the page draws the start points.
    /// Returns the error message or null.
    /// </summary>
    public string? TakeOverPointSet()
    {
        if (IterationPointSet.Count == 1)
        {
            // VB converted with CDec without a check (an invalid input stopped the program)
            var errors = new[]
            {
                UserDataCheck.CheckValue(X, DS.XValueParameter.Range, out var x),
                UserDataCheck.CheckValue(Y, DS.YValueParameter.Range, out var y),
                UserDataCheck.CheckValue(Z, DS.ZValueParameter.Range, out var z),
            }.OfType<string>().ToList();
            if (errors.Count > 0)
            {
                return string.Join(Environment.NewLine, errors);
            }

            var point = IterationPointSet.Points[0].ActualPoint;
            point.X = x;
            point.Y = y;
            point.Z = z;
        }

        SetStartPointSet(SelectedPointSet);
        CanStart = true;
        return null;
    }

    /// <summary>The start points to draw after TakeOverPointSet (VB: DrawStartpointSet, size 3).</summary>
    public IEnumerable<AttractorPoint> StartPoints() =>
        IterationPointSet.Points.Select(p => new AttractorPoint(Project(p.ActualPoint), p.Color));

    /// <summary>VB: SetDSParameterFromTrb (0..1000); resets the iteration.</summary>
    public void SetDSParameterFromTrackBar(int value)
    {
        DSParameterTrackBar = value;
        var range = DS.DSParameter.Range;
        DS.DSParameterValue = range.A + value * (range.B - range.A) / DSParameterTrackBarMaximum;
        DSParameterText = DS.DSParameterValue.ToString(CultureInfo.CurrentCulture);
        ResetIteration();
    }

    private void SetTrackBarFromDSParameter(decimal value)
    {
        var range = DS.DSParameter.Range;
        DSParameterTrackBar = Convert.ToInt32((value - range.A) * DSParameterTrackBarMaximum / (range.B - range.A));
        DSParameterText = value.ToString(CultureInfo.CurrentCulture);
    }

    /// <summary>VB: SetStepWidthFromTrb (1..9).</summary>
    public void SetStepWidthFromTrackBar(int value)
    {
        StepWidthTrackBar = value;
        DS.StepWidth = StepWidths[value - 1];
    }

    private void SetTrackBarFromStepWidth(decimal stepWidth)
    {
        var index = StepWidths.ToList().IndexOf(stepWidth);
        StepWidthTrackBar = index >= 0 ? index + 1 : 9;
    }

    /// <summary>VB: ShowProjectionAngles (Opt3D / OptXY / OptXZ).</summary>
    public void SetProjection(AttractorProjection projection)
    {
        Projection = projection;
        ResetIteration();
    }

    /// <summary>VB: StartIteration. Returns false if nothing starts.</summary>
    public bool Start()
    {
        if (Status == IterationStatus.Stopped)
        {
            if (!CanStart || IterationPointSet.Count == 0)
            {
                return false;
            }

            Status = IterationStatus.Ready;
            SetStepWidthFromTrackBar(StepWidthTrackBar);
            DS.SetIterationParameters();
        }

        if (Status is IterationStatus.Ready or IterationStatus.Interrupted)
        {
            Status = IterationStatus.Running;
            return true;
        }

        return false;
    }

    /// <summary>True if Start clears the drawing (VB: PicDiagram.Refresh when starting from Stopped).</summary>
    public bool IsNewStart => Status == IterationStatus.Stopped;

    /// <summary>VB: StopIteration.</summary>
    public void Stop()
    {
        if (Status == IterationStatus.Running)
        {
            Status = IterationStatus.Interrupted;
        }
    }

    /// <summary>One step of every point (VB: one pass of the For Each in IterationLoop); returns the drawn points.</summary>
    public List<AttractorPoint> Step()
    {
        var drawn = new List<AttractorPoint>(IterationPointSet.Count);
        foreach (var point in IterationPointSet.Points)
        {
            DS.ActualMathPoint = point.ActualPoint;
            DS.IterationStep();
            point.ActualPoint.CopyFrom(DS.ActualMathPoint);
            N++;
            drawn.Add(new AttractorPoint(Project(point.ActualPoint), point.Color));
        }

        return drawn;
    }

    // SECTOR MOUSE: rotation of the 3D projection

    public bool MouseDown()
    {
        if (CanRotate)
        {
            _isMouseDown = true;
        }

        return _isMouseDown;
    }

    /// <summary>VB: MouseMove; the angles follow the mouse (pixel coordinates). The page redraws the cube.</summary>
    public bool MouseMove(int pixelX, int pixelY)
    {
        if (!_isMouseDown)
        {
            return false;
        }

        // Corrected: VB moved the position by (+2, -25) for its hand cursor; in the browser the pointer is exact.
        var mouseX = pixelX;
        var mouseY = pixelY;

        var interval = DS.MathInterval;
        var planeX = Math.Min(interval.B, Math.Max(interval.A, interval.A + (interval.B - interval.A) * mouseX / DiagramSize));
        var planeY = Math.Min(interval.B, Math.Max(interval.A, interval.B - (interval.B - interval.A) * mouseY / DiagramSize));

        Phi = Math.Max((decimal)(-(Math.PI / 2)),
            Math.Min((decimal)(Math.PI / 2), (decimal)Math.PI * (planeX - interval.A) / (interval.B - interval.A) - (decimal)(Math.PI / 2)));
        Theta = Math.Min(1, Math.Max(0.01m,
            (decimal)(Math.PI / 2) * ((interval.B + 2 * interval.A - 3 * planeY) / (interval.B - interval.A))));
        return true;
    }

    public void MouseUp() => _isMouseDown = false;

    // SECTOR PROJECTIONS

    public MathPoint Project(MathPoint point) => Projection switch
    {
        AttractorProjection.ThreeD => Projection3D(point),
        AttractorProjection.XY => ProjectionXY(point),
        _ => ProjectionXZ(point),
    };

    /// <summary>VB: Projection3D, rotation by Phi and Theta in Double, converted with CDec.</summary>
    public MathPoint Projection3D(MathPoint realPoint)
    {
        double phi = (double)Phi, theta = (double)Theta;
        double x = (double)realPoint.X, y = (double)realPoint.Y, z = (double)realPoint.Z;

        var rotatedX = (decimal)(x * Math.Cos(phi) - y * Math.Sin(phi) * Math.Cos(theta) - z * Math.Sin(phi) * Math.Sin(theta));
        var rotatedZ = (decimal)((double)(-realPoint.Y) * Math.Sin(theta) + z * Math.Cos(theta));

        var interval = DS.MathInterval;
        var planePoint = new MathPoint(-rotatedX, rotatedZ);
        planePoint.X += (interval.A + interval.B) / 2;
        planePoint.Y += (interval.A * 2 + interval.B) / 3 + DS.ViewCorrection[0];
        planePoint.Y = Math.Max(interval.A, planePoint.Y);
        return planePoint;
    }

    public MathPoint ProjectionXY(MathPoint realPoint)
    {
        var interval = DS.MathInterval;
        return new MathPoint(
            realPoint.X + (interval.A + interval.B) / 2,
            realPoint.Y + (interval.A * 2 + interval.B) / 3 + DS.ViewCorrection[1]);
    }

    public MathPoint ProjectionXZ(MathPoint realPoint)
    {
        var interval = DS.MathInterval;
        return new MathPoint(
            realPoint.X + (interval.A + interval.B) / 2,
            realPoint.Z + (interval.A * 2 + interval.B) / 3 + DS.ViewCorrection[2]);
    }

    /// <summary>VB: DrawCoordinateCube; the cube in 3D, only the axes (red) in XY and XZ.</summary>
    public (List<CubeLine> Lines, List<CubePoint> Points) CoordinateCube()
    {
        var e = (decimal)((double)(DS.MathInterval.B - DS.MathInterval.A) / (2 * Math.Sqrt(2)));
        MathPoint[] c =
        [
            new(e, e, 0), new(e, -e, 0), new(-e, -e, 0), new(-e, e, 0), new(e, -e, e), new(-e, -e, e),
            new(0, 0, 0), new(e + 1, 0, 0), new(0, e + 1, 0), new(0, 0, e + 1),
        ];

        var lines = new List<CubeLine>();
        var points = new List<CubePoint>();
        if (Projection == AttractorProjection.ThreeD)
        {
            foreach (var (a, b) in new[] { (0, 1), (1, 2), (2, 3), (3, 0), (1, 4), (2, 5), (4, 5) })
            {
                lines.Add(new CubeLine(Project(c[a]), Project(c[b]), false, 1));
            }

            points.Add(new CubePoint(Project(c[0]), true, 3));
            for (var i = 1; i <= 5; i++)
            {
                points.Add(new CubePoint(Project(c[i]), false, 2));
            }

            points.Add(new CubePoint(Project(c[6]), false, 3));
        }

        for (var i = 7; i <= 9; i++)
        {
            lines.Add(new CubeLine(Project(c[6]), Project(c[i]), true, 2));
        }

        return (lines, points);
    }
}
