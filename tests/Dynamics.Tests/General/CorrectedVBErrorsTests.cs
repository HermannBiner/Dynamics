using Dynamics.Core.General;

namespace Dynamics.Tests.General;

/// <summary>Mathematical checks of the functions whose VB errors were corrected in Dynamics.</summary>
public class CorrectedVBErrorsTests
{
    [Fact]
    public void CrossProductOfUnitVectors()
    {
        var e1 = new Vector(1, 0, 0);
        var e2 = new Vector(0, 1, 0);
        var e3 = e1.Cross(e2);
        Assert.Equal((0m, 0m, 1m), (e3.X, e3.Y, e3.Z));

        var e1Again = e2.Cross(e3);
        Assert.Equal((1m, 0m, 0m), (e1Again.X, e1Again.Y, e1Again.Z));
    }

    [Fact]
    public void CrossProductIsOrthogonalAndAnticommutative()
    {
        var v = new Vector(1.5m, -2m, 0.25m);
        var w = new Vector(-0.5m, 3m, 4m);
        var c = v.Cross(w);
        var d = w.Cross(v);

        Assert.Equal(0m, c.Scalar(v));
        Assert.Equal(0m, c.Scalar(w));
        Assert.Equal((-c.X, -c.Y, -c.Z), (d.X, d.Y, d.Z));
    }

    [Fact]
    public void TurnInXYPlaneRotates()
    {
        var v = new Vector(1, 0, 7);
        v.TurnInXYPlane((decimal)(Math.PI / 2));
        Assert.Equal(0, (double)v.X, 12);
        Assert.Equal(1, (double)v.Y, 12);
        Assert.Equal(7m, v.Z);

        // The length in the plane is preserved.
        var w = new Vector(0.6m, 0.8m, 0);
        w.TurnInXYPlane(1.234m);
        Assert.Equal(1, (double)w.Abs(), 12);
    }

    [Fact]
    public void IntervalCopyKeepsDoubleBorders()
    {
        var copy = new Interval(new Interval(-1.5m, 2.5m));
        Assert.Equal((-1.5, 2.5), (copy.DblA, copy.DblB));
        Assert.Equal(4.0, copy.DblIntervalWidth);
        Assert.True(copy.IsNumberInInterval(2.0));
    }

    [Theory]
    [InlineData(-3)]
    [InlineData(-2)]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(2)]
    public void PowerTimesInversePowerIsOne(int n)
    {
        var z = new ComplexNumber(0.7, -1.3);
        var product = z.Power(n).Mult(z.Power(-n));
        Assert.Equal(1, product.X, 12);
        Assert.Equal(0, product.Y, 12);
    }

    [Fact]
    public void PowerZeroIsOne()
    {
        var one = new ComplexNumber(2, 3).Power(0);
        Assert.Equal((1.0, 0.0), (one.X, one.Y));
    }
}
