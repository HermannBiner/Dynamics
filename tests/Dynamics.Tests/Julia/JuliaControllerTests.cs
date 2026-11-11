using System.Globalization;
using Dynamics.Core.General;
using Dynamics.Core.Julia;
using static Dynamics.Tests.ReferenceData;

namespace Dynamics.Tests.Julia;

public class JuliaControllerTests
{
    public JuliaControllerTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
    }

    [Theory]
    [InlineData(600)]
    [InlineData(563)]
    [InlineData(64)]
    [InlineData(1)]
    public void SpiralCoversEveryPixelOnce(int size)
    {
        var bitmap = SpiralScan.Pixels(size, size).Select(x => SpiralScan.BitmapPosition(x.P, x.Q)).ToList();

        Assert.Equal(size * size, bitmap.Count);
        Assert.Equal(size * size, bitmap.Distinct().Count());
        Assert.All(bitmap, x => Assert.True(x.X >= 0 && x.X < size && x.Y >= 0 && x.Y < size));
    }

    /// <summary>The spiral of ClsJuliaController.PerformIteration / IterationLoop, with the VB stop rule.</summary>
    private static List<(int P, int Q)> VBSpiral(int size)
    {
        var p = Convert.ToInt32(size / 2.0);
        var q = Convert.ToInt32(size / 2.0);
        var result = new List<(int, int)> { (p, q) };
        int examinatedPoints = 0, l = 0;
        do
        {
            examinatedPoints++;
            var sig = examinatedPoints % 2 == 0 ? -1 : 1;
            l++;
            for (var k = 1; k < l; k++)
            {
                p += sig;
                result.Add((p, q));
            }
            for (var k = 1; k < l; k++)
            {
                q += sig;
                result.Add((p, q));
            }
        } while (!(p >= size || q >= size));

        return result;
    }

    [Theory]
    [InlineData(600, true)]
    [InlineData(64, true)]
    [InlineData(563, false)]
    [InlineData(31, false)]
    public void SpiralIsTheVBSpiral(int size, bool isComplete)
    {
        // Same order as VB, without the pixels outside the bitmap
        var vb = VBSpiral(size).Where(x =>
        {
            var (bx, by) = SpiralScan.BitmapPosition(x.P, x.Q);
            return bx >= 0 && bx < size && by >= 0 && by < size;
        }).ToList();
        var spiral = SpiralScan.Pixels(size, size).ToList();

        Assert.Equal(vb, spiral.Take(vb.Count));

        // For an odd size VB left the top row (q = 0) white; the corrected spiral continues
        Assert.Equal(isComplete, vb.Count == size * size);
        Assert.Equal(isComplete ? 0 : size, spiral.Count - vb.Count);
    }

    [Fact]
    public void SpiralStartsInTheMidpointAsInVB()
    {
        // VB: CInt(Width / 2), then right/down, left/up with growing branches
        var start = SpiralScan.Pixels(600, 600).Take(7).ToList();
        Assert.Equal([(300, 300), (299, 300), (299, 299), (300, 299), (301, 299), (301, 300), (301, 301)], start);

        // 563 / 2 = 281.5 is rounded to even
        Assert.Equal((282, 282), SpiralScan.Pixels(563, 563).First());
    }

    [Fact]
    public void ControllerFindsJuliaAndMandelbrot()
    {
        var controller = new JuliaController(100);
        Assert.Equal([typeof(JuliaPN), typeof(Mandelbrot)], controller.JuliaTypes);
        Assert.All(controller.JuliaNames, name => Assert.DoesNotContain("Cls", name));
    }

    [Fact]
    public void JuliaStartsWithTheSeahorse()
    {
        var controller = new JuliaController(100);
        Assert.Equal("0.32", controller.A);
        Assert.Equal("0.043", controller.B);
        Assert.Equal("-1.5", controller.XMin);
        Assert.Equal("1.5", controller.XMax);
        Assert.Equal("Delta = 3.0", controller.DeltaX);
        Assert.Equal(12, controller.Samples.Count);
    }

    [Fact]
    public void CompletePictureMatchesTheModel()
    {
        const int size = 40;
        var controller = new JuliaController(size);
        Assert.Null(controller.Start());

        var reference = new JuliaPN { DiagramSize = size, N = 2, IsUseSystemColors = true, C = new ComplexNumber(0.32, 0.043) };
        var count = 0;
        while (controller.Step(out var step))
        {
            Assert.Equal(reference.IterationStep(step.P, step.Q), step.Pixel);
            count++;
        }

        Assert.Equal(IterationStatus.Stopped, controller.Status);
        Assert.Equal(count, controller.Steps);
    }

    [Fact]
    public void StopAndContinueResumeTheSpiral()
    {
        var controller = new JuliaController(30);
        controller.Start();
        for (var i = 0; i < 100; i++)
        {
            controller.Step(out _);
        }

        controller.Stop();
        Assert.Equal(IterationStatus.Interrupted, controller.Status);
        Assert.False(controller.Step(out _));
        Assert.False(controller.IsEditable);

        controller.Start();
        Assert.True(controller.Step(out var step));
        Assert.Equal(SpiralScan.Pixels(30, 30).ElementAt(100), (step.P, step.Q));
        Assert.Equal(101, controller.Steps);
    }

    [Fact]
    public void InvalidUserDataIsReported()
    {
        var controller = new JuliaController(30);
        controller.SetXMin("2");
        Assert.NotNull(controller.Start());
        Assert.Equal(IterationStatus.Stopped, controller.Status);

        controller.SetXMin("-1");
        controller.A = "abc";
        Assert.NotNull(controller.Start());

        controller.A = "1.6";
        Assert.NotNull(controller.Start());

        controller.A = "0.3";
        Assert.Null(controller.Start());
        Assert.Equal(-1m, controller.DS.ActualXRange.A);
    }

    [Fact]
    public void MandelbrotShowsTheMidpointOfTheSelectionAsC()
    {
        var controller = new JuliaController(100);
        controller.SelectSystem(1);
        Assert.True(controller.DS.IsMandelbrot);

        // Corrected: a = XMin + DeltaX / 2 (VB: (XMin + DeltaX) / 2 = XMax / 2), b = YMin + DeltaY / 2
        controller.SetXMin("-1");
        controller.SetXMax("0.5");
        controller.SetYMin("0.2");
        controller.SetYMax("0.6");
        Assert.Equal(-0.25m, Dec(controller.A));
        Assert.Equal(0.4m, Dec(controller.B));

        // The stored selection becomes the first Julia sample
        controller.SelectSystem(0);
        Assert.Equal(13, controller.Samples.Count);
        Assert.Equal(-0.25, controller.Samples[0].C.X);
        Assert.Equal("-0.25", controller.A);
    }

    [Fact]
    public void MandelbrotExponentChangesTheRange()
    {
        var controller = new JuliaController(100);
        controller.SelectSystem(1);
        Assert.Equal("-2", controller.XMin);
        Assert.Equal("0.6", controller.XMax);

        controller.SetExponent(3);
        Assert.Equal("-1.5", controller.XMin);
    }

    [Fact]
    public void AreaSelectionUsesThePictureTransformation()
    {
        var controller = new JuliaController(100);

        // Corrected: possible without a complete picture (VB: only after one)
        Assert.True(controller.IsAreaSelectable);
        Assert.True(controller.SelectArea(51, 26, 26, 76));

        // The selected range is exactly the range of the selected pixels: the corners are the
        // mathematical points the pixels were computed for.
        var t = controller.DS.Transform;
        Assert.Equal(t.PixelToMathPoint(26, 26).X, Dec(controller.XMin));
        Assert.Equal(t.PixelToMathPoint(51, 26).X, Dec(controller.XMax));
        Assert.Equal(t.PixelToMathPoint(26, 76).Y, Dec(controller.YMin));
        Assert.Equal(t.PixelToMathPoint(26, 26).Y, Dec(controller.YMax));

        // Zooming into the selection: its corners are drawn at the corners of the new picture
        Assert.Null(controller.Start());
        Assert.Equal(Dec(controller.XMin), controller.DS.Transform.PixelToMathPoint(1, 1).X);
        Assert.Equal(Dec(controller.YMax), controller.DS.Transform.PixelToMathPoint(1, 1).Y);
        Assert.Equal(Dec(controller.XMax), controller.DS.Transform.PixelToMathPoint(100, 100).X);
        Assert.Equal(Dec(controller.YMin), controller.DS.Transform.PixelToMathPoint(100, 100).Y);

        // Not while running or interrupted
        controller.Step(out _);
        Assert.False(controller.SelectArea(10, 10, 50, 50));
        controller.Stop();
        Assert.False(controller.SelectArea(10, 10, 50, 50));

        // Again after reset (the selection refers to the shown, zoomed picture)
        controller.DefaultUserData();
        Assert.True(controller.SelectArea(1, 1, 100, 100));
        Assert.Equal(controller.DS.ActualXRange.A, Dec(controller.XMin));
        Assert.Equal(controller.DS.ActualYRange.B, Dec(controller.YMax));

        // A point is no selection; the selection is cut to the allowed range
        controller = new JuliaController(100);
        Assert.False(controller.SelectArea(5, 5, 5, 5));
        Assert.True(controller.SelectArea(0, 0, 101, 101));
        Assert.Equal("-1.5", controller.XMin);
        Assert.Equal("1.5", controller.XMax);
        Assert.Equal("1.5", controller.YMax);
        Assert.Equal("-1.5", controller.YMin);
    }

    [Fact]
    public void ProtocolIsCompleteForEveryPicture()
    {
        var controller = new JuliaController(40) { IsProtocol = true };
        List<string> Run()
        {
            Assert.Null(controller.Start());
            while (controller.Step(out _))
            {
            }

            return [.. controller.Protocol];
        }

        var first = Run();
        Assert.NotEmpty(first);

        // Corrected: VB kept the bounds, so the second protocol was empty
        controller.ResetIteration();
        Assert.Equal(first, Run());
    }

    [Fact]
    public void UserColorsAreUsedAfterSwitching()
    {
        var controller = new JuliaController(20);
        controller.SetColorMode(false);
        controller.SetUserColors(10, 0, 0);
        Assert.Equal(Argb.FromRgb(255, 0, 0), controller.PreviewColor(255));
        Assert.Equal(Argb.FromRgb(150, 0, 0), controller.PreviewColor(150));

        controller.Start();
        Assert.True(controller.Step(out _));
        var colors = new List<int>();
        while (controller.Step(out var step))
        {
            colors.Add(step.Pixel.Color);
        }

        Assert.All(colors, c => Assert.True(Argb.Green(c) == 0 && Argb.Blue(c) == 0));
    }

    [Fact]
    public void MandelbrotMapDrawsMandelbrotThenJulia()
    {
        var controller = new MandelbrotMapController(30, 20);
        Assert.Equal("0.32", controller.A);
        Assert.False(controller.MouseDown(10, 10));

        var mandelbrot = 0;
        var julia = 0;
        var wasJulia = false;
        while (controller.Step(out var isMandelbrot, out _))
        {
            Assert.False(isMandelbrot && wasJulia);
            wasJulia = !isMandelbrot;
            if (isMandelbrot)
            {
                mandelbrot++;
            }
            else
            {
                julia++;
            }
        }

        Assert.Equal(SpiralScan.Pixels(30, 30).Count(), mandelbrot);
        Assert.Equal(SpiralScan.Pixels(20, 20).Count(), julia);
        Assert.False(controller.IsRunning);
    }

    [Fact]
    public void MandelbrotMapMouseChoosesC()
    {
        var controller = new MandelbrotMapController(600, 563);
        while (controller.Step(out var isMandelbrot, out _) && isMandelbrot)
        {
        }

        Assert.True(controller.MouseDown(1, 1));
        Assert.Equal(-2m, Dec(controller.A));
        Assert.Equal(1.3m, Dec(controller.B));

        Assert.True(controller.MouseMove(300, 300));
        var expected = controller.DSMandelbrot.Transform.PixelToMathPoint(300, 300);
        Assert.Equal(expected.X, Dec(controller.A));

        // The Julia set is generated for the last position
        Assert.True(controller.MouseUp(out var error));
        Assert.Null(error);
        Assert.Equal((double)expected.X, controller.JuliaC.X);
        Assert.False(controller.IsJuliaComplete);
        Assert.False(controller.MouseUp(out _));

        // c outside [-1.9, 1.9] is not allowed
        controller.MouseDown(1, 300);
        Assert.True(controller.MouseUp(out error));
        Assert.NotNull(error);
    }
}
