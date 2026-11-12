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
        Assert.Equal((0.0, 0.0, 1.0), (e3.X, e3.Y, e3.Z));

        var e1Again = e2.Cross(e3);
        Assert.Equal((1.0, 0.0, 0.0), (e1Again.X, e1Again.Y, e1Again.Z));
    }

    [Fact]
    public void CrossProductIsOrthogonalAndAnticommutative()
    {
        var v = new Vector(1.5, -2.0, 0.25);
        var w = new Vector(-0.5, 3.0, 4.0);
        var c = v.Cross(w);
        var d = w.Cross(v);

        Assert.Equal(0.0, c.Scalar(v));
        Assert.Equal(0.0, c.Scalar(w));
        Assert.Equal((-c.X, -c.Y, -c.Z), (d.X, d.Y, d.Z));
    }

    [Fact]
    public void TurnInXYPlaneRotates()
    {
        var v = new Vector(1, 0, 7);
        v.TurnInXYPlane(Math.PI / 2);
        Assert.Equal(0, v.X, 12);
        Assert.Equal(1, v.Y, 12);
        Assert.Equal(7.0, v.Z);

        // The length in the plane is preserved.
        var w = new Vector(0.6, 0.8, 0);
        w.TurnInXYPlane(1.234);
        Assert.Equal(1, w.Abs(), 12);
    }

    [Fact]
    public void IntervalCopyKeepsBorders()
    {
        var copy = new Interval(new Interval(-1.5, 2.5));
        Assert.Equal((-1.5, 2.5), (copy.A, copy.B));
        Assert.Equal(4.0, copy.IntervalWidth);
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
