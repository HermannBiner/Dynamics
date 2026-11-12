namespace Dynamics.Core.General;

/// <summary>
/// Conversion between mathematical and pixel coordinates of a diagram.
/// Port of the transformation part of ClsGraphicTool (the drawing part is done by the UI).
/// </summary>
public class CoordinateTransform
{
    // VB: the diagram size is reduced by 1 for better visibility.
    private readonly int _cornerX;
    private readonly int _cornerY;

    public CoordinateTransform(int width, int height, Interval mathXInterval, Interval mathYInterval)
    {
        _cornerX = Math.Max(1, width - 1);
        _cornerY = Math.Max(1, height - 1);
        MathXInterval = mathXInterval;
        MathYInterval = mathYInterval;
    }

    public Interval MathXInterval { get; }
    public Interval MathYInterval { get; }

    /// <summary>Pixel point to mathematical coordinates, exactly as ClsGraphicTool.PixelToMathpoint.</summary>
    public MathPoint PixelToMathPoint(int pixelX, int pixelY) => new(
        MathXInterval.A + (pixelX - 1) * MathXInterval.IntervalWidth / _cornerX,
        MathYInterval.B - (pixelY - 1) * MathYInterval.IntervalWidth / _cornerY);

    /// <summary>Mathematical point to pixel coordinates; unrounded, for drawing on a canvas.</summary>
    public (double X, double Y) MathToPixel(MathPoint point) => MathToPixel(point.X, point.Y);

    public (double X, double Y) MathToPixel(double x, double y) => (
        1 + (x - MathXInterval.A) * _cornerX / MathXInterval.IntervalWidth,
        1 + (MathYInterval.B - y) * _cornerY / MathYInterval.IntervalWidth);

    /// <summary>Length in mathematical X units converted to pixels.</summary>
    public double LengthToPixel(double length) => length * _cornerX / MathXInterval.IntervalWidth;

    /// <summary>VB DrawPoint/FillCircle only draw points inside the diagram.</summary>
    public bool IsInside(MathPoint point) =>
        MathXInterval.IsNumberInInterval(point.X) && MathYInterval.IsNumberInInterval(point.Y);
}
