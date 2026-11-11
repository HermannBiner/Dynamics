using System.Globalization;

namespace Dynamics.Core.General;

/// <summary>
/// Selection of an area of a diagram with the mouse. Port of the computation of ClsDiagramAreaSelector;
/// the UI draws the selection rectangle and calls <see cref="Select"/> when the mouse button is released.
/// Corrected: VB converted the corners with its own formulas (x divided by the width instead of width - 1,
/// y by 0.99 · height), so the selected range differed slightly from the drawn rectangle. Now the corners
/// are converted with the transformation the picture was drawn with.
/// </summary>
public static class DiagramAreaSelector
{
    /// <summary>
    /// The texts for XMin, XMax, YMin, YMax of the rectangle between the pixels start and end
    /// (pixel coordinates of the transform), cut to the allowed ranges; null if start equals end.
    /// </summary>
    public static (string XMin, string XMax, string YMin, string YMax)? Select(
        CoordinateTransform picture, Interval xRange, Interval yRange, int startX, int startY, int endX, int endY)
    {
        if (startX == endX && startY == endY)
        {
            return null;
        }

        var a = picture.PixelToMathPoint(startX, startY);
        var b = picture.PixelToMathPoint(endX, endY);

        return (
            Math.Max(Math.Min(a.X, b.X), xRange.A).ToString(CultureInfo.CurrentCulture),
            Math.Min(Math.Max(a.X, b.X), xRange.B).ToString(CultureInfo.CurrentCulture),
            Math.Max(Math.Min(a.Y, b.Y), yRange.A).ToString(CultureInfo.CurrentCulture),
            Math.Min(Math.Max(a.Y, b.Y), yRange.B).ToString(CultureInfo.CurrentCulture));
    }
}
