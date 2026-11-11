using System.Diagnostics;
using System.Globalization;
using Dynamics.Core.General;
using Dynamics.Core.Julia;

namespace Dynamics.Core.Newton;

/// <summary>One computed pixel in VB pixel coordinates.</summary>
public readonly record struct NewtonStep(int P, int Q, NewtonPixel Pixel);

/// <summary>
/// UI-free logic of FrmNewtonIteration. Port of ClsNewtonIterationController: the text boxes are string
/// properties, messages are returned instead of shown, the page draws the pixels returned by <see cref="Step"/>
/// and the roots of <see cref="INewton.Roots"/>. The pixels are visited by the same spiral as the Julia sets.
/// </summary>
public sealed class NewtonController
{
    public static readonly IReadOnlyList<int> Exponents = [2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12];

    /// <summary>The protocol has a line per pixel; only the newest lines are kept.</summary>
    public const int MaxProtocolLines = 10000;

    private readonly Stopwatch _watch = new();
    private IEnumerator<(int P, int Q)>? _spiral;

    public NewtonController(int diagramSize)
    {
        DiagramSize = diagramSize;

        // VB found the polynomials by reflection, so own classes appear automatically.
        NewtonTypes = typeof(INewton).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(INewton).IsAssignableFrom(t))
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ToList();

