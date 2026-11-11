namespace Dynamics.Core.Julia;

/// <summary>
/// Goes through the pixels of a diagram in a spiral starting in the midpoint, in the order of
/// PerformIteration and IterationLoop of ClsJuliaController and ClsMandelbrotMapController.
/// </summary>
/// <remarks>
/// VB drew the pixel (p, q) as a 2 x 2 ellipse at (p - 1, q - 1), which GDI puts into the bitmap pixels
/// (p - 1, q) and (p, q); see <see cref="BitmapPosition"/>. VB stopped when p or q left the diagram.
/// For an even size this covers the whole bitmap; for an odd size (e.g. 563 in the Mandelbrot map)
/// the top row stayed white. Corrected: the spiral continues until every bitmap pixel is computed.
/// Pixels outside the bitmap are skipped (VB computed them without drawing).
/// </remarks>
public static class SpiralScan
{
    /// <summary>The bitmap pixel (column, row) of the VB pixel (p, q).</summary>
    public static (int X, int Y) BitmapPosition(int p, int q) => (p - 1, q);

    /// <summary>The VB pixel (p, q) shown in the bitmap pixel (column, row); inverse of <see cref="BitmapPosition"/>.</summary>
    public static (int P, int Q) PixelAt(int x, int y) => (x + 1, y);

    public static IEnumerable<(int P, int Q)> Pixels(int width, int height)
    {
        var remaining = width * height;
        foreach (var (p, q) in Spiral(width, height))
        {
            var (x, y) = BitmapPosition(p, q);
            if (x >= 0 && x < width && y >= 0 && y < height)
            {
                yield return (p, q);
                if (--remaining == 0)
                {
                    yield break;
                }
            }
        }
    }

    /// <summary>The endless spiral of VB.</summary>
    private static IEnumerable<(int P, int Q)> Spiral(int width, int height)
    {
        // VB: CInt(Width / 2) rounds half to even.
        var p = Convert.ToInt32(width / 2.0);
        var q = Convert.ToInt32(height / 2.0);
        yield return (p, q);

        var examinatedPoints = 0;

        // Length of a branch of the spiral
        var l = 0;
        while (true)
        {
            examinatedPoints++;

            // Sig = 1 if odd, -1 else
            var sig = examinatedPoints % 2 == 0 ? -1 : 1;
            l++;
            for (var k = 1; k < l; k++)
            {
                p += sig;
                yield return (p, q);
            }

            for (var k = 1; k < l; k++)
            {
                q += sig;
                yield return (p, q);
            }
        }
    }
}
