namespace Dynamics.Core.General;

// Ports of ClsMathHelperAngles, ClsMathHelperComplex and ClsMathHelperPendulum.
// VB mixed Decimal and Double; since step 6 of the port everything is Double.

/// <summary>Angle calculations used by billiard tables and balls.</summary>
public static class MathHelperAngles
{
    /// <summary>Angle between (deltaX, deltaY) and the positive x-axis, in [0, 2π[.</summary>
    public static double CalculateAngleOfDirection(double deltaX, double deltaY)
    {
        if (deltaX > 0)
        {
            return deltaY >= 0
                ? Math.Atan((double)(deltaY / deltaX))
                : (2 * Math.PI + Math.Atan((double)(deltaY / deltaX)));
        }

        if (deltaX < 0)
        {
            return (Math.PI + Math.Atan((double)(deltaY / deltaX)));
        }

        return deltaY > 0 ? (Math.PI / 2) : (Math.PI * 3 / 2);
    }

    /// <summary>Maps an angle into [0, 2π[.</summary>
    public static double AngleInNullTwoPi(double angle)
    {
        var tempAngle = NumericCheck.ReduceAngle(angle);

        while (!(tempAngle > 0))
        {
            tempAngle = ((double)tempAngle + Math.PI * 2);
        }

        return ((double)tempAngle % (Math.PI * 2));
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
    public static double GetAngle(double x, double y)
    {
        if (y == 0)
        {
            return x > 0 ? (Math.PI / 2) : -(Math.PI / 2);
        }

        var alfa = -Math.Atan((double)(x / y));

        if (y < 0)
        {
            return alfa;
        }

        return x < 0 ? alfa - Math.PI : alfa + Math.PI;
    }

    /// <summary>Maps an angle into ]-π, π].</summary>
    public static double AngleInMinusPiAndPi(double angle)
    {
        var tempAngle = NumericCheck.ReduceAngle(angle);

        while (!(tempAngle > -Math.PI))
        {
            tempAngle = ((double)tempAngle + Math.PI * 2);
        }

        while (!(tempAngle <= Math.PI))
        {
            tempAngle = ((double)tempAngle - Math.PI * 2);
        }

        return tempAngle;
    }
}