        // VB: the last entry is selected
        SelectSystem(NewtonTypes.Count - 1);
    }

    public int DiagramSize { get; }

    public IReadOnlyList<Type> NewtonTypes { get; }

    public IEnumerable<string> NewtonNames => NewtonTypes.Select(t => Text.GetClassName("Cls" + t.Name));

    public int SelectedIndex { get; private set; }

    public INewton DS { get; private set; } = null!;

    public IterationStatus Status { get; private set; } = IterationStatus.Stopped;

    /// <summary>VB SetControlsEnabled: the inputs are enabled only when stopped.</summary>
    public bool IsEditable => Status is IterationStatus.Stopped;

    /// <summary>Selection of an area with the mouse whenever stopped (corrected as for the Julia sets).</summary>
    public bool IsAreaSelectable => IsEditable;

    // The text boxes and options
    public int N { get; private set; } = Exponents[1];
    public string XMin { get; private set; } = "";
    public string XMax { get; private set; } = "";
    public string YMin { get; private set; } = "";
    public string YMax { get; private set; } = "";
    public string A { get; private set; } = "";
    public string B { get; private set; } = "";
    public string DeltaX { get; private set; } = "";
    public string DeltaY { get; private set; } = "";
    public NewtonMixing Mixing { get; private set; } = NewtonMixing.None;
    public NewtonColor Color { get; set; } = NewtonColor.Shadowed;
    public bool IsProtocol { get; set; }

    /// <summary>Start point and end point of each pixel (VB: LstProtocol).</summary>
    public List<string> Protocol { get; } = [];

    /// <summary>Number of computed pixels (VB: TxtSteps).</summary>
    public int Steps { get; private set; }

    public TimeSpan Elapsed => _watch.Elapsed;

    /// <summary>True when the roots are drawn black (VB: DrawRoots(True) at the end of PerformIteration).</summary>
    public bool IsFinished { get; private set; }

    /// <summary>VB: SetDS.</summary>
    public void SelectSystem(int index)
    {
        SelectedIndex = index;
        DS = (INewton)Activator.CreateInstance(NewtonTypes[index])!;
        InitializeMe();
        SetDefaultUserData();
        ResetIteration();
    }

    /// <summary>VB: InitializeMe.</summary>
    private void InitializeMe()
    {
        DS.DiagramSize = DiagramSize;

        // VB: CboN.SelectedIndex = 1
        N = Exponents[1];
        DS.ActualXRange = DS.XValueParameter.Range;
        DS.ActualYRange = DS.YValueParameter.Range;
        SetExponent(N);
    }

    /// <summary>VB: SetDefaultUserData.</summary>
    public void SetDefaultUserData()
    {
        XMin = DS.XValueParameter.Range.A.ToString(CultureInfo.CurrentCulture);
        XMax = DS.XValueParameter.Range.B.ToString(CultureInfo.CurrentCulture);
        YMin = DS.YValueParameter.Range.A.ToString(CultureInfo.CurrentCulture);
        YMax = DS.YValueParameter.Range.B.ToString(CultureInfo.CurrentCulture);

        if (DS.IsUseC)
        {
            A = "0.5";
            B = "1";
        }

        SetDelta();
    }

    /// <summary>VB: BtnDefault.</summary>
    public void DefaultUserData()
    {
        ResetIteration();
        SetDefaultUserData();
    }

    private void SetDelta()
    {
        if (UserDataCheck.TryParse(XMax, out var xMax) && UserDataCheck.TryParse(XMin, out var xMin))
        {
            DeltaX = "Delta = " + (xMax - xMin).ToString(CultureInfo.CurrentCulture);
        }

        if (UserDataCheck.TryParse(YMax, out var yMax) && UserDataCheck.TryParse(YMin, out var yMin))
        {
            DeltaY = "Delta = " + (yMax - yMin).ToString(CultureInfo.CurrentCulture);
        }
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

    public void SetYMin(string text)
    {
        YMin = text;
        SetDelta();
    }

    public void SetYMax(string text)
    {
        YMax = text;
        SetDelta();
    }

    /// <summary>
    /// Corrected: VB TxtA_TextChanged called SetDefaultUserData, which set a back to 0.5 (and the ranges
    /// to their defaults), so c could not be changed. The new c is used at the next start.
    /// </summary>
    public void SetA(string text) => A = text;

    /// <summary>Corrected like <see cref="SetA"/> (VB set b back to 1).</summary>
    public void SetB(string text) => B = text;

    /// <summary>VB: SetExponent (CboN).</summary>
    public void SetExponent(int n)
    {
        N = n;
        DS.N = DS.IsUseN ? n : 3;
        ResetIteration();
        SetDefaultUserData();
    }

    /// <summary>VB: SetOptions. Shaded colors together with rotation or conjugation are not the standard.</summary>
    public void SetMixing(NewtonMixing mixing)
    {
        Mixing = mixing;
        Color = mixing is NewtonMixing.Rotate or NewtonMixing.Conjugate ? NewtonColor.Bright : NewtonColor.Shadowed;
    }

    /// <summary>VB: ResetIteration. The page clears the picture and draws the axes and the roots.</summary>
    public void ResetIteration()
    {
        Status = IterationStatus.Stopped;
        Steps = 0;
        _spiral = null;
        _watch.Reset();
        IsFinished = false;
        Protocol.Clear();
        DS.ResetIteration();
    }

    /// <summary>
    /// VB: StartIteration. From Stopped the user data is checked and the iteration is prepared;
    /// returns the error message, or null if the iteration runs.
    /// </summary>
    public string? Start()
    {
        if (Status == IterationStatus.Stopped)
        {
            var errorX = UserDataCheck.CheckInterval(XMin, XMax, DS.XValueParameter.Range, out var xRange);
            var errorY = UserDataCheck.CheckInterval(YMin, YMax, DS.YValueParameter.Range, out var yRange);
            string? errorA = null, errorB = null;
            if (DS.IsUseC)
            {
                // The parameter c has to be in the same area as the z-values
                errorA = UserDataCheck.CheckValue(A, DS.XValueParameter.Range, out _);
                errorB = UserDataCheck.CheckValue(B, DS.YValueParameter.Range, out _);
            }

            var errors = new[] { errorX, errorY, errorA, errorB }.OfType<string>().ToList();
            if (errors.Count > 0)
            {
                // VB went on with the old c if only c was wrong; here nothing starts.
                return string.Join(Environment.NewLine, errors);
            }

            DS.N = DS.IsUseN ? N : 3;
            DS.ActualXRange = xRange!;
            DS.ActualYRange = yRange!;
            DS.IsProtocol = IsProtocol;
            DS.UseMixing = Mixing;
            DS.UseColor = Color;
            if (DS.IsUseC)
            {
                DS.C = new ComplexNumber(ParseDouble(A), ParseDouble(B));
            }

            // Corrected: VB called ResetIteration (which prepares the roots) before n, the ranges and c were
            // set, so the first picture of the polynomial missed the root c and a new c showed only at the
            // second start. Now the roots are prepared with the new values.
            ResetIteration();
            _spiral = SpiralScan.Pixels(DiagramSize, DiagramSize).GetEnumerator();
            Status = IterationStatus.Ready;
        }

        if (Status is IterationStatus.Ready or IterationStatus.Interrupted)
        {
            Status = IterationStatus.Running;
            IsFinished = false;
            _watch.Start();
        }

        return null;
    }

    /// <summary>VB: StopIteration. VB draws the roots black also when interrupted.</summary>
    public void Stop()
    {
        if (Status == IterationStatus.Running)
        {
            Status = IterationStatus.Interrupted;
            _watch.Stop();
            IsFinished = true;
        }
    }

    /// <summary>Computes the next pixel of the spiral; false (and Stopped) when the picture is complete.</summary>
    public bool Step(out NewtonStep step)
    {
        if (Status != IterationStatus.Running || _spiral is null || !_spiral.MoveNext())
        {
            if (Status == IterationStatus.Running)
            {
                Status = IterationStatus.Stopped;
                _watch.Stop();
                IsFinished = true;
            }

            step = default;
            return false;
        }

        var (p, q) = _spiral.Current;
        var pixel = DS.IterationStep(p, q);
        if (pixel.ProtocolEntry is not null)
        {
            Protocol.Add(pixel.ProtocolEntry);
            if (Protocol.Count > MaxProtocolLines + MaxProtocolLines / 10)
            {
                Protocol.RemoveRange(0, Protocol.Count - MaxProtocolLines);
            }
        }

        Steps++;
        step = new NewtonStep(p, q, pixel);
        return true;
    }

    /// <summary>The selector writes the selected area into the text boxes (VB pixels of the shown picture).</summary>
    public bool SelectArea(int startP, int startQ, int endP, int endQ)
    {
        if (!IsAreaSelectable)
        {
            return false;
        }

        var selection = DiagramAreaSelector.Select(DS.Transform, DS.XValueParameter.Range, DS.YValueParameter.Range,
            startP, startQ, endP, endQ);
        if (selection is null)
        {
            return false;
        }

        (XMin, XMax, YMin, YMax) = selection.Value;
        SetDelta();
        return true;
    }

    /// <summary>
    /// VB: ShowBasin. Resets the picture (SetExponent) and returns the approximated basin of the root 1,
    /// the area where |N'(z)| &lt; 1, drawn temporarily over the picture.
    /// </summary>
    public NewtonBasin ShowBasin()
    {
        SetExponent(N);
        return NewtonBasin.Create(DS.N);
    }

    internal static double ParseDouble(string text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var value)
            ? value
            : double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
}
