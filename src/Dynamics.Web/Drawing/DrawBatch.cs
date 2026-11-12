using Dynamics.Core.General;

namespace Dynamics.Web.Drawing;

/// <summary>Colours of wwwroot/js/canvas.js.</summary>
public enum CanvasColor
{
    White = 0,
    Blue = 1,
    Red = 2,
    Green = 3,
    Aquamarine = 4,
    DarkViolet = 5,
    Black = 6,
    Magenta = 7,
}

/// <summary>
/// Collects drawing commands in mathematical coordinates for one canvas and sends them in one interop call.
/// Replaces the drawing part of ClsGraphicTool.
/// </summary>
public sealed class DrawBatch(CoordinateTransform transform)
{
    private readonly List<double> _commands = [];

    public CoordinateTransform Transform { get; } = transform;

    public bool IsEmpty => _commands.Count == 0;

    public void Clear() => _commands.Add(0);

    /// <summary>
    /// Adds one command. A command with an infinite or NaN number is dropped: Double does not throw on overflow
    /// like Decimal did, and JSON (the transfer to JavaScript) cannot carry such numbers.
    /// </summary>
    private void Add(ReadOnlySpan<double> command)
    {
        foreach (var value in command)
        {
            if (!double.IsFinite(value))
            {
                return;
            }
        }

        _commands.AddRange(command);
    }

    public void Line(MathPoint a, MathPoint b, CanvasColor color, int width)
    {
        var (x1, y1) = Transform.MathToPixel(a);
        var (x2, y2) = Transform.MathToPixel(b);
        Add([1, x1, y1, x2, y2, (int)color, width]);
    }

    /// <summary>Filled circle with radius in mathematical units; like VB only if the centre is inside.</summary>
    public void FillCircle(MathPoint center, double radius, CanvasColor color)
    {
        if (!Transform.IsInside(center))
        {
            return;
        }

        var (x, y) = Transform.MathToPixel(center);
        Add([2, x, y, Transform.LengthToPixel(radius), (int)color]);
    }

    /// <summary>Point of about "size" pixels; like VB only if it is inside.</summary>
    public void Point(MathPoint point, CanvasColor color, int size)
    {
        if (!Transform.IsInside(point))
        {
            return;
        }

        var (x, y) = Transform.MathToPixel(point);
        Add([3, x, y, size, (int)color]);
    }

    private readonly HashSet<(int X, int Y, int Color)> _drawnPoints = [];

    /// <summary>
    /// Like <see cref="Point(MathPoint, CanvasColor, int)"/>, but a point at the same half pixel with the same color is
    /// drawn only once per batch. For diagrams with thousands of values per column (Feigenbaum, bifurcation,
    /// C-diagram), most of which fall on the same pixels of a cycle.
    /// </summary>
    public void PointOnce(MathPoint point, CanvasColor color, int size)
    {
        if (!Transform.IsInside(point))
        {
            return;
        }

        var (x, y) = Transform.MathToPixel(point);
        if (_drawnPoints.Add(((int)Math.Round(x * 2), (int)Math.Round(y * 2), (int)color)))
        {
            Add([3, x, y, size, (int)color]);
        }
    }

    /// <summary>Like <see cref="PointOnce(MathPoint, CanvasColor, int)"/> with any ARGB color.</summary>
    public void PointOnce(MathPoint point, int argb, int size)
    {
        if (!Transform.IsInside(point))
        {
            return;
        }

        var (x, y) = Transform.MathToPixel(point);
        if (_drawnPoints.Add(((int)Math.Round(x * 2), (int)Math.Round(y * 2), argb)))
        {
            Add([7, x, y, size, argb]);
        }
    }

    /// <summary>Point with any ARGB color (VB brushes); like VB only if it is inside.</summary>
    public void Point(MathPoint point, int argb, int size)
    {
        if (!Transform.IsInside(point))
        {
            return;
        }

        var (x, y) = Transform.MathToPixel(point);
        Add([7, x, y, size, argb]);
    }

    /// <summary>Line with any ARGB color.</summary>
    public void Line(MathPoint a, MathPoint b, int argb, int width)
    {
        var (x1, y1) = Transform.MathToPixel(a);
        var (x2, y2) = Transform.MathToPixel(b);
        Add([9, x1, y1, x2, y2, argb, width]);
    }

