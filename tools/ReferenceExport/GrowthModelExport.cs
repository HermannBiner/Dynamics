// Reference data of the VB growth models (ClsLogisticGrowth, ClsMandelbrotReal, ClsParabola, ClsTentmap)
// and of the loops of the growth controllers (logic copied from the VB controllers, which need their forms).
//
// "Types": ["type", class name] in the order of Assembly.GetTypes (the order of the VB combo boxes)
// Trajectories: ["setup", class, a, x0, power], ["step", n, x] for n = 1..steps          (FN, also Sensitivity / Population)
// "Protocols": ["protocol", class, protocol, start value]                                  (CalculateStartValueForProtocol)
// "Targets":   ["target", class, start value, target value, adjusted start value]         (CalculateStartValueForTargetValue)
// Feigenbaum:  ["setup", class, aMin, aMax, width, height],
//              ["column", p, a, k1, v1, k2, v2, ...]  v_k = k-th drawn value before clamping (k = 0..4·height)
// Histogram:   ["setup", class, a, x0, width, steps], ["hits", h0, h1, ..., h_width]

using System.Globalization;
using Simulator;

internal static class GrowthModelExport
{
    private static string D(decimal d) => d.ToString(CultureInfo.InvariantCulture);
    private static string I(int i) => i.ToString(CultureInfo.InvariantCulture);

