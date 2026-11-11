using Dynamics.Core.General;

namespace Dynamics.Core.NumericMethods;

/// <summary>
/// A pendulum together with the numeric method that computes its position.
/// A spring pendulum with forward Euler is a different "pendulum" than one with backward Euler.
/// Port of INumericMethod.
/// </summary>
public interface INumericMethod
{
    /// <summary>Name of the VB class, used as resource key for the display name.</summary>
    string ClassName { get; }

    /// <summary>Step width of one approximation step.</summary>
    decimal h { get; set; }

    /// <summary>Number of approximation steps per <see cref="Iteration"/>.</summary>
    int NumberOfApproxSteps { set; }

    /// <summary>Start position (y); also sets ActualParameter(1).</summary>
    decimal Amplitude { set; }

    /// <summary>[0] = time t (mod 2π), [1] = y, [2] = y'.</summary>
    decimal this[int index] { get; set; }

    void Iteration();
}

/// <summary>Common parameters of all methods. Port of ClsNumericMethodAbstract.</summary>
public abstract class NumericMethodBase : INumericMethod
{
    protected decimal MyH;
    protected int MyNumberOfApproxSteps;
    protected decimal MyAmplitude;
    protected readonly NTuple MyActualParameter = new(2);

    // VB: CDec(2 * Math.PI)
    protected static readonly decimal TwoPi = (decimal)(2 * Math.PI);

    public abstract string ClassName { get; }

    public decimal h
    {
        get => MyH;
        set => MyH = value;
    }

    public int NumberOfApproxSteps
    {
        set => MyNumberOfApproxSteps = value;
    }

    public decimal Amplitude
    {
        set
        {
            MyAmplitude = value;
            MyActualParameter[1] = value;
        }
    }

    public decimal this[int index]
    {
        get => MyActualParameter[index];
        set => MyActualParameter[index] = value;
    }

    public void Iteration()
    {
        for (var i = 1; i <= MyNumberOfApproxSteps; i++)
        {
            MyActualParameter[0] += MyH;
            ApproximationStep();
        }

        // The time t has period 2π.
        MyActualParameter[0] %= TwoPi;
        AfterIteration();
    }

    /// <summary>One approximation step of (y, y') = ([1], [2]).</summary>
    protected abstract void ApproximationStep();

    protected virtual void AfterIteration()
    {
    }
}

/// <summary>The real spring pendulum, y = A·cos(t). Port of ClsRealSpringPendulum.</summary>
public class RealSpringPendulum : NumericMethodBase
{
    public override string ClassName => "ClsRealSpringPendulum";

    protected override void ApproximationStep()
    {
    }

    protected override void AfterIteration() =>
        MyActualParameter[1] = MyAmplitude * (decimal)Math.Cos((double)MyActualParameter[0]);
}

/// <summary>Spring pendulum with the forward (explicit) Euler method. Port of ClsForwardEuler.</summary>
public class ForwardEuler : NumericMethodBase
{
    public override string ClassName => "ClsForwardEuler";

    protected override void ApproximationStep()
    {
        var u = MyActualParameter[1];
        var v = MyActualParameter[2];
        MyActualParameter[1] = u + v * MyH;
        MyActualParameter[2] = v - u * MyH;
    }
}

/// <summary>Spring pendulum with the backward (implicit) Euler method. Port of ClsBackwardEuler.</summary>
public class BackwardEuler : NumericMethodBase
{
    public override string ClassName => "ClsBackwardEuler";

    protected override void ApproximationStep()
    {
        var locH = 1 + MyH * MyH;
        var u = MyActualParameter[1];
        var v = MyActualParameter[2];
        MyActualParameter[1] = (u + v * MyH) / locH;
        MyActualParameter[2] = (v - u * MyH) / locH;
    }
}

/// <summary>Spring pendulum with the implicit midpoint rule. Port of ClsMidpointImplicit.</summary>
public class MidpointImplicit : NumericMethodBase
{
    public override string ClassName => "ClsMidpointImplicit";

    protected override void ApproximationStep()
    {
        var locH = 1 + MyH * MyH / 4;
        var u = MyActualParameter[1];
        var v = MyActualParameter[2];
        var uHalf = (u + v * MyH / 2) / locH;
        var vHalf = (v - u * MyH / 2) / locH;
        MyActualParameter[1] = u + vHalf * MyH;
        MyActualParameter[2] = v - uHalf * MyH;
    }
}

/// <summary>Spring pendulum (y'' = -y) with Runge-Kutta 4. Port of ClsRungeKutta4Pendulum.</summary>
public class RungeKutta4Pendulum : NumericMethodBase
{
    private readonly NTuple k = new(3);
    private readonly NTuple l = new(3);

    public override string ClassName => "ClsRungeKutta4Pendulum";

    protected override void ApproximationStep()
    {
        var u = MyActualParameter[1];
        var v = MyActualParameter[2];

        k[0] = MyH * v;
        l[0] = -MyH * u;

        k[1] = MyH * (v + l[0] / 2);
        l[1] = MyH * (-u - k[0] / 2);

        k[2] = MyH * (v + l[1] / 2);
        l[2] = MyH * (-u - k[1] / 2);

        k[3] = MyH * (v + l[2]);
        l[3] = MyH * (-u - k[2]);

        MyActualParameter[1] = u + (k[0] + 2 * k[1] + 2 * k[2] + k[3]) / 6;
        MyActualParameter[2] = v + (l[0] + 2 * l[1] + 2 * l[2] + l[3]) / 6;
    }
}

/// <summary>Real thread pendulum y'' + sin(y) = 0 with Runge-Kutta 4. Port of ClsRealThreadPendulum.</summary>
public class RealThreadPendulum : NumericMethodBase
{
    private readonly NTuple k = new(3);
    private readonly NTuple l = new(3);

    public override string ClassName => "ClsRealThreadPendulum";

    protected override void ApproximationStep()
    {
        var u = MyActualParameter[1];
        var v = MyActualParameter[2];

        k[0] = MyH * v;
        l[0] = -MyH * (decimal)Math.Sin((double)u);

        k[1] = MyH * (v + l[0] / 2);
        l[1] = MyH * -(decimal)Math.Sin((double)(u + k[0] / 2));

        k[2] = MyH * (v + l[1] / 2);
        l[2] = MyH * -(decimal)Math.Sin((double)(u + k[1] / 2));

        // Corrected: VB used k[2] / 2 here, which reduced the method to order 1
        // (energy drift 4.5e-3 instead of 1e-10 for h = 0.02 up to t = 10).
        k[3] = MyH * (v + l[2]);
        l[3] = MyH * -(decimal)Math.Sin((double)(u + k[2]));

        MyActualParameter[1] = u + (k[0] + 2 * k[1] + 2 * k[2] + k[3]) / 6;
        MyActualParameter[2] = v + (l[0] + 2 * l[1] + 2 * l[2] + l[3]) / 6;
    }
}
