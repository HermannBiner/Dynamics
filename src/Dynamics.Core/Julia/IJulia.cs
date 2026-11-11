using Dynamics.Core.General;

namespace Dynamics.Core.Julia;

/// <summary>
/// Result of one iteration step: the number of steps until the orbit escaped
/// (MaxSteps + 1 if it did not), the color of the pixel and an optional protocol entry.
/// </summary>
public readonly record struct JuliaPixel(int Steps, int Color, string? ProtocolEntry);

/// <summary>
/// Interface for any kind of Julia set; to program a new Julia set, just implement it.
/// Port of IJulia. The picture box is replaced by the diagram size in pixels; the model returns
/// the color of each pixel and the UI draws it.
/// </summary>
public interface IJulia
{
    /// <summary>Width and height of the square diagram in pixels (VB: PicDiagram).</summary>
    int DiagramSize { get; set; }

    /// <summary>Value parameter definition for the x-values.</summary>
    GeneralParameter XValueParameter { get; }

    /// <summary>Value parameter definition for the y-values.</summary>
    GeneralParameter YValueParameter { get; }

    Interval ActualXRange { get; set; }
    Interval ActualYRange { get; set; }

    /// <summary>Parameter c.</summary>
    ComplexNumber C { set; }

    /// <summary>Power of z.</summary>
    int N { set; }

    bool IsProtocol { set; }

    /// <summary>Color intensity 0..1 of the user defined colors.</summary>
    double RedPercent { set; }
    double GreenPercent { set; }
    double BluePercent { set; }

    bool IsUseSystemColors { set; }

    /// <summary>True: it is a landscape of Julia parameters.</summary>
    bool IsMandelbrot { get; }

    /// <summary>Conversion between pixels and the actual ranges.</summary>
    CoordinateTransform Transform { get; }

    void ResetIteration();

    /// <summary>Computes the pixel (VB coordinates: 1..DiagramSize, y downwards).</summary>
    JuliaPixel IterationStep(int pixelX, int pixelY);
}
