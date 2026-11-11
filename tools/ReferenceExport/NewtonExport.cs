// Reference pictures of the VB Newton iteration (ClsUnitRootCollection, ClsInversUnitRoots, ClsPolynom3C).
// Every pixel p = 1..Size, q = 0..Size - 1 is computed in row order; the VB classes draw a 2 x 2 ellipse into a bitmap,
// which puts pixel (p, q) into the bitmap pixels (p - 1, q) and (p, q). In row order the bitmap pixel (x, q) therefore
// holds the color of pixel (x + 1, q); the bitmap is read at the end.
// The call order follows ClsNewtonIterationController.StartIteration: ResetIteration first (it prepares the roots),
// then N, ranges, mixing, color and c. "cFirst" sets c before ResetIteration (the order of a second start).
//
// Rows per scenario:
//   ["setup", class, size, n, xA, xB, yA, yB, cX, cY, mixing, color, protocol, cFirst]
//   ["pixels", "argb argb ..."]  one row per q = 0..size - 1, entries for x = 0..size - 1
//   ["protocol", line, line, ...]

using System.Globalization;
using System.Reflection;
using System.Text;
using Simulator;

internal static class NewtonExport
{
    private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;

    private static string D(decimal d) => d.ToString(CultureInfo.InvariantCulture);
    private static string F(double d) => d.ToString("R", CultureInfo.InvariantCulture);
    private static string I(int i) => i.ToString(CultureInfo.InvariantCulture);

    private sealed record Scenario(
        string Name, Type Type, int Size, int N, decimal XA, decimal XB, decimal YA, decimal YB,
        double CX = 0.5, double CY = 1, INewton.EnMixing Mixing = INewton.EnMixing.None,
        INewton.EnColor Color = INewton.EnColor.Shadowed, bool Protocol = false, bool CFirst = false);

    public static Dictionary<string, List<string[]>> Run()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        var unit = typeof(ClsUnitRootCollection);
        var invers = typeof(ClsInversUnitRoots);
        var poly = typeof(ClsPolynom3C);
        const INewton.EnMixing conj = INewton.EnMixing.Conjugate, rot = INewton.EnMixing.Rotate;
        const INewton.EnColor bright = INewton.EnColor.Bright, shaded = INewton.EnColor.Shadowed;

        Scenario[] scenarios =
        [
            new("Unit_N3", unit, 64, 3, -1.5m, 1.5m, -1.5m, 1.5m),
            new("Unit_N2_Protocol", unit, 24, 2, -1.5m, 1.5m, -1.5m, 1.5m, Protocol: true),
            new("Unit_N5_Bright", unit, 64, 5, -1.5m, 1.5m, -1.5m, 1.5m, Color: bright),
            new("Unit_N4_Zoom", unit, 64, 4, -0.1m, 0.4m, 0.2m, 0.7m),
            new("Unit_N3_Conjugate", unit, 64, 3, -1.5m, 1.5m, -1.5m, 1.5m, Mixing: conj, Color: bright),
            new("Unit_N6_Rotate", unit, 48, 6, -1.5m, 1.5m, -1.5m, 1.5m, Mixing: rot, Color: bright),
            new("Unit_N12", unit, 40, 12, -1.5m, 1.5m, -1.5m, 1.5m),
            new("Invers_N3", invers, 64, 3, -1.5m, 1.5m, -1.5m, 1.5m),
            new("Invers_N4_Rotate", invers, 48, 4, -1.5m, 1.5m, -1.5m, 1.5m, Mixing: rot, Color: bright),
            new("Invers_N2_Protocol", invers, 20, 2, -1.5m, 1.5m, -1.5m, 1.5m, Mixing: conj, Protocol: true),
            new("Poly3C_FirstStart", poly, 64, 3, -1.5m, 1.5m, -1.5m, 1.5m),
            new("Poly3C_Default", poly, 64, 3, -1.5m, 1.5m, -1.5m, 1.5m, CFirst: true),
            new("Poly3C_C1", poly, 48, 3, -1.5m, 1.5m, -1.5m, 1.5m, 1, 0, CFirst: true),
            new("Poly3C_CMinus1", poly, 48, 3, -1.5m, 1.5m, -1.5m, 1.5m, -1, 0, Color: bright, CFirst: true),
            new("Poly3C_Rotate", poly, 48, 3, -1.5m, 1.5m, -1.5m, 1.5m, -0.3, 0.8, rot, bright, CFirst: true),
            new("Poly3C_Zoom_Protocol", poly, 24, 3, -0.2m, 0.3m, 0.1m, 0.6m, 0.5, 1, conj, shaded, true, true),
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
        var ds = (INewton)Activator.CreateInstance(s.Type)!;
        using var pic = new PictureBox { Width = s.Size, Height = s.Size };
        using var protocol = new ListBox();

        ds.PicDiagram = pic;
        ds.ProcotolList = protocol;
        ds.N = s.N;
        ds.ActualXRange = ds.XValueParameter.Range;
        ds.ActualYRange = ds.YValueParameter.Range;

        if (s.CFirst)
        {
            ds.C = new ClsComplexNumber(s.CX, s.CY);
        }

        ds.ResetIteration();
        ds.N = ds.IsUseN ? s.N : 3;
        ds.ActualXRange = new ClsInterval(s.XA, s.XB);
        ds.ActualYRange = new ClsInterval(s.YA, s.YB);
        ds.IsProtocol = s.Protocol;
        ds.UseMixing = s.Mixing;
        ds.UseColor = s.Color;
        if (ds.IsUseC)
        {
            ds.C = new ClsComplexNumber(s.CX, s.CY);
        }

        var rows = new List<string[]>
        {
            new[]
            {
                "setup", s.Type.Name, I(s.Size), I(s.N), D(s.XA), D(s.XB), D(s.YA), D(s.YB), F(s.CX), F(s.CY),
                s.Mixing.ToString(), s.Color.ToString(), s.Protocol.ToString(), s.CFirst.ToString(),
            },
        };

        for (var q = 0; q < s.Size; q++)
        {
            for (var p = 1; p <= s.Size; p++)
            {
                ds.IterationStep(new Point(p, q));
            }
        }

        var bitmap = (Bitmap)typeof(ClsNewtonAbstract).GetField("BmpDiagram", Hidden)!.GetValue(ds)!;
        for (var y = 0; y < s.Size; y++)
        {
            var line = new StringBuilder();
            for (var x = 0; x < s.Size; x++)
            {
                if (x > 0)
                {
                    line.Append(' ');
                }
                line.Append(I(bitmap.GetPixel(x, y).ToArgb()));
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
