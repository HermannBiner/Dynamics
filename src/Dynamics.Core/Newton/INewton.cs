using Dynamics.Core.General;

namespace Dynamics.Core.Newton;

/// <summary>VB INewton.EnColor: bright colors or shaded by the number of steps.</summary>
public enum NewtonColor
{
    Bright,
    Shadowed,
}

/// <summary>VB INewton.EnMixing: the point is conjugated or rotated before each Newton step.</summary>
public enum NewtonMixing
{
    None,
    Conjugate,
    Rotate,
}

/// <summary>Result of one iteration step: the color of the pixel and an optional protocol line.</summary>
public readonly record struct NewtonPixel(int Color, string? ProtocolEntry);

/// <summary>
/// Interface for any kind of polynomial that is the base of a Newton iteration; to program a new one,
/// just implement it. Port of INewton: the picture box is replaced by the diagram size, the model returns
/// the color of each pixel and the roots; the UI draws.
/// </summary>
public interface INewton
{
    /// <summary>Width and height of the square diagram in pixels (VB: PicDiagram).</summary>
    int DiagramSize { get; set; }

    GeneralParameter XValueParameter { get; }
    GeneralParameter YValueParameter { get; }

    Interval ActualXRange { get; set; }
    Interval ActualYRange { get; set; }

    /// <summary>Conversion between pixels and the actual ranges.</summary>
    CoordinateTransform Transform { get; }

    bool IsProtocol { set; }

    /// <summary>Using n (number of roots) as parameter.</summary>
    bool IsUseN { get; }

    int N { get; set; }

    /// <summary>Using c as parameter.</summary>
    bool IsUseC { get; }

    ComplexNumber C { set; }

    NewtonMixing UseMixing { set; }

    NewtonColor UseColor { set; }

    bool IsShowBasin { get; }

    /// <summary>The roots of the polynomial (VB: UnitRootCollection), prepared by ResetIteration.</summary>
    IReadOnlyList<UnitRoot> Roots { get; }

    /// <summary>VB: prepares the roots; clearing the picture and drawing the roots is done by the UI.</summary>
    void ResetIteration();

    /// <summary>Computes the pixel (VB coordinates).</summary>
    NewtonPixel IterationStep(int pixelX, int pixelY);
}
