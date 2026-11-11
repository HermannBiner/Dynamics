namespace Dynamics.Core.General;

/// <summary>An interval [A,B] of real numbers with A &lt; B. Port of ClsInterval.</summary>
public class Interval
{
    public Interval(decimal a, decimal b)
    {
        if (a >= b)
        {
            throw new ArgumentException(Text.Get("LeftSmallerRight"));
        }

        A = a;
        B = b;
        DblA = (double)a;
        DblB = (double)b;
    }

    public Interval(double dblA, double dblB)
    {
        if (dblA >= dblB)
        {
            throw new ArgumentException(Text.Get("LeftSmallerRight"));
        }

        A = (decimal)dblA;
        B = (decimal)dblB;
        DblA = dblA;
        DblB = dblB;
    }

    /// <summary>Copy constructor. Corrected: VB copied only A and B, DblA and DblB stayed 0.</summary>
    public Interval(Interval other)
    {
        A = other.A;
        B = other.B;
        DblA = other.DblA;
        DblB = other.DblB;
    }

    public decimal A { get; }
    public decimal B { get; }
    public double DblA { get; }
    public double DblB { get; }

    public decimal IntervalWidth => B - A;
    public double DblIntervalWidth => DblB - DblA;

    public bool IsNumberInInterval(int number) => number >= A && number <= B;
    public bool IsNumberInInterval(decimal number) => number >= A && number <= B;
    public bool IsNumberInInterval(double number) => number >= DblA && number <= DblB;

    public bool IsInterval2PartOfInterval(Interval interval2) => interval2.A >= A && interval2.B <= B;
}
