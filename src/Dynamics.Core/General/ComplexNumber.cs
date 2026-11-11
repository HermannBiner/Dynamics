namespace Dynamics.Core.General;

/// <summary>A complex number z = x + iy. Port of ClsComplexNumber.</summary>
public class ComplexNumber
{
    public ComplexNumber()
    {
    }

    public ComplexNumber(double x, double y)
    {
        X = x;
        Y = y;
    }

    public double X { get; set; }
    public double Y { get; set; }

    public double AbsoluteValue => Math.Sqrt(X * X + Y * Y);

    public double Argument => MathHelperComplex.GetArgument(X, Y);

    public ComplexNumber Conjugate => new(X, -Y);

    public ComplexNumber Add(ComplexNumber a) => new(X + a.X, Y + a.Y);

    /// <summary>Multiplies by the real number a.</summary>
    public ComplexNumber Stretch(double a) => new(X * a, Y * a);

    public ComplexNumber Mult(ComplexNumber u) => new(X * u.X - Y * u.Y, X * u.Y + Y * u.X);

    public ComplexNumber Div(ComplexNumber c)
    {
        if (c.AbsoluteValue == 0)
        {
            throw new DivideByZeroException("Division by Zero");
        }

        var nominator = new ComplexNumber(c.X * X + c.Y * Y, c.X * Y - c.Y * X);

        // VB-compatible: the divisor is converted to Decimal and 1 / divisor is a Decimal division.
        var divisor = (decimal)Math.Pow(c.AbsoluteValue, 2);
        return nominator.Stretch((double)(1m / divisor));
    }

    /// <summary>Rotates by the angle phi.</summary>
    public ComplexNumber Rotate(double phi) => Mult(new ComplexNumber(Math.Cos(phi), Math.Sin(phi)));

    public ComplexNumber Invers
    {
        get
        {
            var result = Conjugate;
            return result.Stretch(1 / (result.AbsoluteValue * result.AbsoluteValue));
        }
    }

    public ComplexNumber Power2 => new(X * X - Y * Y, 2 * X * Y);

    /// <summary>
    /// n-th power by repeated multiplication.
    /// VB-compatible: n = 0 returns 0 instead of 1. Simulator never calls it with 0.
    /// </summary>
    public ComplexNumber Power(int n)
    {
        var u = new ComplexNumber(X, Y);
        ComplexNumber result;

        if (n < 0)
        {
            result = u.Invers;
            n = -n;
        }
        else if (n == 0)
        {
            return new ComplexNumber(0, 0);
        }
        else
        {
            result = u;
        }

        // VB-compatible: for n < 0 this multiplies Invers by u (not by Invers), as in the original.
        for (var k = 1; k < n; k++)
        {
            result = result.Mult(u);
        }

        return result;
    }

    /// <summary>Copies the value of w (VB: Equal).</summary>
    public void CopyFrom(ComplexNumber w)
    {
        X = w.X;
        Y = w.Y;
    }
}
