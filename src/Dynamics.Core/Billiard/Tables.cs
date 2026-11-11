using Dynamics.Core.General;
using static Dynamics.Core.General.MathHelperAngles;

namespace Dynamics.Core.Billiard;

/// <summary>Elliptic billiard table. Port of ClsEllipseBilliardTable.</summary>
public class EllipseBilliardTable : BilliardTableBase
{
    public EllipseBilliardTable()
    {
        MyC = 0.8m;
        MyA = 0.99m;
        MyB = MyA * MyC;
        SetValueParameters(
            new GeneralParameter(1, "Parameter t", new Interval(0m, (decimal)(Math.PI * 2)), GeneralParameter.ParameterType.Variable, (decimal)(Math.PI / 2)),
            new GeneralParameter(2, "Angle Alfa", new Interval(0.001m, 3.13m), GeneralParameter.ParameterType.Variable, 0.001m));
    }

    public override decimal C
    {
        get => MyC;
        set
        {
            MyC = value;
            if (MyC <= 1)
            {
                // a > b; for better visibility a = 0.99 instead of 1
                MyA = 0.99m;
                MyB = MyC * MyA;
            }
            else
            {
                // b > a
                MyB = 0.99m;
                MyA = MyB / MyC;
            }
        }
    }

    protected override BilliardBallBase CreateBall() => new EllipseBilliardBall();

    public override IReadOnlyList<TableShape> DrawBilliardtable()
    {
        var shapes = CoordinateSystem().ToList();

        // The midpoint of the ellipse is always (0, 0)
        shapes.Add(new TableArc(new MathPoint(0, 0), MyA, MyB, 0, 360, Blue));

        // Focal points
        decimal f;
        MathPoint f1, f2;
        if (MyA > MyB)
        {
            f = (decimal)Math.Sqrt((double)(MyA * MyA - MyB * MyB));
            f1 = new MathPoint(f, 0);
            f2 = new MathPoint(-f, 0);
        }
        else
        {
            f = (decimal)Math.Sqrt((double)(MyB * MyB - MyA * MyA));
            f1 = new MathPoint(0, f);
            f2 = new MathPoint(0, -f);
        }

        shapes.Add(new TablePoint(f1, 3, DarkBlue));
        shapes.Add(new TablePoint(f2, 3, DarkBlue));
        return shapes;
    }
}

/// <summary>Ball on the elliptic table. Port of ClsEllipseBilliardball.</summary>
public class EllipseBilliardBall : BilliardBallBase
{
    public override MathPoint PositionFromT(decimal t) =>
        new((decimal)((double)A * Math.Cos((double)t)), (decimal)((double)B * Math.Sin((double)t)));

    public override void SetFirstUserStartposition() => SetUserPositions(new MathPoint(0, B), new MathPoint(A, 0));

    public override decimal SetUserEndposition(MathPoint mouse)
    {
        // The direction from the start position to the mouse; then the end position on the border of the ellipse
        var phi = CalculateAngleOfDirection(mouse.X - UserStartposition.X, mouse.Y - UserStartposition.Y);
        var nextT = ParameterOfNextHitPoint(T, phi);
        SetUserPositions(null, PositionFromT(nextT));
        return phi;
    }

    protected override bool IsBallInside(MathPoint p) =>
        Math.Pow((double)(p.X / A), 2) + Math.Pow((double)(p.Y / B), 2) <= 1;

    /// <summary>See mathematical documentation; theta is the parameter of a vector parallel to phi.</summary>
    protected override decimal ParameterOfNextHitPoint(decimal t, decimal phi)
    {
        var theta = CalculateAngleOfDirection((decimal)((double)B * Math.Cos((double)phi)), (decimal)((double)A * Math.Sin((double)phi)));

        // u is the parameter where the straight line of the orbit intersects the ellipse
        var u = (decimal)(-2 * Math.Cos((double)(t - theta)));

        var nextT = (decimal)Math.Min(1, Math.Cos((double)t) + (double)u * Math.Cos((double)theta));
        nextT = Math.Max(-1, nextT);
        nextT = (decimal)Math.Acos((double)nextT);

        // The solution above is in [0, π]; the other one is in [π, 2π]
        if (Math.Sin((double)t) + (double)u * Math.Sin((double)theta) <= 0)
        {
            nextT = (decimal)(2 * Math.PI - (double)nextT);
        }

        return AngleInNullTwoPi(nextT);
    }

    protected override decimal CalculatePsi(decimal t) =>
        CalculateAngleOfDirection((decimal)((double)(-A) * Math.Sin((double)t)), (decimal)((double)B * Math.Cos((double)t)));
}

