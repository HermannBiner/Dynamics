using System.Globalization;
using Dynamics.Core.General;
using static Dynamics.Tests.ReferenceData;

namespace Dynamics.Tests.General;

/// <summary>Compares the general classes with the VB reference results (Double against Decimal: with tolerance).</summary>
public class GeneralReferenceTests
{
    private static string[][] Rows(string name) => ReferenceData.Rows("general.json", name);

    [Fact]
    public void CalculateAngleOfDirection()
    {
        foreach (var r in Rows("CalculateAngleOfDirection"))
        {
            Close(r[2], MathHelperAngles.CalculateAngleOfDirection(Dec(r[0]), Dec(r[1])));
        }
    }

    [Fact]
    public void PendulumGetAngle()
    {
        foreach (var r in Rows("PendulumGetAngle"))
        {
            Close(r[2], MathHelperPendulum.GetAngle(Dec(r[0]), Dec(r[1])));
        }
    }

    [Fact]
    public void ComplexGetArgument()
    {
        foreach (var r in Rows("ComplexGetArgument"))
        {
            Close(r[2], MathHelperComplex.GetArgument(Dbl(r[0]), Dbl(r[1])));
        }
    }

    [Fact]
    public void AngleInNullTwoPi()
    {
        foreach (var r in Rows("AngleInNullTwoPi"))
        {
            Close(r[1], MathHelperAngles.AngleInNullTwoPi(Dec(r[0])));
        }
    }

    [Fact]
    public void AngleInMinusPiAndPi()
    {
        foreach (var r in Rows("AngleInMinusPiAndPi"))
        {
            Close(r[1], MathHelperPendulum.AngleInMinusPiAndPi(Dec(r[0])));
        }
    }

    [Fact]
    public void VectorOperations()
    {
        foreach (var r in Rows("Vector"))
        {
            var v = new Vector(Dec(r[0]), Dec(r[1]), Dec(r[2]));
            var w = new Vector(Dec(r[3]), Dec(r[4]), Dec(r[5]));
            var phi = Dec(r[6]);

            Close(r[7], v.Abs());
            Close(r[8], v.Scalar(w));

            // Cross (columns 9-11) and TurnInXYPlane (12-14) were corrected in Dynamics,
            // so only the unchanged parts are compared with VB.
            Close(r[10], v.Cross(w).Y);
            Close(r[11], v.Cross(w).Z);
            var turned = new Vector(v.X, v.Y, v.Z);
            turned.TurnInXYPlane(phi);
            Close(r[12], turned.X);
            Close(r[14], turned.Z);
        }
    }

    [Fact]
    public void ComplexOperations()
    {
        foreach (var r in Rows("Complex"))
        {
            var z = new ComplexNumber(Dbl(r[0]), Dbl(r[1]));
            var u = new ComplexNumber(Dbl(r[2]), Dbl(r[3]));
            var phi = Dbl(r[4]);

            // Power(-3) (the last pair of the VB row) was corrected in Dynamics and is not compared.
            ComplexNumber[] results =
            [
                z.Mult(u), z.Div(u), z.Invers, z.Power2, z.Rotate(phi),
                z.Power(1), z.Power(2), z.Power(3), z.Power(7), z.Power(-1),
            ];

            var expected = r[5..^4].Select(Dbl).ToArray();
            var actual = results.SelectMany(c => new[] { c.X, c.Y }).ToArray();
            for (var i = 0; i < expected.Length; i++)
            {
                Close(expected[i], actual[i]);
            }

            Close(r[^2], z.AbsoluteValue);
            Close(r[^1], z.Argument);
        }
    }

    [Fact]
    public void DualStringToDecimalNumber()
    {
        foreach (var r in Rows("DualStringToDecimalNumber"))
        {
            Close(r[1], MathHelperUnimodal.DualStringToDecimalNumber(r[0], false));
            Close(r[2], MathHelperUnimodal.DualStringToDecimalNumber(r[0], true));
        }
    }

    [Fact]
    public void ProtocolToTentmapStartValue()
    {
        foreach (var r in Rows("ProtocolToTentmapStartValue"))
        {
            Assert.Equal(r[1], MathHelperUnimodal.ProtocolToTentmapStartValueAsString(r[0]));
        }
    }

    [Fact]
    public void NumberOfOnes()
    {
        foreach (var r in Rows("NumberOfOnes"))
        {
            Assert.Equal(int.Parse(r[1], CultureInfo.InvariantCulture), MathHelperUnimodal.NumberOfOnesInaDualNumber(r[0]));
        }
    }

    [Fact]
    public void DecimalNumberToDualString()
    {
        foreach (var r in Rows("DecimalNumberToDualString"))
        {
            Assert.Equal(r[2], MathHelperUnimodal.DecimalNumberToDualString(Dec(r[0]), int.Parse(r[1], CultureInfo.InvariantCulture)));
        }
    }
}
