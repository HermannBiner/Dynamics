using System.Globalization;
using Dynamics.Core.General;

namespace Dynamics.Core.Julia;

/// <summary>
/// UI-free logic of FrmMandelbrotMap: the Mandelbrot set is shown, the user chooses c = a + ib with the mouse,
/// and the Julia set for c is generated. Port of ClsMandelbrotMapController.
/// Unlike VB, each picture has its own spiral: VB shared the spiral variables and the status between the
/// two pictures, so the Julia set started while the Mandelbrot set was still being drawn.
/// </summary>
public sealed class MandelbrotMapController
{
    /// <summary>For the Mandelbrot map, the interval of c is a little bigger.</summary>
    private static readonly Interval CRange = new(-1.9m, 1.9m);

    private IEnumerator<(int P, int Q)>? _mandelbrotSpiral;
    private IEnumerator<(int P, int Q)>? _juliaSpiral;
    private bool _isMouseDown;

    public MandelbrotMapController(int mandelbrotSize, int juliaSize)
    {
        DSMandelbrot = new Mandelbrot { DiagramSize = mandelbrotSize, N = 2, IsUseSystemColors = true, IsProtocol = false };
        DSMandelbrot.ActualXRange = DSMandelbrot.XValueParameter.Range;
        DSMandelbrot.ActualYRange = DSMandelbrot.YValueParameter.Range;
        DSMandelbrot.C = new ComplexNumber(0, 0);

        DSJulia = new JuliaPN { DiagramSize = juliaSize, N = 2, IsUseSystemColors = true, IsProtocol = false };
        DSJulia.ActualXRange = DSJulia.XValueParameter.Range;
        DSJulia.ActualYRange = DSJulia.YValueParameter.Range;

        _mandelbrotSpiral = SpiralScan.Pixels(mandelbrotSize, mandelbrotSize).GetEnumerator();
        SetDefaultUserData();
    }

    public Mandelbrot DSMandelbrot { get; }
    public JuliaPN DSJulia { get; }

    public string A { get; set; } = "0";
    public string B { get; set; } = "0";

    public bool IsMandelbrotComplete => _mandelbrotSpiral is null;
    public bool IsJuliaComplete => _juliaSpiral is null;
    public bool IsRunning => !IsMandelbrotComplete || !IsJuliaComplete;

    /// <summary>The c of the Julia set being drawn.</summary>
    public ComplexNumber JuliaC { get; private set; } = new(0, 0);

    /// <summary>VB: SetDefaultUserData, the parameters of the seahorse. Returns an error message or null.</summary>
    public string? SetDefaultUserData()
    {
        A = 0.32m.ToString(CultureInfo.CurrentCulture);
        B = 0.043m.ToString(CultureInfo.CurrentCulture);
        return StartJulia();
    }

    /// <summary>VB: Startiteration(DSJulia). Returns an error message or null.</summary>
    public string? StartJulia()
    {
        var errorA = UserDataCheck.CheckValue(A, CRange, out _);
        var errorB = UserDataCheck.CheckValue(B, CRange, out _);
        if (errorA is not null || errorB is not null)
        {
            return string.Join(Environment.NewLine, new[] { errorA, errorB }.OfType<string>());
        }

        JuliaC = new ComplexNumber(JuliaController.ParseDouble(A), JuliaController.ParseDouble(B));
        DSJulia.C = JuliaC;
        DSJulia.ResetIteration();
        _juliaSpiral = SpiralScan.Pixels(DSJulia.DiagramSize, DSJulia.DiagramSize).GetEnumerator();
        return null;
    }

    /// <summary>
    /// Computes the next pixel: first the Mandelbrot set, then the Julia set.
    /// False if both pictures are complete.
    /// </summary>
    public bool Step(out bool isMandelbrot, out JuliaStep step)
    {
        if (_mandelbrotSpiral is not null)
        {
            isMandelbrot = true;
            if (_mandelbrotSpiral.MoveNext())
            {
                var (p, q) = _mandelbrotSpiral.Current;
                step = new JuliaStep(p, q, DSMandelbrot.IterationStep(p, q));
                return true;
            }

            _mandelbrotSpiral = null;
        }

        isMandelbrot = false;
        if (_juliaSpiral is not null)
        {
            if (_juliaSpiral.MoveNext())
            {
                var (p, q) = _juliaSpiral.Current;
                step = new JuliaStep(p, q, DSJulia.IterationStep(p, q));
                return true;
            }

            _juliaSpiral = null;
        }

        step = default;
        return false;
    }

    /// <summary>VB: MouseDown on the Mandelbrot set; possible as soon as the Mandelbrot set is complete.</summary>
    public bool MouseDown(int pixelX, int pixelY)
    {
        if (!IsMandelbrotComplete)
        {
            return false;
        }

        _isMouseDown = true;
        MouseMove(pixelX, pixelY);
        return true;
    }

    /// <summary>VB: MouseMove sets c to the mouse position.</summary>
    public bool MouseMove(int pixelX, int pixelY)
    {
        if (!_isMouseDown)
        {
            return false;
        }

        var point = DSMandelbrot.Transform.PixelToMathPoint(pixelX, pixelY);
        A = point.X.ToString(CultureInfo.CurrentCulture);
        B = point.Y.ToString(CultureInfo.CurrentCulture);
        return true;
    }

    /// <summary>
    /// VB: MouseUp generates the Julia set with the new c. False if the mouse was not down;
    /// error is the message if c is not allowed.
    /// </summary>
    public bool MouseUp(out string? error)
    {
        error = null;
        if (!_isMouseDown)
        {
            return false;
        }

        _isMouseDown = false;
        error = StartJulia();
        return true;
    }
}
