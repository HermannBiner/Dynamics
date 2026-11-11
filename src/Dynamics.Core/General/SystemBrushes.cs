namespace Dynamics.Core.General;

/// <summary>
/// The standard system colors, e.g. for Julia and Mandelbrot sets: a color scale of 25 colors
/// for a number of iteration steps. Port of ClsSystemBrushes; colors are ARGB values (Color.ToArgb).
/// </summary>
public class SystemBrushes(int maxSteps)
{
    private const int ColorNumber = 25;

    /// <summary>To increase the brightness.</summary>
    private const int Increase = 3;

    public int GetSystemColor(int steps)
    {
        // VB: CInt rounds half to even; Steps * ColorNumber * Increase / MaxSteps is a Double division.
        var scaledSteps = Math.Min(Convert.ToInt32((double)(steps * ColorNumber * Increase) / maxSteps), maxSteps);

        var redPart = scaledSteps switch
        {
            >= 14 or 6 or 7 or 8 or 9 => 1.0,
            4 or 5 or 12 or 13 => 0.5,
            _ => 0.0,
        };

        var greenPart = scaledSteps switch
        {
            >= 20 or 2 or 3 or 10 or 11 or 12 or 13 => 1.0,
            1 or 8 or 9 => 0.5,
            _ => 0.0,
        };

        var bluePart = scaledSteps switch
        {
            <= 9 or 24 or 25 => 1.0,
            14 or 15 or 22 or 23 => 0.5,
            _ => 0.0,
        };

        return Argb.FromRgb(Convert.ToInt32(redPart * 255), Convert.ToInt32(greenPart * 255), Convert.ToInt32(bluePart * 255));
    }
}

/// <summary>ARGB colors as in System.Drawing.Color.ToArgb.</summary>
public static class Argb
{
    public const int Black = unchecked((int)0xFF000000);
    public const int White = unchecked((int)0xFFFFFFFF);

    public static int FromRgb(int red, int green, int blue) =>
        unchecked((int)(0xFF000000u | (uint)red << 16 | (uint)green << 8 | (uint)blue));

    public static int Red(int argb) => (argb >> 16) & 0xFF;
    public static int Green(int argb) => (argb >> 8) & 0xFF;
    public static int Blue(int argb) => argb & 0xFF;
}