/// <summary>Oval billiard table: right half circle, left half ellipse. Port of ClsOvalBilliardTable.</summary>
public class OvalBilliardTable : BilliardTableBase
{
    public OvalBilliardTable()
    {
        SetValueParameters(
            new GeneralParameter(1, "Parameter t", new Interval(0m, (decimal)(Math.PI * 2)), GeneralParameter.ParameterType.Variable, (decimal)(Math.PI / 2)),
            new GeneralParameter(2, "Angle Alfa", new Interval(0.001m, 3.13m), GeneralParameter.ParameterType.Variable, (decimal)(Math.PI / 30)));
        C = 0.5m;
    }

    /// <summary>VB: CDec(1.9 · Min(1 / (1 + c), 1 / (2c))); the minimum is computed in Decimal.</summary>
    public override decimal C
    {
        get => MyC;
        set
        {
            MyC = value;
            MyA = (decimal)(1.9 * (double)Math.Min(1 / (1 + MyC), 1 / (2 * MyC)));
            MyB = MyC * MyA;
        }
    }

    protected override BilliardBallBase CreateBall() => new OvalBilliardBall();

    public override IReadOnlyList<TableShape> DrawBilliardtable()
    {
        var shapes = CoordinateSystem().ToList();
        var midpoint = new MathPoint((MyA - MyB) / 2, 0);
        var focalpoint = MyA >= MyB
            ? new MathPoint(midpoint.X - (decimal)Math.Sqrt((double)(MyA * MyA - MyB * MyB)), 0)
            : new MathPoint(midpoint.X, (decimal)Math.Sqrt((double)(MyB * MyB - MyA * MyA)));

        shapes.Add(new TablePoint(midpoint, 2, Blue));
        shapes.Add(new TablePoint(focalpoint, 2, Blue));

        // right half circle (VB: DrawCircleArc from 3π/2 over π) and left half ellipse (from π/2 over π)
        shapes.Add(new TableArc(midpoint, MyB, MyB, RadiantToDegree((decimal)(3 * Math.PI / 2)), RadiantToDegree((decimal)Math.PI), Blue));
        shapes.Add(new TableArc(midpoint, MyA, MyB, RadiantToDegree((decimal)(Math.PI / 2)), RadiantToDegree((decimal)Math.PI), Blue));
        return shapes;
    }

    /// <summary>VB ClsGraphicTool.RadiantToDegree: CInt(180 · arc / π).</summary>
    internal static int RadiantToDegree(decimal arc) => Convert.ToInt32(180 * (double)arc / Math.PI);
}

/// <summary>Ball on the oval table. Port of ClsOvalBilliardball.</summary>
public class OvalBilliardBall : BilliardBallBase
{
    private decimal M => (A - B) / 2;

    /// <summary>VB: (0 ≤ t ≤ π/2) or (3π/2 ≤ t &lt; 2π), the comparisons in Double.</summary>
    private static bool IsRightHalf(decimal t)
    {
        var d = (double)t;
        return (0 <= d && d <= Math.PI / 2) || (3 * Math.PI / 2 <= d && d < 2 * Math.PI);
    }

    public override MathPoint PositionFromT(decimal t)
    {
        t = AngleInNullTwoPi(t);
        var x = IsRightHalf(t)
            ? M + B * (decimal)Math.Cos((double)t)
            : M + A * (decimal)Math.Cos((double)t);
        return new MathPoint(x, B * (decimal)Math.Sin((double)t));
    }

    private decimal TFromPosition(MathPoint point) => point.X >= M
        ? CalculateAngleOfDirection(point.X - M, point.Y)
        : CalculateAngleOfDirection((point.X - M) / A, point.Y / B);

    public override void SetFirstUserStartposition() =>
        SetUserPositions(new MathPoint((A - B) / 2, B), new MathPoint(A + B, 0));

    /// <summary>
    /// The end position is the border point in the direction of the mouse seen from the center of the oval.
    /// Corrected: VB used the angle seen from (0, 0), so the end point was not under the mouse.
    /// </summary>
    public override decimal SetUserEndposition(MathPoint mouse)
    {
        var end = PositionFromT(TFromPosition(mouse));
        SetUserPositions(null, end);
        return CalculateAngleOfDirection(end.X - UserStartposition.X, end.Y - UserStartposition.Y);
    }

    protected override bool IsBallInside(MathPoint p)
    {
        var m = M;
        return p.X >= m
            ? Math.Pow((double)(p.X - m), 2) + Math.Pow((double)p.Y, 2) <= (double)(B * B)
            : Math.Pow((double)((p.X - m) / A), 2) + Math.Pow((double)(p.Y / B), 2) <= 1;
    }

