using System.Globalization;
using Dynamics.Core.General;

namespace Dynamics.Core.NumericMethods;

/// <summary>Result of one iteration step: the trace segments of both pendulums.</summary>
public readonly record struct NumericMethodStep(
    MathPoint TraceA, MathPoint NextTraceA, MathPoint TraceB, MathPoint NextTraceB, bool IsShift, string ValueLine);

/// <summary>
/// UI-independent part of ClsNumericMethodController. Pendulum A is the real spring pendulum (cos t),
/// pendulum B is computed by the selected numeric method. The UI draws a chart recorder:
/// the traces are drawn at the left edge and the picture is shifted right by one pixel
/// every <see cref="NumberOfStepsUntilShift"/> steps.
/// </summary>
public class NumericMethodController
{
    /// <summary>Fixed x-positions of the two pendulums.</summary>
    public const decimal XPositionA = -0.97m;
    public const decimal XPositionB = -0.94m;

    /// <summary>VB: the start position can be set with the mouse left of pixel 100 (of 600).</summary>
    public static readonly decimal StartAreaMaxX = -1m + 100m * 2m / 600m;

    public const int StepWidthMin = 1;
    public const int StepWidthMax = 15;
    public const int DefaultStepWidth = 5;

    private const decimal DefaultAmplitude = 0.5m;
    private const decimal MaxAmplitude = 0.95m;

    private MathPoint _traceA = new();
    private MathPoint _traceB = new();
    private MathPoint _nextTraceA = new();
    private MathPoint _nextTraceB = new();

