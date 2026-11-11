// Reference trajectories of the VB strange attractors (ClsLinear, ClsLorenz, ClsRoessler, ClsThomas)
// and columns of the bifurcation diagram (logic of ClsBifurcationController.IterationLoop).
//
// Trajectory rows:
//   ["setup", class, dsParameterValue, stepWidth, x, y, z]
//   ["step", n, x, y, z]   for n <= 5 and every 50th step up to 3000
// Bifurcation rows (start points (0.1, 0.1, 0.1) and (-0.1, -0.1, 0.1), as in the controller):
//   ["setup", class, rangeA, rangeB, width, height, coordinate]
//   ["column", p, dsParameterValue, values of point 1 ..., values of point 2 ...]  (LengthOfCycle + 1 values each)

using System.Globalization;
using Simulator;

internal static class StrangeAttractorExport
{
    private static string D(decimal d) => d.ToString(CultureInfo.InvariantCulture);
    private static string I(int i) => i.ToString(CultureInfo.InvariantCulture);

    public static Dictionary<string, List<string[]>> Run()
    {
        var result = new Dictionary<string, List<string[]>>();

        (string Name, Type Type, decimal? Parameter, decimal StepWidth, decimal X, decimal Y, decimal Z)[] trajectories =
        [
            ("Lorenz_Classic", typeof(ClsLorenz), null, 0.001m, 1, 1, 1),
            ("Lorenz_Rho10", typeof(ClsLorenz), 10m, 0.005m, 10, 10, 10),
            ("Lorenz_Rho27_99", typeof(ClsLorenz), 27.99m, 0.01m, -14, -14, 39),
            ("Roessler_Standard", typeof(ClsRoessler), null, 0.01m, 0, 1, 0),
            ("Roessler_C3", typeof(ClsRoessler), 3m, 0.05m, 0.1m, -0.1m, 0.5m),
            ("Thomas_Standard", typeof(ClsThomas), null, 0.05m, 0, 0.1m, 0),
            ("Thomas_B0_1", typeof(ClsThomas), 0.1m, 0.1m, -0.2m, 0, -0.2m),
            ("Linear_Real", typeof(ClsLinear), null, 0.005m, 8, 8, 10),
            ("Linear_Complex", typeof(ClsLinear), 15m, 0.005m, 3, 1, 10),
            ("Linear_Complex_Large", typeof(ClsLinear), 19.98m, 0.1m, 5, 5, 10),
        ];

        foreach (var (name, type, parameter, stepWidth, x, y, z) in trajectories)
        {
            var ds = (IStrangeAttractor)Activator.CreateInstance(type)!;
            if (parameter is { } value)
            {
                ds.DSParameterValue = value;
            }
            ds.StepWidth = stepWidth;
            ds.SetIterationParameters();
            ds.ActualMathPoint = new ClsMathpoint(x, y, z);

            var rows = new List<string[]> { new[] { "setup", type.Name, D(ds.DSParameterValue), D(stepWidth), D(x), D(y), D(z) } };
            for (var n = 1; n <= 3000; n++)
            {
                ds.IterationStep();
                if (n <= 5 || n % 50 == 0)
                {
                    var p = ds.ActualMathPoint;
                    rows.Add(["step", I(n), D(p.X), D(p.Y), D(p.Z)]);
                }
            }

            result[name] = rows;
        }

        (string Name, Type Type, decimal A, decimal B, int Width, int Height, char Coordinate, int[] Columns)[] bifurcations =
        [
            ("Bifurcation_Lorenz_X", typeof(ClsLorenz), 0, 30, 580, 580, 'X', [1, 50, 290, 500, 580]),
            ("Bifurcation_Lorenz_Z_Zoom", typeof(ClsLorenz), 20, 26, 580, 580, 'Z', [1, 300]),
            ("Bifurcation_Roessler_Y", typeof(ClsRoessler), 1, 10, 580, 580, 'Y', [1, 200, 580]),
            ("Bifurcation_Thomas_X", typeof(ClsThomas), 0, 1, 580, 580, 'X', [1, 100, 400]),
            ("Bifurcation_Linear_X", typeof(ClsLinear), 0, 20, 580, 580, 'X', [1, 580]),
        ];

        foreach (var (name, type, a, b, width, height, coordinate, columns) in bifurcations)
        {
            var ds = (IStrangeAttractor)Activator.CreateInstance(type)!;
            var range = new ClsInterval(a, b);
            var lengthOfCycle = (int)Math.Round(height / 10.0, MidpointRounding.ToEven); // VB: CInt(Height / 10)
            const int runTimeUntilCycle = 5000;
            ClsIterationPoint[] points =
            [
                new(0.1m, 0.1m, 0.1m, Brushes.Blue),
                new(-0.1m, -0.1m, 0.1m, Brushes.Red),
            ];

            var rows = new List<string[]> { new[] { "setup", type.Name, D(a), D(b), I(width), I(height), coordinate.ToString() } };
            foreach (var p in columns)
            {
                ds.DSParameterValue = range.A + (range.IntervalWidth * p / width);
                var row = new List<string> { "column", I(p), D(ds.DSParameterValue) };
                foreach (var point in points)
                {
                    ds.ActualMathPoint = point.ActualPoint;
                    var n = 1;
                    do
                    {
                        ds.IterationStep();
                        n += 1;
                    } while (!(n > runTimeUntilCycle - 1));

                    n = runTimeUntilCycle;
                    do
                    {
                        ds.IterationStep();
                        var value = coordinate switch
                        {
                            'X' => ds.ActualMathPoint.X,
                            'Y' => ds.ActualMathPoint.Y,
                            _ => ds.ActualMathPoint.Z,
                        };
                        row.Add(D(value));
                        n += 1;
                    } while (!(n > runTimeUntilCycle + lengthOfCycle));
                }

                rows.Add([.. row]);
            }

            result[name] = rows;
        }

        return result;
    }
}
