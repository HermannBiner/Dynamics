using Dynamics.Core.General;
using Dynamics.Core.Pendulum;

namespace Dynamics.Web.Drawing;

/// <summary>
/// Draws the pendulums from their state, as the Draw* methods of the VB pendulum classes did.
/// Persistent layers correspond to the VB bitmaps, overlays to the picture boxes that were refreshed.
/// </summary>
public sealed class PendulumRenderer
{
    public const int DiagramSize = 600;
    public const int PhasePortraitSize = 320;

    private static readonly Interval MathInterval = new(-1.0, 1.0);

    public DrawBatch Diagram { get; } = new(new CoordinateTransform(DiagramSize, DiagramSize, MathInterval, MathInterval));
    public DrawBatch DiagramOverlay { get; } = new(new CoordinateTransform(DiagramSize, DiagramSize, MathInterval, MathInterval));
    public DrawBatch PhasePortrait { get; private set; } = null!;
    public DrawBatch PhasePortraitOverlay { get; private set; } = null!;

    public CoordinateTransform DiagramTransform => Diagram.Transform;

    /// <summary>The phase portrait uses the intervals of the selected phase-portrait type.</summary>
    public void SetPhasePortraitIntervals(IPendulum pendulum)
    {
        var transform = new CoordinateTransform(PhasePortraitSize, PhasePortraitSize, pendulum.UInterval, pendulum.VInterval);
        PhasePortrait = new DrawBatch(transform);
        PhasePortraitOverlay = new DrawBatch(transform);
    }

    /// <summary>VB: ResetIteration + PrepareDiagram: clears everything, draws the axes and the pendulums.</summary>
    public void Prepare(PendulumController controller)
    {
        SetPhasePortraitIntervals(controller.MainDS);

        Diagram.Clear();
        DrawCoordinateSystem(controller.MainDS);
        PhasePortrait.Clear();
        PhasePortraitOverlay.Clear();
        DrawPendulums(controller);
    }

    public void ClearPhasePortrait(PendulumController controller)
    {
        SetPhasePortraitIntervals(controller.MainDS);
        PhasePortrait.Clear();
        PhasePortraitOverlay.Clear();
    }

    private void DrawCoordinateSystem(IPendulum pendulum)
    {
        var a = MathInterval.A;
        var b = MathInterval.B;

        // Combined pendulum: equilibrium line and suspension line Y0.
        foreach (var point in pendulum.GetBackgroundCurve())
        {
            Diagram.Point(point, CanvasColor.Red, 1);
        }
        if (pendulum.Y0 != 0)
        {
            Diagram.Line(new MathPoint(a, pendulum.Y0), new MathPoint(b, pendulum.Y0), CanvasColor.Red, 1);
        }

        Diagram.Line(new MathPoint(a, 0), new MathPoint(b, 0), CanvasColor.Aquamarine, 1);
        Diagram.Line(new MathPoint(0, a), new MathPoint(0, b), CanvasColor.Aquamarine, 1);
    }

    /// <summary>Current position of the main and the shadow pendulum (overlay, redrawn every frame).</summary>
    public void DrawPendulums(PendulumController controller)
    {
        DiagramOverlay.Clear();
        DrawArms(controller.MainDS, CanvasColor.Red);
        if (controller.ShadowDS is { } shadow)
        {
            DrawArms(shadow, CanvasColor.Green);
        }
    }

    private void DrawArms(IPendulum pendulum, CanvasColor pendulumColor)
    {
        foreach (var arm in pendulum.GetArms())
        {
            var color = arm.Role == DrawRole.Primary ? CanvasColor.Blue : pendulumColor;
            DiagramOverlay.Line(arm.From, arm.To, color, 2);
            DiagramOverlay.FillCircle(arm.To, arm.BobRadius, color);
        }
    }

    /// <summary>Track and phase portrait of the last step (VB: DrawTrack, DrawPhaseportrait).</summary>
    public void DrawStep(PendulumController controller)
    {
        PhasePortraitOverlay.Clear();
        DrawStep(controller.MainDS, CanvasColor.Red);
        if (controller.ShadowDS is { } shadow)
        {
            DrawStep(shadow, CanvasColor.Green);
        }
    }

    private void DrawStep(IPendulum pendulum, CanvasColor pendulumColor)
    {
        var (from, to) = pendulum.GetTrackSegment();
        Diagram.Line(from, to, pendulumColor, 1);

        foreach (var point in pendulum.GetPhasePortraitPoints())
        {
            PhasePortrait.Point(point.Point, point.Role == DrawRole.Primary ? CanvasColor.Blue : pendulumColor, point.Size);
        }

        if (pendulum.GetPhasePortraitTangent() is var (a, b))
        {
            PhasePortraitOverlay.Line(a, b, CanvasColor.Blue, 1);
        }
    }
}
