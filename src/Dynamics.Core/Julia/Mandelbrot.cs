using Dynamics.Core.General;

namespace Dynamics.Core.Julia;

/// <summary>
/// Mandelbrot set of the iteration z → zⁿ + c: the pixel is the parameter c, the iteration
/// starts with z = 0. Port of ClsMandelbrot.
/// </summary>
public class Mandelbrot : JuliaBase
{
    public Mandelbrot() => SetParameterRange();

    public override int N
    {
        set
        {
            if (value != MyN)
            {
                MyN = value;
                SetParameterRange();
            }
        }
    }

    public override bool IsMandelbrot => true;

    private void SetParameterRange()
    {
        if (MyN == 2)
        {
            // Size of the Mandelbrot set in case n = 2
            MyXValueParameter = new GeneralParameter(1, "x-Values", new Interval(-2.0, 0.6), GeneralParameter.ParameterType.Variable);
            MyYValueParameter = new GeneralParameter(1, "y-Values", new Interval(-1.3, 1.3), GeneralParameter.ParameterType.Variable);
        }
        else
        {
            // centered
            MyXValueParameter = new GeneralParameter(1, "x-Values", new Interval(-1.5, 1.5), GeneralParameter.ParameterType.Variable);
            MyYValueParameter = new GeneralParameter(1, "y-Values", new Interval(-1.5, 1.5), GeneralParameter.ParameterType.Variable);
        }

        SetActualRanges(MyXValueParameter.Range, MyYValueParameter.Range);
    }

    public override JuliaPixel IterationStep(int pixelX, int pixelY)
    {
        // For the Mandelbrot set, c is the pixel and the iteration starts always with z = 0.
        var (cx, cy) = PixelToComplex(pixelX, pixelY);
        MyC.X = cx;
        MyC.Y = cy;

        var radius = MinimalRadius;
        var steps = Iterate(0, 0, cx, cy, radius);

        int color;
        if (steps > MaxSteps)
        {
            color = Argb.Black;
        }
        else
        {
            // if steps = 0, the color would be black as well
            steps = Math.Max(1, steps);
            color = GetColor(steps);
        }

        // VB-compatible: unlike the Julia set, the protocol also counts the points of the set (MaxSteps + 1).
        return new JuliaPixel(steps, color, Protocol(steps));
    }
}
