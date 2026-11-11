using System.Globalization;
using Dynamics.Core.General;

namespace Dynamics.Core.Julia;

/// <summary>
/// Common logic of all Julia sets of the iteration z → zⁿ + c. Port of ClsJuliaAbstract plus the
/// identical parts of ClsJuliaPN and ClsMandelbrot (escape loop, colors, protocol).
/// </summary>
public abstract class JuliaBase : IJulia
{
    /// <summary>The precision of the border of the set.</summary>
    public const int MaxSteps = 250;

    private readonly SystemBrushes _standardColors = new(MaxSteps);

    private int _diagramSize = 2;
    private Interval _actualXRange = null!;
    private Interval _actualYRange = null!;

    // Bounds of the protocol; corrected: VB did not reset them in ResetIteration.
    private int _upperBoundSteps;
    private int _lowerBoundSteps = MaxSteps;

    protected GeneralParameter MyXValueParameter = null!;
    protected GeneralParameter MyYValueParameter = null!;

    protected ComplexNumber MyC = new(0, 0);
    protected int MyN;

    protected bool MyIsProtocol;
    protected double MyRedPercent;
    protected double MyGreenPercent;
    protected double MyBluePercent;
    protected bool MyIsUseSystemColors;

    public int DiagramSize
    {
        get => _diagramSize;
        set
        {
            _diagramSize = value;
            UpdateTransform();
        }
    }

    public GeneralParameter XValueParameter => MyXValueParameter;
    public GeneralParameter YValueParameter => MyYValueParameter;

    public Interval ActualXRange
    {
        get => _actualXRange;
        set
        {
            _actualXRange = value;
            UpdateTransform();
        }
    }

    public Interval ActualYRange
    {
        get => _actualYRange;
        set
        {
            _actualYRange = value;
            UpdateTransform();
        }
    }

    public CoordinateTransform Transform { get; private set; } = null!;

    public ComplexNumber C { set => MyC = value; }

    public abstract int N { set; }

    public bool IsProtocol { set => MyIsProtocol = value; }
    public double RedPercent { set => MyRedPercent = value; }
    public double GreenPercent { set => MyGreenPercent = value; }
    public double BluePercent { set => MyBluePercent = value; }
    public bool IsUseSystemColors { set => MyIsUseSystemColors = value; }

    public abstract bool IsMandelbrot { get; }

    /// <summary>
    /// VB cleared the bitmap and the protocol list here; this is done by the UI.
    /// Corrected: the protocol bounds are reset as well, so a new picture gets a complete protocol.
    /// </summary>
    public virtual void ResetIteration()
    {
        _upperBoundSteps = 0;
        _lowerBoundSteps = MaxSteps;
    }

    public abstract JuliaPixel IterationStep(int pixelX, int pixelY);

    /// <summary>Sets the actual ranges without the setter logic (VB: MyActualXRange = ...).</summary>
    protected void SetActualRanges(Interval x, Interval y)
    {
        _actualXRange = x;
        _actualYRange = y;
        UpdateTransform();
    }

    private void UpdateTransform()
    {
        if (_actualXRange is not null && _actualYRange is not null)
        {
            Transform = new CoordinateTransform(_diagramSize, _diagramSize, _actualXRange, _actualYRange);
        }
    }

    /// <summary>
    /// Iterates z → zⁿ + c until |z| > radius or more than MaxSteps steps.
    /// Same operations in the same order as ClsComplexNumber.Power(n).Add(c), without allocations.
    /// </summary>
    protected int Iterate(double zx, double zy, double cx, double cy, double radius)
    {
        var n = MyN;
        var steps = 0;
        while (!(Math.Sqrt(zx * zx + zy * zy) > radius || steps > MaxSteps))
        {
            steps++;

            // Power(n): result = z, then n - 1 times result = result.Mult(z)
            var rx = zx;
            var ry = zy;
            for (var k = 1; k < n; k++)
            {
                var x = rx * zx - ry * zy;
                var y = rx * zy + ry * zx;
                rx = x;
                ry = y;
            }

            zx = rx + cx;
            zy = ry + cy;
        }

        return steps;
    }

    /// <summary>VB: the brush for the number of steps; steps is already at least 1.</summary>
    protected int GetColor(int steps)
    {
        if (MyIsUseSystemColors)
        {
            return _standardColors.GetSystemColor(steps);
        }

        var colorIndex = (double)steps / MaxSteps;

        // to keep the brightness higher
        colorIndex = Math.Min(1, colorIndex * 2);
        colorIndex = Math.Pow(colorIndex, 1.0 / 5);

        return Argb.FromRgb(
            Convert.ToInt32(255 * colorIndex * MyRedPercent),
            Convert.ToInt32(255 * colorIndex * MyGreenPercent),
            Convert.ToInt32(255 * colorIndex * MyBluePercent));
    }

    /// <summary>A new protocol entry if the steps are a new maximum or minimum (VB logic).</summary>
    protected string? Protocol(int steps)
    {
        if (!MyIsProtocol)
        {
            return null;
        }

        if (steps > _upperBoundSteps)
        {
            _upperBoundSteps = steps;
            return _upperBoundSteps.ToString(CultureInfo.CurrentCulture);
        }

        if (steps < _lowerBoundSteps)
        {
            _lowerBoundSteps = steps;
            return _lowerBoundSteps.ToString(CultureInfo.CurrentCulture);
        }

        return null;
    }

    /// <summary>The pixel as complex number (VB: BmpGraphics.PixelToMathpoint, Decimal to Double).</summary>
    protected (double X, double Y) PixelToComplex(int pixelX, int pixelY)
    {
        var point = Transform.PixelToMathPoint(pixelX, pixelY);
        return ((double)point.X, (double)point.Y);
    }

    /// <summary>VB: Math.Pow(2, 1 / (n - 1)).</summary>
    protected double MinimalRadius => Math.Pow(2, 1.0 / (MyN - 1));
}