    public static Dictionary<string, List<string[]>> Run()
    {
        var result = new Dictionary<string, List<string[]>>();

        result["Types"] = typeof(IIteration).Assembly.GetTypes()
            .Where(t => t.GetInterfaces().Contains(typeof(IIteration)) && t.IsClass && !t.IsAbstract)
            .Select(t => new[] { "type", t.Name })
            .ToList();

        (string Name, Type Type, decimal A, decimal X, int Power, int Steps)[] trajectories =
        [
            ("Trajectory_Logistic_Chaos", typeof(ClsLogisticGrowth), 4m, 0.314159m, 1, 300),
            ("Trajectory_Logistic_Default", typeof(ClsLogisticGrowth), 2.8m, 0.314159m, 1, 100),
            ("Trajectory_Logistic_Power2", typeof(ClsLogisticGrowth), 3.5m, 0.2m, 2, 100),
            ("Trajectory_Logistic_Power5", typeof(ClsLogisticGrowth), 4m, 0.414m, 5, 60),
            ("Trajectory_Logistic_Sensitivity1", typeof(ClsLogisticGrowth), 4m, 0.1m, 1, 330),
            ("Trajectory_Logistic_Sensitivity2", typeof(ClsLogisticGrowth), 4m, 0.100000000000000000000000001m, 1, 330),
            ("Trajectory_Parabola_Chaos", typeof(ClsParabola), 2m, 0.314159m, 1, 300),
            ("Trajectory_Parabola_Power3", typeof(ClsParabola), 1.3m, -0.5m, 3, 100),
            ("Trajectory_Tentmap_Chaos", typeof(ClsTentmap), 2m, 0.314159m, 1, 200),
            ("Trajectory_Tentmap_1_5", typeof(ClsTentmap), 1.5m, 0.4999m, 1, 200),
            ("Trajectory_MandelbrotReal", typeof(ClsMandelbrotReal), -1.9m, 0m, 1, 200),
        ];

        foreach (var (name, type, a, x0, power, steps) in trajectories)
        {
            var ds = (IIteration)Activator.CreateInstance(type)!;
            ds.ParameterA = a;
            ds.Power = power;
            var rows = new List<string[]> { new[] { "setup", type.Name, D(a), D(x0), I(power) } };
            var x = x0;
            for (var n = 1; n <= steps; n++)
            {
                x = ds.FN(x);
                rows.Add(["step", I(n), D(x)]);
            }

            result[name] = rows;
        }

        Type[] models = [typeof(ClsLogisticGrowth), typeof(ClsParabola), typeof(ClsTentmap), typeof(ClsMandelbrotReal)];
        string[] protocols =
        [
            "0", "1", "01", "0110", "101100111000", "1111111111", "0000000000", "10101010101010101010",
            "11001010111100001010110011101010001110101011", // 44 digits
        ];

        var protocolRows = new List<string[]>();
        foreach (var type in models)
        {
            foreach (var protocol in protocols)
            {
                var ds = (IIteration)Activator.CreateInstance(type)!;
                ds.ParameterA = ds.ChaoticParameterValue;
                ds.Power = 1;
                protocolRows.Add(["protocol", type.Name, protocol, D(ds.CalculateStartValueForProtocol(protocol))]);
            }
        }

        result["Protocols"] = protocolRows;

        (Type Type, decimal Start, decimal Target)[] targets =
        [
            (typeof(ClsLogisticGrowth), 0.314159m, 0.9m),
            (typeof(ClsLogisticGrowth), 0.1m, 0.5m),
            (typeof(ClsLogisticGrowth), 0.7m, 0.001m),
            (typeof(ClsLogisticGrowth), 0.414m, 0.629m),
            (typeof(ClsLogisticGrowth), 0.407m, 0.513m),
            (typeof(ClsParabola), 0.314159m, -0.8m),
            (typeof(ClsParabola), -0.5m, 0.3m),
            (typeof(ClsParabola), -0.172m, 0.25m),
            (typeof(ClsTentmap), 0.314159m, 0.75m),
            (typeof(ClsTentmap), 0.6m, 0.123m),
            (typeof(ClsMandelbrotReal), 0.314159m, 1.5m),
        ];

        var targetRows = new List<string[]>();
        foreach (var (type, start, target) in targets)
        {
            var ds = (IIteration)Activator.CreateInstance(type)!;
            ds.ParameterA = ds.ChaoticParameterValue;
            ds.Power = 1;
            targetRows.Add(["target", type.Name, D(start), D(target), D(ds.CalculateStartValueForTargetValue(start, target))]);
        }

        result["Targets"] = targetRows;

        // ClsFeigenbaumController.IterationLoop
        (string Name, Type Type, decimal A, decimal B, int[] Columns)[] feigenbaum =
        [
            ("Feigenbaum_Logistic", typeof(ClsLogisticGrowth), 2m, 4m, [1, 100, 290, 450, 580]),
            ("Feigenbaum_Logistic_Zoom", typeof(ClsLogisticGrowth), 3.5m, 3.6m, [1, 333]),
            ("Feigenbaum_Parabola", typeof(ClsParabola), 1m, 2m, [1, 200, 580]),
            ("Feigenbaum_Tentmap", typeof(ClsTentmap), 1m, 2m, [1, 300, 580]),
            ("Feigenbaum_MandelbrotReal", typeof(ClsMandelbrotReal), -2m, 0m, [1, 150, 580]),
        ];

        const int width = 580;
        const int height = 580;
        const int runTimeUntilCycle = 1000;
        foreach (var (name, type, a, b, columns) in feigenbaum)
        {
            var ds = (IIteration)Activator.CreateInstance(type)!;
            ds.Power = 1;
            var range = new ClsInterval(a, b);
            var lengthOfCycle = 4 * height;
            var rows = new List<string[]> { new[] { "setup", type.Name, D(a), D(b), I(width), I(height) } };
            foreach (var p in columns)
            {
                ds.ParameterA = range.A + (range.IntervalWidth * p / width);
                var row = new List<string> { "column", I(p), D(ds.ParameterA) };
                var x = ds.CriticalPoint;
                var n = 1;
                do
                {
                    x = ds.FN(x);
                    n += 1;
                } while (!(n > runTimeUntilCycle - 1));

                n = runTimeUntilCycle;
                var k = 0;
                do
                {
                    if (k <= 20 || k % 25 == 0 || k == lengthOfCycle)
                    {
                        row.Add(I(k));
                        row.Add(D(x));
                    }

                    x = ds.FN(x);
                    k++;
                    n += 1;
                } while (!(n > runTimeUntilCycle + lengthOfCycle));

                rows.Add([.. row]);
            }

            result[name] = rows;
        }

        // ClsHistogramController.IterationLoop
        (string Name, Type Type, decimal A, decimal X, int Steps)[] histograms =
        [
            ("Histogram_Logistic", typeof(ClsLogisticGrowth), 4m, 0.314159m, 20000),
            ("Histogram_Parabola", typeof(ClsParabola), 2m, 0.314159m, 20000),
            ("Histogram_Tentmap", typeof(ClsTentmap), 2m, 0.314159m, 3000),
            ("Histogram_Logistic_3_7", typeof(ClsLogisticGrowth), 3.7m, 0.5m, 10000),
        ];

        foreach (var (name, type, a, x0, steps) in histograms)
        {
            var ds = (IIteration)Activator.CreateInstance(type)!;
            ds.Power = 1;
            ds.ParameterA = a;
            var hits = new int[width + 1];
            var x = x0;
            for (var n = 1; n <= steps; n++)
            {
                var p = (int)Math.Round((x - ds.ValueParameter.Range.A) * width / ds.ValueParameter.Range.IntervalWidth, MidpointRounding.ToEven);
                hits[p] += 1;
                x = ds.FN(x);
            }

            result[name] =
            [
                ["setup", type.Name, D(a), D(x0), I(width), I(steps)],
                ["hits", .. hits.Select(I)],
            ];
        }

        return result;
    }
}
