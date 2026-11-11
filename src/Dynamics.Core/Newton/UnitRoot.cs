using Dynamics.Core.General;

namespace Dynamics.Core.Newton;

/// <summary>
/// A root of a complex polynomial with its base color, defined by an index; the brightness depends on the
/// number of steps the iteration needed to approach the root. Port of ClsUnitRoot.
/// </summary>
public class UnitRoot : ComplexNumber
{
    private readonly double _redPart;
    private readonly double _greenPart;
    private readonly double _bluePart;

    public UnitRoot(ComplexNumber z, int index)
        : base(z.X, z.Y)
    {
        Index = index;

        _redPart = index switch
        {
            1 or 4 or 6 or 9 or 10 or 11 or 12 => 1,
            5 or 7 => 0.5,
            _ => 0,
        };

        _greenPart = index switch
        {
            2 or 4 or 7 or 8 or 11 => 1,
            10 or 12 => 0.5,
            _ => 0,
        };

        _bluePart = index switch
        {
            3 or 5 or 8 or 9 or 12 => 1,
            6 or 11 => 0.5,
            _ => 0,
        };
    }

    public int Index { get; }

    /// <summary>ARGB color for a brightness in [0, 1].</summary>
    public int GetColor(double brightness)
    {
        brightness = Math.Min(1, brightness);
        brightness = Math.Max(0, brightness);

        return Argb.FromRgb(
            Convert.ToInt32(brightness * _redPart * 255),
            Convert.ToInt32(brightness * _greenPart * 255),
            Convert.ToInt32(brightness * _bluePart * 255));
    }
}
