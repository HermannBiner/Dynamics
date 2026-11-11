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

    public double[] Take()
    {
        var result = _commands.ToArray();
        _commands.Clear();
        return result;
    }
}
