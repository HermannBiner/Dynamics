using Dynamics.Core.General;

namespace Dynamics.Core.Julia;

/// <summary>Julia set of the iteration z → zⁿ + c. Port of ClsJuliaPN.</summary>
public class JuliaPN : JuliaBase
{
    public JuliaPN()
    {
        MyXValueParameter = new GeneralParameter(1, "x-Values", new Interval(-1.5, 1.5), GeneralParameter.ParameterType.Variable, 0.32);
        MyYValueParameter = new GeneralParameter(1, "y-Values", new Interval(-1.5, 1.5), GeneralParameter.ParameterType.Variable, 0.043);
        SetActualRanges(MyXValueParameter.Range, MyYValueParameter.Range);
    }

    public override int N { set => MyN = value; }

    public override bool IsMandelbrot => false;

    public override JuliaPixel IterationStep(int pixelX, int pixelY)
    {
        var (x, y) = PixelToComplex(pixelX, pixelY);

        // VB: Radius is a Decimal, the comparison with the Double |z| is in Double.
        var radius = Math.Max(MyC.AbsoluteValue, MinimalRadius);

        var steps = Iterate(x, y, MyC.X, MyC.Y, radius);

        if (steps > MaxSteps)
        {
            return new JuliaPixel(steps, Argb.Black, null);
        }

        // if steps = 0, the color would be black as well
        steps = Math.Max(1, steps);
        return new JuliaPixel(steps, GetColor(steps), Protocol(steps));
    }
}
