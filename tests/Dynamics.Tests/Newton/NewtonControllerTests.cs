using System.Globalization;
using Dynamics.Core.General;
using Dynamics.Core.Newton;
using static Dynamics.Tests.ReferenceData;

namespace Dynamics.Tests.Newton;

public class NewtonControllerTests
{
    public NewtonControllerTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
    }

    [Fact]
    public void ControllerStartsWithTheUnitRootsAsInVB()
    {
        var controller = new NewtonController(100);
        Assert.Equal([typeof(InversUnitRoots), typeof(Polynom3C), typeof(UnitRootCollection)], controller.NewtonTypes);
        Assert.Equal(2, controller.SelectedIndex);
        Assert.Equal(3, controller.N);
        Assert.Equal(3, controller.DS.Roots.Count);
        Assert.True(controller.DS.IsShowBasin);
        Assert.Equal(NewtonColor.Shadowed, controller.Color);
        Assert.All(controller.NewtonNames, name => Assert.DoesNotContain("Cls", name));
    }

    [Fact]
    public void CompletePictureMatchesTheModel()
    {
        const int size = 40;
        var controller = new NewtonController(size);
        controller.SetExponent(4);
        Assert.Null(controller.Start());

        var reference = new UnitRootCollection { DiagramSize = size, N = 4, UseColor = NewtonColor.Shadowed };
        reference.ResetIteration();
        var count = 0;
        while (controller.Step(out var step))
        {
            Assert.Equal(reference.IterationStep(step.P, step.Q), step.Pixel);
            count++;
        }

        Assert.Equal(size * size, count);
        Assert.Equal(IterationStatus.Stopped, controller.Status);
        Assert.True(controller.IsFinished);
    }

    [Fact]
    public void Polynom3CUsesTheRootsOfTheActualC()
    {
        var controller = new NewtonController(20);
        controller.SelectSystem(1);
        Assert.Equal("0.5", controller.A);

        // Corrected: VB prepared the roots before c was set, so the first picture knew only -1 and 1
        Assert.Null(controller.Start());
        Assert.Equal(3, controller.DS.Roots.Count);
        Assert.Equal((0.5, 1.0), (controller.DS.Roots[2].X, controller.DS.Roots[2].Y));
        while (controller.Step(out _))
        {
        }

        // A new c is used at the next start
        controller.SetA("-0.3");
        controller.SetB("0.2");
        Assert.Null(controller.Start());
        Assert.Equal((-0.3, 0.2), (controller.DS.Roots[2].X, controller.DS.Roots[2].Y));
    }

    [Fact]
    public void Polynom3CFirstPictureColorsTheBasinOfC()
    {
        const int size = 30;
        var controller = new NewtonController(size);
        controller.SelectSystem(1);
        Assert.Null(controller.Start());

        var reference = new Polynom3C { DiagramSize = size, N = 3, C = new ComplexNumber(0.5, 1), UseColor = NewtonColor.Shadowed };
        reference.ResetIteration();
        while (controller.Step(out var step))
        {
            Assert.Equal(reference.IterationStep(step.P, step.Q), step.Pixel);
        }
    }

    [Fact]
    public void ChangingCKeepsTheValueAndTheRanges()
    {
        var controller = new NewtonController(20);
        controller.SelectSystem(1);
        controller.SetXMin("-1");

        // Corrected: VB TxtA_TextChanged called SetDefaultUserData, which set a = 0.5 and the default ranges
        controller.SetA("0.7");
        controller.SetB("-0.4");
        Assert.Equal("0.7", controller.A);
        Assert.Equal("-0.4", controller.B);
        Assert.Equal("-1", controller.XMin);
    }

    [Fact]
    public void MixingSelectsTheColor()
    {
        var controller = new NewtonController(20);
        controller.SetMixing(NewtonMixing.Rotate);
        Assert.Equal(NewtonColor.Bright, controller.Color);
        controller.SetMixing(NewtonMixing.None);
        Assert.Equal(NewtonColor.Shadowed, controller.Color);
    }

    [Fact]
    public void StopDrawsTheRootsBlackAndContinueResumes()
    {
        var controller = new NewtonController(30);
        controller.Start();
        for (var i = 0; i < 50; i++)
        {
            controller.Step(out _);
        }

        controller.Stop();
        Assert.True(controller.IsFinished);
        Assert.False(controller.IsAreaSelectable);

        controller.Start();
        Assert.False(controller.IsFinished);
        Assert.True(controller.Step(out _));
        Assert.Equal(51, controller.Steps);
    }

    [Fact]
    public void ProtocolHasALinePerPixel()
    {
        var controller = new NewtonController(10) { IsProtocol = true };
        controller.Start();
        while (controller.Step(out _))
        {
        }

        Assert.Equal(100, controller.Protocol.Count);
        Assert.Equal(4, controller.Protocol[0].Split(", ").Length);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    public void BasinCurveIsWhereTheDerivativeHasAbsoluteValueOne(int n)
    {
        // N(z) = ((n-1) z + z^(1-n)) / n, N'(z) = (n-1)/n · (1 - z^(-n))
        foreach (var phi in new[] { 0, 0.1, 0.2, 0.3 })
        {
            var r = NewtonBasin.GetR(n, phi);
            var z = new ComplexNumber(r * Math.Cos(phi), r * Math.Sin(phi));
            var derivative = new ComplexNumber(1, 0).Add(z.Power(-n).Stretch(-1)).Stretch((n - 1.0) / n);
            Assert.Equal(1, derivative.AbsoluteValue, 10);
        }

        var basin = NewtonBasin.Create(n);
        Assert.NotEmpty(basin.CurvePoints);
        Assert.Equal(1 - 1 / Math.Pow(2, 1.0 / n), basin.InnerRadius);
    }

    [Fact]
    public void SingleNewtonConvergesToTheRoots()
    {
        var controller = new SingleNewtonController(600);
        Assert.Equal(3, controller.UnitRoots.Count);

        // 0.5 + i converges to a root (the basins are fractal)
        while (controller.CanStep && controller.Status != IterationStatus.Interrupted)
        {
            Assert.Null(controller.NextStep());
        }

        Assert.NotNull(controller.ConvergedRoot);
        var root = controller.UnitRoots[controller.ConvergedRoot.Value];
        Assert.True(controller.Orbit[^1].Add(root.Stretch(-1)).AbsoluteValue < 0.01);
        Assert.True(controller.Orbit.Count > 2);

        // Near a root it converges to that root
        controller.ResetIteration();
        controller.A = "-0.45";
        controller.B = "0.8";
        while (controller.Status != IterationStatus.Interrupted)
        {
            controller.NextStep();
        }

        Assert.Equal(1, controller.ConvergedRoot);

        // Nothing happens until Reset
        var count = controller.Orbit.Count;
        controller.NextStep();
        Assert.Equal(count, controller.Orbit.Count);

        // n = 2: the left half plane converges to -1
        controller.LoadRoots(2);
        Assert.Empty(controller.Orbit);
        controller.MouseDown(new MathPoint(-0.3, 0.9));
        Assert.True(controller.MouseMove(new MathPoint(-0.3, 0.9)));
        Assert.Equal(1, controller.ConvergedRoot);
        Assert.Equal("-0.3000000000", controller.A);
        controller.MouseUp();
        Assert.False(controller.MouseMove(new MathPoint(0.3, 0.9)));
    }

    [Fact]
    public void SingleNewtonStopsAfterMaxSteps()
    {
        // On the y-axis (n = 2) the iteration never converges
        var controller = new SingleNewtonController(600);
        controller.LoadRoots(2);
        controller.A = "0";
        controller.B = "0.7";
        while (controller.Status != IterationStatus.Interrupted)
        {
            controller.NextStep();
        }

        Assert.Null(controller.ConvergedRoot);
        Assert.Equal(SingleNewtonController.MaxSteps + 2, controller.Orbit.Count);
    }
}
