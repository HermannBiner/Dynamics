using Dynamics.Core.General;

namespace Dynamics.Core.GrowthModel;

// Ports of ClsLogisticGrowth, ClsMandelbrotReal, ClsParabola and ClsTentmap.
// The Decimal/Double mix of VB is kept: Math functions take the Decimal as Double, CDec converts back.

/// <summary>Logistic growth f(x) = a·x·(1 − x), x in [0, 1], a in [0, 4].</summary>
public sealed class LogisticGrowth : GrowthModelBase
{
    protected override void InitializeIterator()
    {
        DSParameter = new GeneralParameter(1, "Parameter a", new Interval(0m, 4m), GeneralParameter.ParameterType.DS, 2.8m);
        ValueParameter = new GeneralParameter(2, "Value x", new Interval(0m, 1m), GeneralParameter.ParameterType.Variable, 0.314159m);
        CriticalPoint = 0.5m;
        ChaoticParameterValue = 4;

        // first 2-, 4-, 8-, 16-cycle, limit of the period doubling
        Splitpoints = [3m, 3.449449m, 3.54409m, 3.564407m, 3.569946m];
    }

    protected override decimal F(decimal x) => ParameterA * x * (1 - x);

    protected override decimal IterationToTentmap(decimal x) => (decimal)(2 * Math.Asin(Math.Sqrt((double)x)) / Math.PI);

    protected override decimal TentmapToIteration(decimal u)
    {
        var x = (decimal)Math.Sin(Math.PI * (double)u / 2);
        return x * x;
    }
}

/// <summary>Mandelbrot iteration for real numbers f(x) = a + x²; only for the Feigenbaum diagram.</summary>
public sealed class MandelbrotReal : GrowthModelBase
{
    protected override void InitializeIterator()
    {
        DSParameter = new GeneralParameter(1, "Parameter a", new Interval(-2m, 0m), GeneralParameter.ParameterType.DS, -1m);
        ValueParameter = new GeneralParameter(2, "Value x", new Interval(-2m, 2m), GeneralParameter.ParameterType.Variable, 0.314159m);
        CriticalPoint = 0;
        ChaoticParameterValue = -2;
        Splitpoints = [-0.75m, -1.24995m, -1.3681m, -1.39405m, -1.401155m];
    }

    protected override decimal F(decimal x) => ParameterA + x * x;

    protected override decimal IterationToTentmap(decimal x)
    {
        // VB-compatible: clamped to [-1, 1] (the class is only used for the Feigenbaum diagram)
        x = Math.Max(-1, Math.Min(1, x));
        return (decimal)(Math.Acos((double)(x / 2)) / Math.PI);
    }

    protected override decimal TentmapToIteration(decimal u) => (decimal)(2 * Math.Cos(Math.PI * (double)u));
}

/// <summary>Parabola f(x) = 1 − a·x², x in [−1, 1], a in [0, 2].</summary>
public sealed class Parabola : GrowthModelBase
{
    protected override void InitializeIterator()
    {
        DSParameter = new GeneralParameter(1, "Parameter a", new Interval(0m, 2m), GeneralParameter.ParameterType.DS, 2m);
        ValueParameter = new GeneralParameter(2, "Value x", new Interval(-1m, 1m), GeneralParameter.ParameterType.Variable, 0.314159m);
        CriticalPoint = 0;
        ChaoticParameterValue = 2;
        Splitpoints = [0.75m, 1.24995m, 1.3681m, 1.39405m, 1.401155m];
    }

    protected override decimal F(decimal x) => 1 - (ParameterA * x * x);

    protected override decimal IterationToTentmap(decimal x)
    {
        // Because of roundings, x can be a little outside [-1, 1]
        x = Math.Max(-1, Math.Min(1, x));
        return (decimal)(1 - (Math.Acos((double)x) / Math.PI));
    }

    protected override decimal TentmapToIteration(decimal u) => (decimal)Math.Cos(Math.PI * (double)(1 - u));
}

/// <summary>Tent map f(u) = a·u for u &lt; 0.5, a·(1 − u) otherwise; u in [0, 1], a in [0, 2].</summary>
public sealed class Tentmap : GrowthModelBase
{
    protected override void InitializeIterator()
    {
        DSParameter = new GeneralParameter(1, "Parameter a", new Interval(0m, 2m), GeneralParameter.ParameterType.DS, 2m);
        ValueParameter = new GeneralParameter(2, "Value x", new Interval(0m, 1m), GeneralParameter.ParameterType.Variable, 0.314159m);
        CriticalPoint = 0.4999m;
        ChaoticParameterValue = 2; // not used
        Splitpoints = [1m];
    }

    // VB compares the Decimal with the Double 0.5
    protected override decimal F(decimal u) => (double)u < 0.5 ? ParameterA * u : ParameterA * (1 - u);

    protected override decimal IterationToTentmap(decimal x) => x;

    protected override decimal TentmapToIteration(decimal u) => u;
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
