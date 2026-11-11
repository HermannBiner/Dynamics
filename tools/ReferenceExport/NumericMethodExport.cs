// Reference trajectories of the VB numeric methods (INumericMethod implementations).
// Rows per scenario:
//   ["setup", class, h, numberOfApproxSteps, amplitude, startVelocity]
//   ["step", n, t, y, y']   for n <= 5 and every 25th iteration

using System.Globalization;
using Simulator;

internal static class NumericMethodExport
{
    private static string D(decimal d) => d.ToString(CultureInfo.InvariantCulture);

    public static Dictionary<string, List<string[]>> Run()
    {
        Type[] types =
        [
            typeof(ClsRealSpringPendulum), typeof(ClsForwardEuler), typeof(ClsBackwardEuler),
            typeof(ClsMidpointImplicit), typeof(ClsRungeKutta4Pendulum), typeof(ClsRealThreadPendulum),
        ];
        (decimal H, int Steps, decimal Amplitude, decimal Velocity)[] settings =
        [
            (0.1m, 1, 0.5m, 0m),
            (0.02m, 5, -0.8m, 0m),
            (0.001m, 100, 0.95m, 0m),
            (0.05m, 2, 0.3m, 0.4m),
        ];

        var result = new Dictionary<string, List<string[]>>();
        foreach (var type in types)
        {
            for (var s = 0; s < settings.Length; s++)
            {
                var (h, steps, amplitude, velocity) = settings[s];
                var ds = (INumericMethod)Activator.CreateInstance(type)!;
                ds.h = h;
                ds.NumberOfApproxSteps = steps;
                ds.Amplitude = amplitude;
                ds.set_ActualParameter(0, 0);
                ds.set_ActualParameter(2, velocity);

                var rows = new List<string[]> { new[] { "setup", type.Name, D(h), steps.ToString(CultureInfo.InvariantCulture), D(amplitude), D(velocity) } };
                for (var n = 1; n <= 500; n++)
                {
                    ds.Iteration();
                    if (n <= 5 || n % 25 == 0)
                    {
                        rows.Add(["step", n.ToString(CultureInfo.InvariantCulture),
                            D(ds.get_ActualParameter(0)), D(ds.get_ActualParameter(1)), D(ds.get_ActualParameter(2))]);
                    }
                }

                result[$"{type.Name}_{s}"] = rows;
            }
        }

        return result;
    }
}