    public NumericMethodController()
    {
        // VB found the methods by reflection; the real spring pendulum is one of them.
        MethodTypes = typeof(INumericMethod).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(INumericMethod).IsAssignableFrom(t))
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ToList();
        SelectMethod(0);
    }

    public IReadOnlyList<Type> MethodTypes { get; }

    /// <summary>Pendulum A: the real spring pendulum.</summary>
    public INumericMethod DSA { get; private set; } = null!;

    /// <summary>Pendulum B: computed by the selected numeric method.</summary>
    public INumericMethod DSB { get; private set; } = null!;

    public IterationStatus Status { get; private set; }

    /// <summary>Number of steps.</summary>
    public int N { get; private set; }

    /// <summary>Track-bar value 1..15 (VB: TrbStepWidth).</summary>
    public int StepWidthTrackBar { get; private set; } = DefaultStepWidth;

    /// <summary>Stretched mode: both pendulums use the small step width.</summary>
    public bool IsStretched { get; private set; }

    public decimal StepWidthA { get; private set; } = 0.1m;
    public decimal StepWidthB { get; private set; } = 0.1m;
    public int NumberOfApproxStepsB { get; private set; } = 1;
    public int NumberOfStepsUntilShift { get; private set; } = 1;

    /// <summary>Step width shown in the label (VB: LocStepWidth).</summary>
    public decimal DisplayedStepWidth { get; private set; }

    public bool IsMouseDown { get; private set; }

    public IEnumerable<string> MethodNames => MethodTypes.Select(t => Text.GetClassName(Create(t).ClassName));

    public int SelectedIndex { get; private set; }

    private static INumericMethod Create(Type type) => (INumericMethod)Activator.CreateInstance(type)!;

    /// <summary>VB: SetDS + InitializeMe + SetDefaultUserData + ResetIteration.</summary>
    public void SelectMethod(int index)
    {
        SelectedIndex = index;
        DSB = Create(MethodTypes[index]);
        DSA = new RealSpringPendulum();

        // VB InitializeMe unchecks "stretched", which resets the iteration and sets the step widths.
        IsStretched = false;
        SetStepWidthAB();

        SetDefaultUserData();
        ResetIteration();
    }

    public void SetDefaultUserData()
    {
        foreach (var ds in new[] { DSA, DSB })
        {
            ds.Amplitude = DefaultAmplitude;
            ds[0] = 0;
            ds[2] = 0;
        }
    }

    public void ResetIteration()
    {
        Status = IterationStatus.Stopped;
        N = 0;
        IsMouseDown = false;
    }

    public void SetStretched(bool isStretched)
    {
        IsStretched = isStretched;
        ResetIteration();
        SetStepWidthAB();
    }

    public void SetStepWidth(int trackBarValue)
    {
        StepWidthTrackBar = trackBarValue;
        SetStepWidthAB();
    }

    /// <summary>
    /// VB: SetStepWidthAB. Not stretched: A uses 0.1 and B repeats its smaller step so often
    /// that NumberOfApproxStepsB · StepWidthB = StepWidthA (both stay synchronized).
    /// Stretched: both use the small step width.
    /// </summary>
    public void SetStepWidthAB()
    {
        var stretchFactor = StepWidthTrackBar;
        var locStepWidth = stretchFactor > 5
            ? (decimal)Math.Max(0.01 - 0.001 * (stretchFactor - 6), 0.001)
            : (decimal)(0.1 - 0.02 * (stretchFactor - 1));

        if (IsStretched)
        {
            StepWidthA = locStepWidth;
            StepWidthB = StepWidthA;
            NumberOfApproxStepsB = 1;
        }
        else
        {
            StepWidthA = 0.1m;
            NumberOfApproxStepsB = Convert.ToInt32(Math.Round(StepWidthA / locStepWidth));
            StepWidthB = StepWidthA / NumberOfApproxStepsB;
        }

        DSA.h = StepWidthA;
        DSA.NumberOfApproxSteps = 1;
        DSB.h = StepWidthB;
        DSB.NumberOfApproxSteps = NumberOfApproxStepsB;
        DisplayedStepWidth = locStepWidth;
    }

    /// <summary>VB: StartIteration. Returns true if the iteration may run now.</summary>
    public bool Start()
    {
        if (Status == IterationStatus.Stopped)
        {
            N = 0;
            _traceA = new MathPoint(XPositionA, DSA[1]);
            _traceB = new MathPoint(XPositionB, DSB[1]);
            _nextTraceA = new MathPoint(XPositionA, DSA[1]);
            _nextTraceB = new MathPoint(XPositionB, DSB[1]);

            if (IsStretched)
            {
                DSB.h = DSA.h;
                DSB.NumberOfApproxSteps = 1;
            }
            else
            {
                DSA.h = StepWidthA;
                SetStepWidthAB();
            }

            // One pixel corresponds to 0.02 = NumberOfStepsUntilShift · StepWidthA (VB CInt rounds to even).
            NumberOfStepsUntilShift = Math.Max(1, Convert.ToInt32(0.02 / (double)StepWidthA));

            Status = IterationStatus.Ready;
        }

        if (Status is IterationStatus.Ready or IterationStatus.Interrupted)
        {
            Status = IterationStatus.Running;
            return true;
        }

        return false;
    }

    /// <summary>One iteration step of both pendulums.</summary>
    public NumericMethodStep Step()
    {
        N++;

        _traceA = new MathPoint(_nextTraceA.X, _nextTraceA.Y);
        _traceB = new MathPoint(_nextTraceB.X, _nextTraceB.Y);

        DSA.Iteration();
        DSB.Iteration();

        _nextTraceA = new MathPoint(XPositionA, DSA[1]);
        _nextTraceB = new MathPoint(XPositionB, DSB[1]);

        // VB: difference of the previous positions.
        var line = N.ToString("00000", CultureInfo.CurrentCulture) + ": " +
                   (_traceA.Y - _traceB.Y).ToString("0.##########", CultureInfo.CurrentCulture);

        return new NumericMethodStep(_traceA, _nextTraceA, _traceB, _nextTraceB, N % NumberOfStepsUntilShift == 0, line);
    }

    public void Stop()
    {
        if (Status == IterationStatus.Running)
        {
            Status = IterationStatus.Interrupted;
        }
    }

    /// <summary>Current positions of pendulum A and B.</summary>
    public (MathPoint A, MathPoint B) Positions => (new MathPoint(XPositionA, DSA[1]), new MathPoint(XPositionB, DSB[1]));

    /// <summary>VB: MouseDown; only when stopped and left of the start area limit.</summary>
    public bool MouseDown(MathPoint position)
    {
        if (Status == IterationStatus.Stopped && position.X < StartAreaMaxX)
        {
            IsMouseDown = true;
            MouseMove(position);
        }

        return IsMouseDown;
    }

    /// <summary>VB: MouseMove; leaving the start area ends the drag. Returns true if the amplitude changed.</summary>
    public bool MouseMove(MathPoint position)
    {
        if (!IsMouseDown)
        {
            return false;
        }

        if (position.X >= StartAreaMaxX)
        {
            IsMouseDown = false;
            return false;
        }

        var amplitude = Math.Min(Math.Max(position.Y, -MaxAmplitude), MaxAmplitude);
        DSA.Amplitude = amplitude;
        DSB.Amplitude = amplitude;
        return true;
    }

    /// <summary>VB: MouseUp; the pendulums start from the new position with t = 0 and y' = 0.</summary>
    public void MouseUp()
    {
        if (!IsMouseDown)
        {
            return;
        }

        foreach (var ds in new[] { DSA, DSB })
        {
            ds[0] = 0;
            ds[2] = 0;
        }

        IsMouseDown = false;
    }
}
