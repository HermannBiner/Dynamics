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

    public void Line(MathPoint a, MathPoint b, CanvasColor color, int width)
    {
        var (x1, y1) = Transform.MathToPixel(a);
        var (x2, y2) = Transform.MathToPixel(b);
        _commands.AddRange([1, x1, y1, x2, y2, (int)color, width]);
    }

    /// <summary>Filled circle with radius in mathematical units; like VB only if the centre is inside.</summary>
    public void FillCircle(MathPoint center, decimal radius, CanvasColor color)
    {
        if (!Transform.IsInside(center))
        {
            return;
        }

        var (x, y) = Transform.MathToPixel(center);
        _commands.AddRange([2, x, y, Transform.LengthToPixel(radius), (int)color]);
    }

    /// <summary>Point of about "size" pixels; like VB only if it is inside.</summary>
    public void Point(MathPoint point, CanvasColor color, int size)
    {
        if (!Transform.IsInside(point))
        {
            return;
        }

        var (x, y) = Transform.MathToPixel(point);
        _commands.AddRange([3, x, y, size, (int)color]);
    }

    /// <summary>Shifts the whole picture right by dx pixels (chart recorder).</summary>
    public void ShiftRight(int dx) => _commands.AddRange([4, dx]);

    /// <summary>Rectangle outline in pixel coordinates (e.g. the selection of an area).</summary>
    public void PixelRectangle(double x, double y, double width, double height, CanvasColor color, int lineWidth) =>
        _commands.AddRange([5, x, y, width, height, (int)color, lineWidth]);

    /// <summary>Circle outline with radius in mathematical units; like VB only if the centre is inside.</summary>
    public void Circle(MathPoint center, decimal radius, CanvasColor color, int width)
    {
        if (!Transform.IsInside(center))
        {
            return;
        }

        var (x, y) = Transform.MathToPixel(center);
        _commands.AddRange([6, x, y, Transform.LengthToPixel(radius), (int)color, width]);
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
        _commands.AddRange([1, Math.Clamp(x1, -far, far), Math.Clamp(y1, -far, far),
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
        _commands.AddRange([3, x, y, size, (int)color]);
    }

    public double[] Take()
    {
        var result = _commands.ToArray();
        _commands.Clear();
        return result;
    }
}
