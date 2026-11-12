using Dynamics.Core.General;

namespace Dynamics.Core.GrowthModel;

// Ports of ClsLogisticGrowth, ClsMandelbrotReal, ClsParabola and ClsTentmap.
// VB mixed Decimal and Double; here everything is Double, except the tent map step (see Tentmap.F).

/// <summary>Logistic growth f(x) = a·x·(1 − x), x in [0, 1], a in [0, 4].</summary>
public sealed class LogisticGrowth : GrowthModelBase
{
    protected override void InitializeIterator()
    {
        DSParameter = new GeneralParameter(1, "Parameter a", new Interval(0.0, 4.0), GeneralParameter.ParameterType.DS, 2.8);
        ValueParameter = new GeneralParameter(2, "Value x", new Interval(0.0, 1.0), GeneralParameter.ParameterType.Variable, 0.314159);
        CriticalPoint = 0.5;
        ChaoticParameterValue = 4;

        // first 2-, 4-, 8-, 16-cycle, limit of the period doubling
        Splitpoints = [3.0, 3.449449, 3.54409, 3.564407, 3.569946];
    }

    protected override double F(double x) => ParameterA * x * (1 - x);

    protected override double IterationToTentmap(double x) => (2 * Math.Asin(Math.Sqrt((double)x)) / Math.PI);

    protected override double TentmapToIteration(double u)
    {
        var x = Math.Sin(Math.PI * (double)u / 2);
        return x * x;
    }
}

/// <summary>Mandelbrot iteration for real numbers f(x) = a + x²; only for the Feigenbaum diagram.</summary>
public sealed class MandelbrotReal : GrowthModelBase
{
    protected override void InitializeIterator()
    {
        DSParameter = new GeneralParameter(1, "Parameter a", new Interval(-2.0, 0.0), GeneralParameter.ParameterType.DS, -1.0);
        ValueParameter = new GeneralParameter(2, "Value x", new Interval(-2.0, 2.0), GeneralParameter.ParameterType.Variable, 0.314159);
        CriticalPoint = 0;
        ChaoticParameterValue = -2;
        Splitpoints = [-0.75, -1.24995, -1.3681, -1.39405, -1.401155];
    }

    protected override double F(double x) => ParameterA + x * x;

    protected override double IterationToTentmap(double x)
    {
        // VB-compatible: clamped to [-1, 1] (the class is only used for the Feigenbaum diagram)
        x = Math.Max(-1, Math.Min(1, x));
        return (Math.Acos((double)(x / 2)) / Math.PI);
    }

    protected override double TentmapToIteration(double u) => (2 * Math.Cos(Math.PI * (double)u));
}

/// <summary>Parabola f(x) = 1 − a·x², x in [−1, 1], a in [0, 2].</summary>
public sealed class Parabola : GrowthModelBase
{
    protected override void InitializeIterator()
    {
        DSParameter = new GeneralParameter(1, "Parameter a", new Interval(0.0, 2.0), GeneralParameter.ParameterType.DS, 2.0);
        ValueParameter = new GeneralParameter(2, "Value x", new Interval(-1.0, 1.0), GeneralParameter.ParameterType.Variable, 0.314159);
        CriticalPoint = 0;
        ChaoticParameterValue = 2;
        Splitpoints = [0.75, 1.24995, 1.3681, 1.39405, 1.401155];
    }

    protected override double F(double x) => 1 - (ParameterA * x * x);

    protected override double IterationToTentmap(double x)
    {
        // Because of roundings, x can be a little outside [-1, 1]
        x = Math.Max(-1, Math.Min(1, x));
        return (1 - (Math.Acos((double)x) / Math.PI));
    }

    protected override double TentmapToIteration(double u) => Math.Cos(Math.PI * (double)(1 - u));
}

/// <summary>Tent map f(u) = a·u for u &lt; 0.5, a·(1 − u) otherwise; u in [0, 1], a in [0, 2].</summary>
public sealed class Tentmap : GrowthModelBase
{
    protected override void InitializeIterator()
    {
        DSParameter = new GeneralParameter(1, "Parameter a", new Interval(0.0, 2.0), GeneralParameter.ParameterType.DS, 2.0);
        ValueParameter = new GeneralParameter(2, "Value x", new Interval(0.0, 1.0), GeneralParameter.ParameterType.Variable, 0.314159);
        CriticalPoint = 0.4999;
        ChaoticParameterValue = 2; // not used
        Splitpoints = [1.0];
    }

    /// <summary>
    /// Computed in Decimal, as in VB: in binary Double, a = 2 shifts out one bit per step, so every orbit falls to the
    /// fixed point 0 after about 53 steps. Double → Decimal keeps 15 significant digits, so the orbit stays on the
    /// decimal grid of its start value (e.g. multiples of 10⁻⁶ for 0.314159) and follows VB exactly.
    /// </summary>
    protected override double F(double u)
    {
        var x = (decimal)u;
        var a = (decimal)ParameterA;
        return (double)(x < 0.5m ? a * x : a * (1 - x));
    }

    protected override double IterationToTentmap(double x) => x;

    protected override double TentmapToIteration(double u) => u;
}

/// <summary>The growth models found by reflection (VB filled the combo boxes by reflection).</summary>
public static class GrowthModels
{
    /// <summary>All models, ordered by name; the Mandelbrot iteration only for the Feigenbaum diagram.</summary>
    public static IReadOnlyList<Type> FindTypes(bool includeMandelbrot) => typeof(IIteration).Assembly.GetTypes()
        .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IIteration).IsAssignableFrom(t))
        .Where(t => includeMandelbrot || t != typeof(MandelbrotReal))
        .OrderBy(t => t.Name, StringComparer.Ordinal)
        .ToList();

    public static string DisplayName(Type type) => Text.GetClassName("Cls" + type.Name);

    public static IIteration Create(Type type) => (IIteration)Activator.CreateInstance(type)!;
}
