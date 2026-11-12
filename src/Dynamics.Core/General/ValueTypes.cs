namespace Dynamics.Core.General;

// Ports of ClsMathpoint, ClsDecValuePair, ClsDblValuePair, ClsNTupel.
// These stay mutable reference types like in VB, because the models share and modify instances.

/// <summary>A point in mathematical coordinates.</summary>
public class MathPoint
{
    public MathPoint()
    {
    }

    public MathPoint(double x, double y)
    {
        X = x;
        Y = y;
    }

    public MathPoint(double x, double y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }

    /// <summary>Copies the coordinates of another point (VB: Equal).</summary>
    public void CopyFrom(MathPoint other)
    {
        X = other.X;
        Y = other.Y;
        Z = other.Z;
    }
}

/// <summary>A value pair describing the state of a dynamic system.</summary>
public class DecValuePair(double x, double y)
{
    public double X { get; set; } = x;
    public double Y { get; set; } = y;
}

/// <summary>A value pair describing the state of a dynamic system.</summary>
public class DblValuePair(double x, double y)
{
    public double X { get; set; } = x;
    public double Y { get; set; } = y;
}

/// <summary>A vector of decimals with the dimension given in the constructor.</summary>
public class NTuple
{
    private readonly double[] _components;

    /// <summary>
    /// VB-compatible: like <c>ReDim MyComponents(N)</c>, valid indices are 0..n (n + 1 components).
    /// The models use indices 0 and 1 for NTuple(1).
    /// </summary>
    public NTuple(int n)
    {
        Dimension = n;
        _components = new double[n + 1];
    }

    public int Dimension { get; }

    public double this[int index]
    {
        get => _components[index];
        set => _components[index] = value;
    }
}
