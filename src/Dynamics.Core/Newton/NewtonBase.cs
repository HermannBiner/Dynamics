using System.Globalization;
using Dynamics.Core.General;

namespace Dynamics.Core.Newton;

/// <summary>
/// Common logic of all Newton iterations. Port of ClsNewtonAbstract; the operations on complex numbers
/// are the same as in VB (ClsComplexNumber), so the pictures are identical.
/// </summary>
public abstract class NewtonBase : INewton
{
    private int _diagramSize = 2;
    private Interval _actualXRange;
    private Interval _actualYRange;

    /// <summary>The radius where z is regarded as belonging to a root.</summary>
    protected double MyRadius;

    /// <summary>How many steps should be iterated?</summary>
    protected int IterationDeepness;

    protected int MyN;
    protected ComplexNumber? MyC;
    protected NewtonMixing MyMixing;
    protected NewtonColor MyColor;
    protected bool MyIsProtocol;

    /// <summary>List of roots of the polynomial.</summary>
    protected readonly List<UnitRoot> UnitRootCollection = [];

    protected NewtonBase()
    {
        XValueParameter = new GeneralParameter(1, "x-Values", new Interval(-1.5, 1.5), GeneralParameter.ParameterType.Variable);
        YValueParameter = new GeneralParameter(1, "y-Values", new Interval(-1.5, 1.5), GeneralParameter.ParameterType.Variable);
        _actualXRange = XValueParameter.Range;
        _actualYRange = YValueParameter.Range;
        UpdateTransform();
    }

    public int DiagramSize
    {
        get => _diagramSize;
        set
        {
            _diagramSize = value;
            UpdateTransform();
        }
    }

    public GeneralParameter XValueParameter { get; }
    public GeneralParameter YValueParameter { get; }

    public Interval ActualXRange
    {
        get => _actualXRange;
        set
        {
            _actualXRange = value;
            UpdateTransform();
        }
    }

    public Interval ActualYRange
    {
        get => _actualYRange;
        set
        {
            _actualYRange = value;
            UpdateTransform();
        }
    }

    public CoordinateTransform Transform { get; private set; } = null!;

    public bool IsProtocol { set => MyIsProtocol = value; }

    public bool IsUseN { get; protected init; }

    public int N
    {
        get => MyN;
        set
        {
            MyN = value;
            MyRadius = Math.Min(0.1, 1 - Math.Pow(2, -1.0 / MyN));
        }
    }

    public bool IsUseC { get; protected init; }

    public ComplexNumber C { set => MyC = value; }

    public NewtonMixing UseMixing { set => MyMixing = value; }

    public NewtonColor UseColor { set => MyColor = value; }

    public abstract bool IsShowBasin { get; }

    public IReadOnlyList<UnitRoot> Roots => UnitRootCollection;

    public void ResetIteration() => PrepareUnitRoots();

    private void UpdateTransform() =>
        Transform = new CoordinateTransform(_diagramSize, _diagramSize, _actualXRange, _actualYRange);

    /// <summary>
    /// If the stop condition is fulfilled, the start point converges to a root: find out which root
    /// that is and get its color.
    /// </summary>
    private int GetBasin(ComplexNumber z, int steps)
    {
        var temp = 1000.0;
        var rootColor = Argb.Black;

        double finalBrightness;
        if (MyColor == NewtonColor.Bright)
        {
            finalBrightness = 1;
        }
        else
        {
            // Set after experiments: n = 3 → 30, n = 4 → 50, n = 5 → 80, n = 6 → 120, ...: y = 5x² - 15x + 30
            var colorDeepness = MyN == 2 ? 30 : 5 * MyN * MyN - 15 * MyN + 30;

            finalBrightness = (1 - (double)steps / colorDeepness) * 1.1;

            // but the maximum is 1
            finalBrightness = Math.Min(finalBrightness, 1);
        }

        foreach (var root in UnitRootCollection)
        {
            var difference = z.Add(root.Stretch(-1)).AbsoluteValue;
            if (difference < temp)
            {
                temp = difference;
                rootColor = root.GetColor(finalBrightness);
            }
        }

        return rootColor;
    }

    public NewtonPixel IterationStep(int pixelX, int pixelY)
    {
        // Transform the pixel to a complex number
        var point = Transform.PixelToMathPoint(pixelX, pixelY);
        var mathStartpoint = new ComplexNumber((double)point.X, (double)point.Y);

        var zi = new ComplexNumber(mathStartpoint.X, mathStartpoint.Y);
        int color;

        if (zi.AbsoluteValue > 0)
        {
            var i = 1;

            // VB: "And" evaluates both sides; StopCondition has no side effects.
            while (i <= IterationDeepness && !StopCondition(zi))
            {
                i++;
                zi = Newton(zi);
            }

            // VB: "Or" evaluates both sides
            var notConvergent = i > IterationDeepness;
            var isDenominatorZero = Denominator(zi).AbsoluteValue == 0;
            color = notConvergent || isDenominatorZero
                ? Argb.Black // the point doesn't converge to a root
                : GetBasin(zi, i); // the basin defines the color and i its brightness
        }
        else
        {
            color = Argb.Black;
        }

        // Protocol of the start point and the end point
        string? protocol = null;
        if (MyIsProtocol)
        {
            protocol = mathStartpoint.X.ToString("N5", CultureInfo.CurrentCulture) + ", " +
                       mathStartpoint.Y.ToString("N5", CultureInfo.CurrentCulture) + ", " +
                       zi.X.ToString("N5", CultureInfo.CurrentCulture) + ", " +
                       zi.Y.ToString("N5", CultureInfo.CurrentCulture);
        }

        return new NewtonPixel(color, protocol);
    }

    /// <summary>VB mixing: z is conjugated or rotated by phi before the Newton step.</summary>
    protected ComplexNumber Mix(ComplexNumber z, double phi) => MyMixing switch
    {
        NewtonMixing.Conjugate => z.Conjugate,
        NewtonMixing.Rotate => z.Mult(new ComplexNumber(Math.Cos(phi), Math.Sin(phi))),
        _ => z,
    };

    /// <summary>The n unit roots: 1 and its rotations by 2π(k-1)/n.</summary>
    protected void PrepareNUnitRoots()
    {
        UnitRootCollection.Clear();

        var w = new ComplexNumber(1, 0);
        UnitRootCollection.Add(new UnitRoot(w, 1));

        for (var k = 2; k <= MyN; k++)
        {
            UnitRootCollection.Add(new UnitRoot(w.Rotate((double)(2 * Math.PI * (k - 1) / MyN)), k));
        }

        IterationDeepness = 60 * MyN;
    }

    public abstract bool StopCondition(ComplexNumber z);

    public abstract ComplexNumber Newton(ComplexNumber z);

    protected abstract void PrepareUnitRoots();

    public abstract ComplexNumber Denominator(ComplexNumber z);
}
