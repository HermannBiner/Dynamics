using Dynamics.Core.General;

namespace Dynamics.Core.StrangeAttractor;

/// <summary>
/// Interface for all types of strange attractors. Port of IStrangeAttractor.
/// </summary>
public interface IStrangeAttractor
{
    /// <summary>Definition of the parameter relevant for the chaotic behaviour.</summary>
    GeneralParameter DSParameter { get; }

    decimal DSParameterValue { get; set; }

    GeneralParameter XValueParameter { get; }
    GeneralParameter YValueParameter { get; }
    GeneralParameter ZValueParameter { get; }

    decimal StepWidth { get; set; }

    IReadOnlyList<PointSet> StartPointSets { get; }

    Interval MathInterval { get; }

    /// <summary>Split points of the Hopf bifurcation diagram.</summary>
    IReadOnlyList<decimal> Splitpoints { get; }

    /// <summary>The point that is iterated; setting copies the coordinates (VB: Equal).</summary>
    MathPoint ActualMathPoint { get; set; }

    /// <summary>Adjusts the position in the diagram depending on the attractor (3D, XY, XZ).</summary>
    NTuple ViewCorrection { get; }

    void CreateStartPointSets();

    void SetIterationParameters();

    void IterationStep();
}

/// <summary>Colors of the iteration points (System.Drawing brushes of VB, as ARGB).</summary>
public static class AttractorColors
{
    public const int Aqua = unchecked((int)0xFF00FFFF);
    public const int Blue = unchecked((int)0xFF0000FF);
    public const int Cyan = unchecked((int)0xFF00FFFF);
    public const int Green = unchecked((int)0xFF008000);
    public const int LightBlue = unchecked((int)0xFFADD8E6);
    public const int LightGreen = unchecked((int)0xFF90EE90);
    public const int Lime = unchecked((int)0xFF00FF00);
    public const int LimeGreen = unchecked((int)0xFF32CD32);
    public const int Magenta = unchecked((int)0xFFFF00FF);
    public const int Orange = unchecked((int)0xFFFFA500);
    public const int OrangeRed = unchecked((int)0xFFFF4500);
    public const int Pink = unchecked((int)0xFFFFC0CB);
    public const int Red = unchecked((int)0xFFFF0000);
    public const int Yellow = unchecked((int)0xFFFFFF00);
}

/// <summary>A point in the iteration process moving to a strange attractor. Port of ClsIterationPoint.</summary>
public class IterationPoint(decimal x, decimal y, decimal z, int color)
{
    public MathPoint DefaultStartPoint { get; } = new(x, y, z);
    public MathPoint UserStartPoint { get; } = new(x, y, z);
    public MathPoint ActualPoint { get; } = new(x, y, z);

    /// <summary>ARGB color.</summary>
    public int Color { get; } = color;

    public void SetDefaultUserData()
    {
        UserStartPoint.CopyFrom(DefaultStartPoint);
        ActualPoint.CopyFrom(DefaultStartPoint);
    }

    public void ResetIteration() => ActualPoint.CopyFrom(UserStartPoint);
}

/// <summary>All points of a start constellation (one point, two points or a cloud). Port of ClsPointSet.</summary>
public class PointSet(string name)
{
    private readonly List<IterationPoint> _points = [];

    public string Name { get; } = name;

    public decimal ProposedStepWidth { get; set; }

    public IReadOnlyList<IterationPoint> Points => _points;

    public void AddPoint(IterationPoint point) => _points.Add(point);

    public int Count => _points.Count;
}
