// Reference pictures of the VB Julia and Mandelbrot sets (ClsJuliaPN, ClsMandelbrot).
// Every pixel of a small square diagram is computed in row order (q outer, p inner, 1..Size);
// the VB classes draw into a bitmap, so they get an invisible picture box. The call order follows
// ClsJuliaController: PicDiagram, N, ranges, colors, protocol, C, ResetIteration, IterationStep.
//
// Rows per scenario:
//   ["setup", class, size, n, xA, xB, yA, yB, cX, cY, systemColors, red, green, blue, protocol]
//   ["pixels", "steps;argb steps;argb ..."]  one row per q, entries for p = 1..size
//   ["protocol", entry, entry, ...]

using System.Globalization;
using System.Reflection;
using System.Text;
using Simulator;

internal static class JuliaExport
{
    private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;

    private static string D(decimal d) => d.ToString(CultureInfo.InvariantCulture);
    private static string F(double d) => d.ToString("R", CultureInfo.InvariantCulture);
    private static string I(int i) => i.ToString(CultureInfo.InvariantCulture);

    private sealed record Scenario(
        string Name, Type Type, int Size, int N, decimal XA, decimal XB, decimal YA, decimal YB,
        double CX, double CY, bool SystemColors, int Red = 0, int Green = 10, int Blue = 2, bool Protocol = true);

    public static Dictionary<string, List<string[]>> Run()
    {
        var julia = typeof(ClsJuliaPN);
        var mandel = typeof(ClsMandelbrot);

        Scenario[] scenarios =
        [
            new("Julia_Seehorse_System", julia, 64, 2, -1.5m, 1.5m, -1.5m, 1.5m, 0.32, 0.043, true),
            new("Julia_Seehorse_User", julia, 64, 2, -1.5m, 1.5m, -1.5m, 1.5m, 0.32, 0.043, false),
            new("Julia_Rabbit_User", julia, 64, 2, -1.5m, 1.5m, -1.5m, 1.5m, -0.12256117, 0.744861771, false, 7, 3, 9),
            new("Julia_Dendrite_System", julia, 50, 2, -1.5m, 1.5m, -1.5m, 1.5m, 0, 1, true),
            new("Julia_Cristal_Zoom", julia, 64, 2, -0.4m, 0.1m, -0.3m, 0.25m, -1.5, 0, true),
            new("Julia_N3_System", julia, 64, 3, -1.5m, 1.5m, -1.5m, 1.5m, 0.4, 0.1, true),
            new("Julia_N5_User", julia, 48, 5, -1.5m, 1.5m, -1.5m, 1.5m, -0.5, 0.5, false, 10, 5, 0),
            new("Julia_BigC", julia, 40, 2, -1.5m, 1.5m, -1.5m, 1.5m, -1.45, 1.2, true),
            new("Mandelbrot_N2_System", mandel, 64, 2, -2m, 0.6m, -1.3m, 1.3m, 0, 0, true),
            new("Mandelbrot_N2_User", mandel, 64, 2, -2m, 0.6m, -1.3m, 1.3m, 0, 0, false),
            new("Mandelbrot_N2_Zoom", mandel, 64, 2, -0.8m, -0.7m, 0.05m, 0.15m, 0, 0, true),
            new("Mandelbrot_N3_System", mandel, 64, 3, -1.5m, 1.5m, -1.5m, 1.5m, 0, 0, true),
            new("Mandelbrot_N4_User", mandel, 48, 4, -1.5m, 1.5m, -1.5m, 1.5m, 0, 0, false, 3, 6, 10),
        ];

        var result = new Dictionary<string, List<string[]>>();
        foreach (var scenario in scenarios)
        {
            result[scenario.Name] = RunScenario(scenario);
        }

        return result;
    }

    private static List<string[]> RunScenario(Scenario s)
    {
        var ds = (IJulia)Activator.CreateInstance(s.Type)!;
        using var pic = new PictureBox { Width = s.Size, Height = s.Size };
        using var protocol = new ListBox();

        ds.PicDiagram = pic;
        ds.N = s.N;
        ds.ActualXRange = new ClsInterval(s.XA, s.XB);
        ds.ActualYRange = new ClsInterval(s.YA, s.YB);
        ds.ProcotolList = protocol;
        ds.IsProtocol = s.Protocol;
        ds.RedPercent = s.Red / 10.0;
        ds.GreenPercent = s.Green / 10.0;
        ds.BluePercent = s.Blue / 10.0;
        ds.IsUseSystemColors = s.SystemColors;
        ds.C = new ClsComplexNumber(s.CX, s.CY);
        ds.ResetIteration();

        var stepsField = s.Type.GetField("Steps", Hidden)!;
        var brushField = s.Type.GetField("MyBrush", Hidden)!;

        var rows = new List<string[]>
        {
            new[]
            {
                "setup", s.Type.Name, I(s.Size), I(s.N), D(s.XA), D(s.XB), D(s.YA), D(s.YB), F(s.CX), F(s.CY),
                s.SystemColors.ToString(), I(s.Red), I(s.Green), I(s.Blue), s.Protocol.ToString(),
            },
        };

        for (var q = 1; q <= s.Size; q++)
        {
            var line = new StringBuilder();
            for (var p = 1; p <= s.Size; p++)
            {
                ds.IterationStep(new Point(p, q));
                var steps = (int)stepsField.GetValue(ds)!;
                var color = ((SolidBrush)brushField.GetValue(ds)!).Color.ToArgb();
                if (p > 1)
                {
                    line.Append(' ');
                }
                line.Append(I(steps)).Append(';').Append(I(color));
            }
            rows.Add(["pixels", line.ToString()]);
        }

        var entries = new List<string> { "protocol" };
        foreach (var item in protocol.Items)
        {
            entries.Add(Convert.ToString(item, CultureInfo.InvariantCulture)!);
        }
        rows.Add([.. entries]);

        return rows;
    }
}
