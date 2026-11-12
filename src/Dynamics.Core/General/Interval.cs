namespace Dynamics.Core.General;

/// <summary>An interval [A,B] of real numbers with A &lt; B. Port of ClsInterval (VB kept the borders in Decimal and Double).</summary>
public class Interval
{
    public Interval(double a, double b)
    {
        if (a >= b)
        {
            throw new ArgumentException(Text.Get("LeftSmallerRight"));
        }

        A = a;
        B = b;
    }

    /// <summary>Copy constructor.</summary>
    public Interval(Interval other)
    {
        A = other.A;
        B = other.B;
    }

    public double A { get; }
    public double B { get; }

    public double IntervalWidth => B - A;

    public bool IsNumberInInterval(double number) => number >= A && number <= B;

    public bool IsInterval2PartOfInterval(Interval interval2) => interval2.A >= A && interval2.B <= B;
}
