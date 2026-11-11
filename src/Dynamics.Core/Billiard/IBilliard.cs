using Dynamics.Core.General;

namespace Dynamics.Core.Billiard;

/// <summary>A point of the phase portrait: parameter t of the hit point and reflection angle alfa.</summary>
public readonly record struct PhasePoint(decimal T, decimal Alfa);

/// <summary>
/// Result of one iteration step of a ball: the segment from the actual to the next hit point, the positions of
/// the moving ball on it (VB: MoveOnSegment), the new point of the phase portrait and the protocol line.
/// </summary>
public sealed record BilliardStep(MathPoint Start, MathPoint End, IReadOnlyList<MathPoint> Positions, PhasePoint? Phase, string? Protocol);

/// <summary>
/// Interface for all kinds of billiard balls: a ball knows the profile of its table and the rules of its movement.
/// Port of IBilliardball; the picture boxes are replaced by the returned positions and points, the UI draws.
/// </summary>
public interface IBilliardBall
{
    /// <summary>The profile of the table is defined by a and b.</summary>
    decimal A { get; set; }
    decimal B { get; set; }

    /// <summary>ARGB color and its name (key of the texts, e.g. "Red").</summary>
    int Color { get; set; }
    string ColorName { get; set; }

    /// <summary>VB BallSpeed: the ball moves IntervalWidth · speed / 1000 of a segment per drawing step.</summary>
    int Speed { get; set; }

    bool IsProtocol { set; }

    /// <summary>VB: the ball got a phase portrait (the billiard table, not the C-diagram).</summary>
    bool HasPhasePortrait { get; set; }

    /// <summary>VB: the ball got the protocol list (not the balls of the filled phase portrait).</summary>
    bool HasValueProtocol { get; set; }

    /// <summary>Parameter t of the actual hit point; setting it puts the ball on the table (VB: Startparameter).</summary>
    decimal Startparameter { get; set; }

    /// <summary>Setting the start angle alfa computes the first hit point (VB: Startangle).</summary>
    decimal Startangle { set; }

    bool IsStartParameterSet { get; set; }
    bool IsStartangleSet { get; set; }

    MathPoint UserStartposition { get; }
    MathPoint UserEndposition { get; }

    /// <summary>The actual direction of the movement (angle to the positive x-axis).</summary>
    decimal Phi { get; }

    /// <summary>VB: SetUserStartposition; the mouse position in mathematical coordinates; returns t.</summary>
    decimal SetUserStartposition(MathPoint mouse);

    /// <summary>VB: SetUserEndposition; returns the direction phi of the first movement.</summary>
    decimal SetUserEndposition(MathPoint mouse);

    /// <summary>VB: DrawFirstUserStartposition, the default positions before the mouse sets them.</summary>
    void SetFirstUserStartposition();

    BilliardStep IterationStep();

    /// <summary>The next pair (t, alfa) of the iteration (used by the C-diagram).</summary>
    DecValuePair GetNextValuePair(DecValuePair actualPair);

    /// <summary>The reflection angle between the movement phi and the tangent in the hit point t.</summary>
    decimal CalculateAlfa(decimal t, decimal phi);
}

/// <summary>A shape of the billiard table drawing: lines, ellipse arcs (angles as in GDI) and points.</summary>
public abstract record TableShape(int Color);

public sealed record TableLine(MathPoint A, MathPoint B, int Color) : TableShape(Color);

/// <summary>
/// Arc of the ellipse with center and half axes; StartAngle and Sweep in degrees, measured clockwise on the screen
/// from the positive x-axis (as Graphics.DrawArc). A full ellipse has Sweep 360.
/// </summary>
public sealed record TableArc(MathPoint Center, decimal RadiusX, decimal RadiusY, int StartAngle, int Sweep, int Color) : TableShape(Color);

public sealed record TablePoint(MathPoint Point, int Size, int Color) : TableShape(Color);

/// <summary>
/// Interface for all kinds of billiard tables. Port of IBilliardTable; the table returns its shapes instead of drawing.
/// </summary>
public interface IBilliardTable
{
    List<IBilliardBall> BilliardBallCollection { get; }

    Interval MathInterval { get; }

    /// <summary>The parameter c defining the form of the table.</summary>
    GeneralParameter DSParameter { get; }

    GeneralParameter TValueParameter { get; }
    GeneralParameter AlfaValueParameter { get; }
    IReadOnlyList<GeneralParameter> ValueParameterList { get; }

    decimal C { get; set; }
    decimal A { get; }
    decimal B { get; }

    /// <summary>Clears all balls (the UI clears the pictures).</summary>
    void ResetIteration();

    /// <summary>A new ball for this table.</summary>
    IBilliardBall GetBilliardBall();

    /// <summary>VB: DrawBilliardtable.</summary>
    IReadOnlyList<TableShape> DrawBilliardtable();
}
