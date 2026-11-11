using System.Globalization;
using Dynamics.Core.General;

namespace Dynamics.Tests.General;

public class GeneralTests
{
    [Fact]
    public void TextIsLocalized()
    {
        Assert.Equal("Die linke Intervallgrenze muss kleiner sein als die rechte",
            Text.Get("LeftSmallerRight", new CultureInfo("de-CH")));
        Assert.NotEqual(Text.Get("LeftSmallerRight", new CultureInfo("de")),
            Text.Get("LeftSmallerRight", CultureInfo.InvariantCulture));
    }

    [Fact]
    public void TextFallsBackToKey() => Assert.Equal("NoSuchKey", Text.Get("NoSuchKey"));

    [Fact]
    public void ClassNameWithoutResourceCutsPrefix() => Assert.Equal("MyOwnSystem", Text.GetClassName("ClsMyOwnSystem"));

    [Fact]
    public void IntervalRejectsWrongOrder()
    {
        Assert.Throws<ArgumentException>(() => new Interval(2m, 1m));
        Assert.Throws<ArgumentException>(() => new Interval(1.0, 1.0));
    }

    [Fact]
    public void IntervalContains()
    {
        var interval = new Interval(-1m, 2m);
        Assert.True(interval.IsNumberInInterval(2));
        Assert.True(interval.IsNumberInInterval(-0.5m));
        Assert.False(interval.IsNumberInInterval(2.0001));
        Assert.True(interval.IsInterval2PartOfInterval(new Interval(0m, 1m)));
        Assert.Equal(3m, interval.IntervalWidth);
    }

    [Fact]
    public void NTupleHasNPlusOneComponents()
    {
        var tuple = new NTuple(1);
        tuple[0] = 1;
        tuple[1] = 2;
        Assert.Equal(1, tuple.Dimension);
        Assert.Throws<IndexOutOfRangeException>(() => tuple[2] = 3);
    }

    [Fact]
    public void ComplexDivisionByZeroThrows() =>
        Assert.Throws<DivideByZeroException>(() => new ComplexNumber(1, 1).Div(new ComplexNumber(0, 0)));

    [Fact]
    public void DualNumberFormat()
    {
        Assert.True(MathHelperUnimodal.CheckFormatDualNumber("0101"));
        Assert.False(MathHelperUnimodal.CheckFormatDualNumber(""));
        Assert.False(MathHelperUnimodal.CheckFormatDualNumber("012"));
        Assert.Equal("10100", MathHelperUnimodal.FixNumberOfDigitsInDualNumber("101", 5));
        Assert.Equal("1010", MathHelperUnimodal.ComplementDualString("0101"));
    }

    [Fact]
    public void CopyPropertiesCopiesMatchingProperties()
    {
        var source = new MathPoint(1, 2, 3);
        var target = new MathPoint();
        ObjectCopy.CopyProperties(source, target);
        Assert.Equal((1m, 2m, 3m), (target.X, target.Y, target.Z));
    }
}