    /// <summary>Arc of an ellipse; angles in degrees, clockwise on the screen (as GDI DrawArc).</summary>
    public void Arc(MathPoint center, double radiusX, double radiusY, int startAngle, int sweep, int argb, int width)
    {
        var (x, y) = Transform.MathToPixel(center);
        Add([8, x, y, Transform.LengthToPixel(radiusX), Transform.LengthToPixel(radiusY), startAngle, sweep, argb, width]);
    }

    /// <summary>VB DrawVerticalLine: a line over the whole height at x, if x is inside.</summary>
    public void VerticalLine(double x, CanvasColor color, int width)
    {
        if (!Transform.MathXInterval.IsNumberInInterval(x))
        {
            return;
        }

        Line(new MathPoint(x, Transform.MathYInterval.A), new MathPoint(x, Transform.MathYInterval.B), color, width);
    }

    /// <summary>Shifts the whole picture right by dx pixels (chart recorder).</summary>
    public void ShiftRight(int dx) => Add([4, dx]);

    /// <summary>Rectangle outline in pixel coordinates (e.g. the selection of an area).</summary>
    public void PixelRectangle(double x, double y, double width, double height, CanvasColor color, int lineWidth) =>
        Add([5, x, y, width, height, (int)color, lineWidth]);

    /// <summary>Filled rectangle in pixel coordinates with any ARGB color.</summary>
    public void FillPixelRectangle(double x, double y, double width, double height, int argb) =>
        Add([10, x, y, width, height, argb]);

    /// <summary>
    /// Filled rectangle between two mathematical corners (VB FillRectangle with the lower left corner a and the
    /// upper right corner c); like VB only if both corners are inside.
    /// </summary>
    public void FillRectangle(MathPoint a, MathPoint c, int argb)
    {
        if (!Transform.IsInside(a) || !Transform.IsInside(c))
        {
            return;
        }

        var (x1, y1) = Transform.MathToPixel(a);
        var (x2, y2) = Transform.MathToPixel(c);
        FillPixelRectangle(Math.Min(x1, x2), Math.Min(y1, y2), Math.Abs(x2 - x1), Math.Abs(y2 - y1), argb);
    }

    /// <summary>Circle outline with radius in pixels (e.g. around a start point); only if the centre is inside.</summary>
    public void PixelCircle(MathPoint center, double radius, int argb, int width)
    {
        if (!Transform.IsInside(center))
        {
            return;
        }

        var (x, y) = Transform.MathToPixel(center);
        Add([8, x, y, radius, radius, 0, 360, argb, width]);
    }

    /// <summary>Circle outline with radius in mathematical units; like VB only if the centre is inside.</summary>
    public void Circle(MathPoint center, double radius, CanvasColor color, int width)
    {
        if (!Transform.IsInside(center))
        {
            return;
        }

        var (x, y) = Transform.MathToPixel(center);
        Add([6, x, y, Transform.LengthToPixel(radius), (int)color, width]);
    }

    /// <summary>Line between complex numbers (Double); far away points are cut, invalid ones skipped.</summary>
    public void Line(ComplexNumber a, ComplexNumber b, CanvasColor color, int width)
    {
        var (x1, y1) = Transform.MathToPixel(a.X, a.Y);
        var (x2, y2) = Transform.MathToPixel(b.X, b.Y);
        if (!double.IsFinite(x1) || !double.IsFinite(y1) || !double.IsFinite(x2) || !double.IsFinite(y2))
        {
            return;
        }

        const double far = 1e6;
        Add([1, Math.Clamp(x1, -far, far), Math.Clamp(y1, -far, far),
            Math.Clamp(x2, -far, far), Math.Clamp(y2, -far, far), (int)color, width]);
    }

    /// <summary>Point at a complex number (Double); like VB only if it is inside.</summary>
    public void Point(ComplexNumber z, CanvasColor color, int size)
    {
        if (!Transform.MathXInterval.IsNumberInInterval(z.X) || !Transform.MathYInterval.IsNumberInInterval(z.Y))
        {
            return;
        }

        var (x, y) = Transform.MathToPixel(z.X, z.Y);
        Add([3, x, y, size, (int)color]);
    }

    public double[] Take()
    {
        var result = _commands.ToArray();
        _commands.Clear();
        _drawnPoints.Clear();
        return result;
    }
}