    protected override decimal ParameterOfNextHitPoint(decimal t, decimal phi)
    {
        var m = M;
        MathPoint? intersection = null;
        var startpoint = PositionFromT(t);
        var p = startpoint.X;
        var q = startpoint.Y;
        var cos = Math.Cos((double)phi);
        var sin = Math.Sin((double)phi);

        // Intersection with the half circle (x ≥ m)
        var da = B * B;
        var db = B * B * (decimal)((double)(p - m) * cos + (double)q * sin);
        var dc = B * B * ((decimal)Math.Pow((double)(p - m), 2) + q * q - B * B);
        intersection = Candidate(da, db, dc, x => x >= m);

        // Intersection with the half ellipse (x < m)
        if (intersection is null)
        {
            da = (decimal)(Math.Pow((double)A * sin, 2) + Math.Pow((double)B * cos, 2));
            db = (decimal)((double)(A * A * q) * sin + (double)(B * B * (p - m)) * cos);
            dc = (decimal)(Math.Pow((double)(B * (p - m)), 2) + (double)(A * A * (q * q - B * B)));
            intersection = Candidate(da, db, dc, x => x < m);
        }

        if (intersection is null)
        {
            throw new ArgumentException(Text.Get("NoIntersectionPoint") + ", t: " + t + ", phi: " + phi);
        }

        // VB-compatible: AngleInNullTwoPi was called, but its result not used
        return TFromPosition(intersection);

        MathPoint? Candidate(decimal a, decimal b, decimal c, Func<decimal, bool> isOnPart)
        {
            if (b * b - a * c < 0)
            {
                return null;
            }

            foreach (var sign in new[] { 1, -1 })
            {
                var u = (-b + sign * (decimal)Math.Sqrt((double)(b * b - a * c))) / a;
                if ((double)Math.Abs(u) > 0.000001)
                {
                    var candidate = new MathPoint(startpoint.X + u * (decimal)cos, startpoint.Y + u * (decimal)sin);
                    if (isOnPart(candidate.X))
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }
    }

    protected override decimal CalculatePsi(decimal t)
    {
        t = AngleInNullTwoPi(t);
        var x = IsRightHalf(t)
            ? -B * (decimal)Math.Sin((double)t)
            : -A * (decimal)Math.Sin((double)t);
        return CalculateAngleOfDirection(x, B * (decimal)Math.Cos((double)t));
    }
}

/// <summary>Stadium billiard table: two half circles connected by straight lines. Port of ClsStadiumBilliardTable.</summary>
public class StadiumBilliardTable : BilliardTableBase
{
    public StadiumBilliardTable()
    {
        // VB-compatible: the default of t uses c before it is set (c = 0)
        SetValueParameters(
            new GeneralParameter(1, "Parameter t", new Interval(0m, (decimal)(0.95 * (2 + Math.PI))), GeneralParameter.ParameterType.Variable, 0.95m / (1 + MyC)),
            new GeneralParameter(2, "Angle Alfa", new Interval(0.01m, 3.13m), GeneralParameter.ParameterType.Variable, (decimal)(Math.PI / 30)));
        C = 2;
    }

    public override decimal C
    {
        get => MyC;
        set
        {
            MyC = value;
            MyA = 0.95m / (1 + MyC);
            MyB = MyC * MyA;
        }
    }

    protected override BilliardBallBase CreateBall() => new StadiumBilliardBall();

    public override IReadOnlyList<TableShape> DrawBilliardtable()
    {
        var shapes = CoordinateSystem().ToList();
        shapes.Add(new TableLine(new MathPoint(-MyA, -MyB), new MathPoint(MyA, -MyB), Blue));
        shapes.Add(new TableLine(new MathPoint(-MyA, MyB), new MathPoint(MyA, MyB), Blue));
        shapes.Add(new TableArc(new MathPoint(-MyA, 0), MyB, MyB, OvalBilliardTable.RadiantToDegree((decimal)(Math.PI / 2)),
            OvalBilliardTable.RadiantToDegree((decimal)Math.PI), Blue));
        shapes.Add(new TablePoint(new MathPoint(-MyA, 0), 2, Blue));
        shapes.Add(new TableArc(new MathPoint(MyA, 0), MyB, MyB, OvalBilliardTable.RadiantToDegree((decimal)(3 * Math.PI / 2)),
            OvalBilliardTable.RadiantToDegree((decimal)Math.PI), Blue));
        shapes.Add(new TablePoint(new MathPoint(MyA, 0), 2, Blue));
        return shapes;
    }
}

/// <summary>
/// Ball on the stadium table; t is the arc length from (a, b) counterclockwise. Port of ClsStadiumBilliardball.
/// </summary>
public class StadiumBilliardBall : BilliardBallBase
{
    private static readonly decimal Pi = (decimal)Math.PI;

    public override MathPoint PositionFromT(decimal t)
    {
        t %= 4 * A + 2 * B * Pi;
        if (t <= 2 * A)
        {
            return new MathPoint(A - t, B);
        }

        if (t > 2 * A && t < 2 * A + B * Pi)
        {
            var angle = (t - 2 * A) / B;
            return new MathPoint(-A - B * (decimal)Math.Sin((double)angle), B * (decimal)Math.Cos((double)angle));
        }

        if (t >= 2 * A + B * Pi && t <= 4 * A + B * Pi)
        {
            return new MathPoint(t - 3 * A - B * Pi, -B);
        }

        var angle2 = (t - 4 * A - B * Pi) / B;
        return new MathPoint(A + B * (decimal)Math.Sin((double)angle2), -B * (decimal)Math.Cos((double)angle2));
    }

    private decimal TFromPosition(MathPoint point)
    {
        if (-A <= point.X && point.X <= A)
        {
            return point.Y >= 0 ? A - point.X : 3 * A + B * Pi + point.X;
        }

        if (point.X < -A)
        {
            var sinValue = Math.Min(1, (-point.X - A) / B);
            return point.Y >= 0
                ? 2 * A + B * (decimal)Math.Asin((double)sinValue)
                : 2 * A + B * (Pi - (decimal)Math.Asin((double)sinValue));
        }

        var sinValue2 = Math.Min(1, (point.X - A) / B);
        return point.Y <= 0
            ? 4 * A + B * (Pi + (decimal)Math.Asin((double)sinValue2))
            : 4 * A + B * (2 * Pi - (decimal)Math.Asin((double)sinValue2));
    }

    public override decimal SetUserStartposition(MathPoint mouse)
    {
        T = TFromPosition(mouse);
        SetUserPositions(PositionFromT(T), null);
        return T;
    }

    public override void SetFirstUserStartposition() => SetUserPositions(new MathPoint(A, B), new MathPoint(-A, -B));

    public override decimal SetUserEndposition(MathPoint mouse)
    {
        var end = PositionFromT(TFromPosition(mouse));
        SetUserPositions(null, end);
        return CalculateAngleOfDirection(end.X - UserStartposition.X, end.Y - UserStartposition.Y);
    }

    /// <summary>
    /// Corrected: VB checked the half circles around (0, 0) instead of (∓a, 0), so in the curves the moving ball
    /// jumped to the end of the segment too early.
    /// </summary>
    protected override bool IsBallInside(MathPoint p)
    {
        if (-A <= p.X && p.X <= A)
        {
            return -B <= p.Y && p.Y <= B;
        }

        var center = p.X < -A ? -A : A;
        return (p.X - center) * (p.X - center) + p.Y * p.Y <= B * B;
    }

    protected override decimal ParameterOfNextHitPoint(decimal s, decimal phi)
    {
        phi = AngleInNullTwoPi(phi);
        var startpoint = PositionFromT(s);
        var cos = (decimal)Math.Cos((double)phi);
        var sin = (decimal)Math.Sin((double)phi);
        decimal x;
        decimal t;

        // the straight lines y = ±b
        if (phi != 0 && Math.Abs((double)phi - Math.PI) > 0)
        {
            foreach (var y in new[] { B, -B })
            {
                t = (decimal)((double)(y - startpoint.Y) / Math.Sin((double)phi));
                x = startpoint.X + t * cos;
                if (Math.Abs(x) <= A && (double)Math.Abs(t) > 0.0000001)
                {
                    return TFromPosition(new MathPoint(x, y));
                }
            }
        }

        // the right half circle (x > a), then the left one (x < -a)
        foreach (var center in new[] { A, -A })
        {
            var tempB = (decimal)((double)(startpoint.X - center) * Math.Cos((double)phi) + (double)startpoint.Y * Math.Sin((double)phi));
            var tempC = (startpoint.X - center) * (startpoint.X - center) + startpoint.Y * startpoint.Y - B * B;
            if (tempB * tempB - tempC < 0)
            {
                continue;
            }

            foreach (var sign in new[] { 1, -1 })
            {
                t = -tempB + sign * (decimal)Math.Sqrt((double)(tempB * tempB - tempC));
                x = startpoint.X + t * cos;
                var isOnPart = center > 0 ? x > A : x < -A;
                if (isOnPart && (double)Math.Abs(t) > 0.0000001)
                {
                    return TFromPosition(new MathPoint(x, startpoint.Y + t * sin));
                }
            }
        }

        throw new ArgumentException(Text.Get("NoIntersectionPoint"));
    }

    protected override decimal CalculatePsi(decimal t)
    {
        if (t <= 2 * A)
        {
            return Pi;
        }

        if (t > 2 * A && t < 2 * A + B * Pi)
        {
            return Pi + (t - 2 * A) / B;
        }

        if (t >= 2 * A + B * Pi && t <= 4 * A + B * Pi)
        {
            return 0;
        }

        return (t - 4 * A - B * Pi) / B;
    }
}
