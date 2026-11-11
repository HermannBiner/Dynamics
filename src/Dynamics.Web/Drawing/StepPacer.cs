using System.Diagnostics;

namespace Dynamics.Web.Drawing;

/// <summary>
/// Paces an iteration loop: returns how many steps are due since the last frame for a given
/// rate in steps per second, independent of the frame rate of the browser.
/// </summary>
public sealed class StepPacer
{
    /// <summary>Longer pauses (tab in background, slow frame) do not cause a burst of steps.</summary>
    private const double MaxFrameSeconds = 0.1;

    private readonly Stopwatch _watch = Stopwatch.StartNew();
    private double _last;
    private double _carry;

    /// <summary>Call when the iteration (re)starts.</summary>
    public void Restart()
    {
        _last = _watch.Elapsed.TotalSeconds;
        _carry = 0;
    }

    /// <summary>Number of steps due now; int.MaxValue for an unlimited rate.</summary>
    public int NextSteps(double stepsPerSecond)
    {
        var now = _watch.Elapsed.TotalSeconds;
        var elapsed = Math.Min(now - _last, MaxFrameSeconds);
        _last = now;

        if (double.IsPositiveInfinity(stepsPerSecond))
        {
            return int.MaxValue;
        }

        _carry += stepsPerSecond * elapsed;
        var steps = (int)_carry;
        _carry -= steps;
        return steps;
    }
}
