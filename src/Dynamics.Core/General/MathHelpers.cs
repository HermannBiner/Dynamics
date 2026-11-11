namespace Dynamics.Core.General;

// Ports of ClsMathHelperAngles, ClsMathHelperComplex and ClsMathHelperPendulum.
// The casts reproduce VB's mixed Decimal/Double arithmetic exactly:
// Decimal op Double is evaluated in Double, CDec(...) becomes (decimal)(...).

/// <summary>Angle calculations used by billiard tables and balls.</summary>
public static class MathHelperAngles
{
    /// <summary>Angle between (deltaX, deltaY) and the positive x-axis, in [0, 2π[.</summary>
    public static decimal CalculateAngleOfDirection(decimal deltaX, decimal deltaY)
    {
        if (deltaX > 0)
        {
            return deltaY >= 0
                ? (decimal)Math.Atan((double)(deltaY / deltaX))
                : (decimal)(2 * Math.PI + Math.Atan((double)(deltaY / deltaX)));
        }

        if (deltaX < 0)
        {
            return (decimal)(Math.PI + Math.Atan((double)(deltaY / deltaX)));
        }

        return deltaY > 0 ? (decimal)(Math.PI / 2) : (decimal)(Math.PI * 3 / 2);
    }

    /// <summary>Maps an angle into [0, 2π[.</summary>
    public static decimal AngleInNullTwoPi(decimal angle)
    {
        var tempAngle = angle;

        while (!(tempAngle > 0))
        {
            tempAngle = (decimal)((double)tempAngle + Math.PI * 2);
        }

        return (decimal)((double)tempAngle % (Math.PI * 2));
    }
}

/// <summary>Helpers for complex numbers.</summary>
public static class MathHelperComplex
{
    /// <summary>Argument of the point (x, y) in [0, 2π[; the argument of (0, 0) is 0.</summary>
    public static double GetArgument(double x, double y)
    {
        if (x == 0)
        {
            return y switch
            {
                > 0 => Math.PI / 2,
                0 => 0,
                _ => 3 * Math.PI / 2,
            };
        }

        var phi = Math.Atan(y / x);

        if (x < 0)
        {
            return phi + Math.PI;
        }

        return y < 0 ? phi + 2 * Math.PI : phi;
    }
}

/// <summary>Angle calculations used by the pendulums.</summary>
public static class MathHelperPendulum
{
    /// <summary>Angle between the negative y-axis and the pendulum axis (x, y).</summary>
    public static decimal GetAngle(decimal x, decimal y)
    {
        if (y == 0)
        {
            return x > 0 ? (decimal)(Math.PI / 2) : -(decimal)(Math.PI / 2);
        }

        var alfa = -(decimal)Math.Atan((double)(x / y));

        if (y < 0)
        {
            return alfa;
        }

        return x < 0 ? alfa - (decimal)Math.PI : alfa + (decimal)Math.PI;
    }

    /// <summary>Maps an angle into ]-π, π].</summary>
    public static decimal AngleInMinusPiAndPi(decimal angle)
    {
        var tempAngle = angle;

        while (!(tempAngle > -(decimal)Math.PI))
        {
            tempAngle = (decimal)((double)tempAngle + Math.PI * 2);
        }

        while (!(tempAngle <= (decimal)Math.PI))
        {
            tempAngle = (decimal)((double)tempAngle - Math.PI * 2);
        }

        return tempAngle;
    }
}
