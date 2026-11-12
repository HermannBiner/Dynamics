using System.Globalization;
using Dynamics.Core.General;

namespace Dynamics.Core.Newton;

/// <summary>
/// UI-free logic of FrmSingleNewtonIteration: the Newton iteration of a single start point for the unit roots
/// of z² - 1 or z³ - 1. Port of ClsSingleNewtonIterationController. The temporary drawing of VB (on the picture
/// box) is the state <see cref="Orbit"/> and <see cref="ConvergedRoot"/>; the page draws it.
/// </summary>
public sealed class SingleNewtonController
{
    public static readonly IReadOnlyList<int> Exponents = [2, 3];

    public const int MaxSteps = 100;

    /// <summary>VB: Const ConvergenceLimit As Decimal = 0.01, compared with a Double.</summary>
    private static readonly double ConvergenceLimit = 0.01;

    private readonly List<ComplexNumber> _orbit = [];
    private readonly List<ComplexNumber> _unitRoots = [];
    private bool _isMouseDown;
    private int _steps;

    public SingleNewtonController(int diagramSize)
    {
        Transform = new CoordinateTransform(diagramSize, diagramSize, MathInterval, MathInterval);

        // VB: CboN.SelectedIndex = 1
        LoadRoots(3);
        A = "0.5";
        B = "1";
    }

    public static Interval MathInterval { get; } = new(-1.5, 1.5);

    public CoordinateTransform Transform { get; }

    /// <summary>Exponent of the unit roots, 2 or 3.</summary>
    public int N { get; private set; }

    public IReadOnlyList<ComplexNumber> UnitRoots => _unitRoots;

    /// <summary>The start point (text boxes).</summary>
    public string A { get; set; }
    public string B { get; set; }

    public IterationStatus Status { get; private set; } = IterationStatus.Stopped;

    /// <summary>VB: BtnNextStep is disabled while the iteration runs (it never runs longer than a call).</summary>
    public bool CanStep => Status != IterationStatus.Running;

    /// <summary>The start point and the iterated points drawn so far (VB: PicGraphics, cleared by Refresh).</summary>
    public IReadOnlyList<ComplexNumber> Orbit => _orbit;

    /// <summary>Index of the root (0, 1, 2) the last point converged to, or null.</summary>
    public int? ConvergedRoot { get; private set; }

    /// <summary>VB: LoadRoots (CboN); the page clears the picture.</summary>
    public void LoadRoots(int n)
    {
        N = n;
        _unitRoots.Clear();

        var w = new ComplexNumber(1, 0);
        _unitRoots.Add(w);

        for (var k = 2; k <= N; k++)
        {
            w = w.Rotate((double)(2 * Math.PI / N));
            _unitRoots.Add(w);
        }

        ResetIteration();
    }

    /// <summary>VB: ResetIteration; Refresh clears the temporary drawing.</summary>
    public void ResetIteration()
    {
        ClearDrawing();
        Status = IterationStatus.Stopped;
        _steps = 0;
    }

    private void ClearDrawing()
    {
        _orbit.Clear();
        ConvergedRoot = null;
    }

    /// <summary>VB: NextStep. From Stopped the start point is read from the text boxes.</summary>
    public string? NextStep()
    {
        if (Status == IterationStatus.Stopped)
        {
            if (!double.TryParse(A, NumberStyles.Float, CultureInfo.CurrentCulture, out var a) &&
                !double.TryParse(A, NumberStyles.Float, CultureInfo.InvariantCulture, out a) ||
                !double.TryParse(B, NumberStyles.Float, CultureInfo.CurrentCulture, out var b) &&
                !double.TryParse(B, NumberStyles.Float, CultureInfo.InvariantCulture, out b))
            {
                return Text.Get("NumericValue");
            }

            // From Stopped the drawing is empty (Reset) or shows the start point set with the mouse.
            ClearDrawing();
            _orbit.Add(new ComplexNumber(a, b));
            Status = IterationStatus.Ready;
        }

        if (Status == IterationStatus.Ready)
        {
            IterationStep();
        }

        return null;
    }

    private ComplexNumber Actual => _orbit[^1];

    private bool IsConvergent()
    {
        var ok = false;
        if (Actual.Add(_unitRoots[0].Stretch(-1)).AbsoluteValue < ConvergenceLimit)
        {
            ConvergedRoot = 0;
            ok = true;
        }
        else if (Actual.Add(_unitRoots[1].Stretch(-1)).AbsoluteValue < ConvergenceLimit)
        {
            ConvergedRoot = 1;
            ok = true;
        }

        if (N == 3 && Actual.Add(_unitRoots[2].Stretch(-1)).AbsoluteValue < ConvergenceLimit)
        {
            ConvergedRoot = 2;
            ok = true;
        }

        return ok;
    }

    private void IterationStep()
    {
        _steps++;
        _orbit.Add(Newton3N(Actual));
        if (IsConvergent() || _steps > MaxSteps)
        {
            Status = IterationStatus.Interrupted;
        }
    }

    /// <summary>VB: IterationLoop, iterates until convergence or MaxSteps.</summary>
    private void IterationLoop()
    {
        Status = IterationStatus.Running;
        while (Status != IterationStatus.Interrupted)
        {
            IterationStep();
        }

        _steps = 0;
    }

    private ComplexNumber Newton3N(ComplexNumber z) => z.Stretch((double)(N - 1) / N).Add(Denominator(z));

    private ComplexNumber Denominator(ComplexNumber z) => z.Power(N - 1).Invers.Stretch(1.0 / N);

    /// <summary>VB: MouseDown sets the start point.</summary>
    public void MouseDown(MathPoint point)
    {
        _isMouseDown = true;
        ClearDrawing();
        _orbit.Add(new ComplexNumber((double)point.X, (double)point.Y));
        A = point.X.ToString(CultureInfo.CurrentCulture);
        B = point.Y.ToString(CultureInfo.CurrentCulture);
    }

    /// <summary>VB: MouseMove with the button down iterates the start point under the mouse completely.</summary>
    public bool MouseMove(MathPoint point)
    {
        if (!_isMouseDown)
        {
            return false;
        }

        ClearDrawing();
        _orbit.Add(new ComplexNumber((double)point.X, (double)point.Y));
        A = point.X.ToString("N10", CultureInfo.CurrentCulture);
        B = point.Y.ToString("N10", CultureInfo.CurrentCulture);
        _steps = 0;
        IterationLoop();
        return true;
    }

    public void MouseUp() => _isMouseDown = false;
}
