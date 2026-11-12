namespace Dynamics.Core.General;

/// <summary>A vector in R3. Port of ClsVector.</summary>
public class Vector
{
    // VB-compatible: ToMathPoint returns this shared instance, not a new point.
    private readonly MathPoint _mathPoint = new();

    public Vector()
    {
    }

    public Vector(double x, double y)
    {
        X = x;
        Y = y;
    }

    public Vector(double x, double y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }

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

    public void Mult(double factor)
    {
        X *= factor;
        Y *= factor;
        Z *= factor;
    }

    public double Scalar(Vector other) => X * other.X + Y * other.Y + Z * other.Z;

    /// <summary>Cross product. Corrected: VB used Z*X instead of Z*Y in the X component.</summary>
    public Vector Cross(Vector other) => new()
    {
        X = Y * other.Z - Z * other.Y,
        Y = Z * other.X - X * other.Z,
        Z = X * other.Y - Y * other.X,
    };

    /// <summary>
    /// Rotates the projection on the x-y plane by phi; Z is unchanged.
    /// Corrected: VB computed Y with the already rotated X.
    /// </summary>
    public void TurnInXYPlane(double phi)
    {
        var x = X;
        var y = Y;
        X = x * Math.Cos(phi) - y * Math.Sin(phi);
        Y = x * Math.Sin(phi) + y * Math.Cos(phi);
    }

    public double Abs() => Math.Sqrt(X * X + Y * Y + Z * Z);

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
