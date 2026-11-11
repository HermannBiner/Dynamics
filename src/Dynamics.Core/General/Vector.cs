namespace Dynamics.Core.General;

/// <summary>A vector in R3. Port of ClsVector.</summary>
public class Vector
{
    // VB-compatible: ToMathPoint returns this shared instance, not a new point.
    private readonly MathPoint _mathPoint = new();

    public Vector()
    {
    }

    public Vector(decimal x, decimal y)
    {
        X = x;
        Y = y;
    }

    public Vector(decimal x, decimal y, decimal z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public decimal X { get; set; }
    public decimal Y { get; set; }
    public decimal Z { get; set; }

    /// <summary>Copies the components of another vector (VB: Equal).</summary>
    public void CopyFrom(Vector other)
    {
        X = other.X;
        Y = other.Y;
        Z = other.Z;
    }

    public void Add(Vector other)
    {
        X += other.X;
        Y += other.Y;
        Z += other.Z;
    }

    public void Minus(Vector other)
    {
        X -= other.X;
        Y -= other.Y;
        Z -= other.Z;
    }

    public void Mult(decimal factor)
    {
        X *= factor;
        Y *= factor;
        Z *= factor;
    }

    public decimal Scalar(Vector other) => X * other.X + Y * other.Y + Z * other.Z;

    /// <summary>
    /// VB-compatible: the original formula is not the cross product
    /// (X uses Z*X instead of Z*Y). Unused in Simulator; kept as is for now.
    /// </summary>
    public Vector Cross(Vector other) => new()
    {
        X = Y * other.Z - Z * other.X,
        Y = Z * other.X - X * other.Z,
        Z = X * other.Y - Y * other.X,
    };

    /// <summary>
    /// Rotates the projection on the x-y plane by phi; Z is unchanged.
    /// VB-compatible: Y is computed with the already rotated X. Unused in Simulator.
    /// </summary>
    public void TurnInXYPlane(decimal phi)
    {
        X = (decimal)((double)X * Math.Cos((double)phi) - (double)Y * Math.Sin((double)phi));
        Y = (decimal)((double)X * Math.Sin((double)phi) + (double)Y * Math.Cos((double)phi));
    }

    public decimal Abs() => (decimal)Math.Sqrt((double)(X * X + Y * Y + Z * Z));

    public MathPoint ToMathPoint()
    {
        _mathPoint.X = X;
        _mathPoint.Y = Y;
        return _mathPoint;
    }

    public void Reset()
    {
        X = 0;
        Y = 0;
        Z = 0;
    }
}
