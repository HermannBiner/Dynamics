// Reference trajectories of the VB pendulums (ClsDoublePendulum, ClsShakedPendulum, ClsCombinedPendulum).
// The VB pendulums draw on every step, so they get invisible picture boxes. The call order follows
// ClsPendulumController: InitializeMe, SetDefaultUserData, ResetIteration, (start parameters), StartIteration.
//
// Rows per scenario:
//   ["setup", class, additionalParameterValue, stepWidth, testMode, phasePortraitIndex, start1X, start1Y, start2X, start2Y, takeOverC0, takeOverC1, takeOverV0, takeOverV1]
//     (empty strings if not used; start points are in mathematical coordinates)
//   ["start", startEnergy, energyRangeA, energyRangeB, c0, c1, var0, var1, y0]
//   ["step", n, u1, v1, u2, v2, p1x, p1y, p2x, p2y, isSignumChanged, energy]

using System.Globalization;
using System.Reflection;
using Simulator;

internal static class PendulumExport
{
    private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;

    private static string D(decimal d) => d.ToString(CultureInfo.InvariantCulture);

    private sealed record Scenario(
        string Name, Type Type, int? AdditionalParameterValue, int StepWidth, bool TestMode, int PhaseIndex,
        Point? Start1 = null, Point? Start2 = null, decimal[]? TakeOverC = null, decimal[]? TakeOverV = null, int Steps = 3000);

    public static Dictionary<string, List<string[]>> Run()
    {
        var dbl = typeof(ClsDoublePendulum);
        var shk = typeof(ClsShakedPendulum);
        var cmb = typeof(ClsCombinedPendulum);

        Scenario[] scenarios =
        [
            new("Double_Default", dbl, null, 10, false, 0),
            new("Double_M025_Step20", dbl, 0, 20, false, 1),
            new("Double_M4_Step3", dbl, 4, 3, false, 2),
            new("Double_TestMode", dbl, 2, 10, true, 0),
            new("Double_Mouse", dbl, 2, 10, false, 2, new Point(420, 380), new Point(470, 520)),
            new("Double_MouseFlat", dbl, 1, 15, false, 0, new Point(80, 290), new Point(20, 100)),
            new("Double_TakeOver", dbl, 3, 10, false, 2, TakeOverC: [0.5m, 0.4m], TakeOverV: [2.5m, -1m]),
            new("Shaked_Default", shk, null, 10, false, 0),
            new("Shaked_1_Step20", shk, 1, 20, false, 2),
            new("Shaked_10", shk, 10, 10, false, 1),
            new("Shaked_TestMode", shk, 5, 10, true, 0),
            new("Shaked_Mouse", shk, 7, 10, false, 2, new Point(380, 300), new Point(520, 420)),
            new("Shaked_MouseLeft", shk, 3, 10, false, 0, new Point(150, 300), new Point(60, 200)),
            new("Shaked_TakeOver", shk, 4, 12, false, 2, TakeOverC: [0.3m, 0.6m], TakeOverV: [0.3m, 2.9m]),
            new("Combined_Default", cmb, null, 10, false, 0),
            new("Combined_5_Step20", cmb, 5, 20, false, 2),
            new("Combined_15", cmb, 15, 10, false, 1),
            new("Combined_TestMode", cmb, 8, 10, true, 0),
            new("Combined_Mouse", cmb, 10, 10, false, 2, new Point(450, 500)),
            new("Combined_MouseHigh", cmb, 6, 5, false, 2, new Point(200, 50)),
            new("Combined_TakeOver", cmb, 12, 10, false, 2, TakeOverV: [0.8m, 1.2m]),
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
        var rows = new List<string[]>();
        var p = (IPendulum)Activator.CreateInstance(s.Type)!;

        // InitializeMe
        p.IsMainDS = true;
        p.PendulumColor = Brushes.Red;
        p.PicDiagram = new PictureBox { Width = 600, Height = 600 };
        p.PicPhaseportrait = new PictureBox { Width = 320, Height = 320 };
        p.Protocol = new ListBox();
        p.LblStepWidth = new Label();
        p.StepWidth = s.StepWidth;
        p.PhaseportraitIndex = s.PhaseIndex;
        if (s.AdditionalParameterValue is int add)
        {
            p.AdditionalParameterValue = add;
        }

        // SetDefaultUserData (controller), ResetIteration
        p.SetDefaultUserData();
        p.SetAdditionalParameters();
        p.PrepareDiagram();
        p.ResetIteration();

        string[] start1 = ["", ""];
        string[] start2 = ["", ""];
        if (s.Start1 is Point pixel1)
        {
            var m = p.PicGraphics.PixelToMathpoint(pixel1);
            start1 = [D(m.X), D(m.Y)];
            p.SetAndDrawStartparameter1(pixel1);
            p.IsStartparameter1Set = true;
        }
        if (s.Start2 is Point pixel2)
        {
            var m = p.PicGraphics.PixelToMathpoint(pixel2);
            start2 = [D(m.X), D(m.Y)];
            p.SetAndDrawStartparameter2(pixel2);
        }

        string[] takeOverC = ["", ""];
        string[] takeOverV = ["", ""];
        if (s.TakeOverC is { } c)
        {
            var tuple = new ClsNTupel(1);
            tuple.set_Component(0, c[0]);
            tuple.set_Component(1, c[1]);
            p.CalculationConstants = tuple;
            takeOverC = [D(c[0]), D(c[1])];
        }
        if (s.TakeOverV is { } v)
        {
            var tuple = new ClsNTupel(1);
            tuple.set_Component(0, v[0]);
            tuple.set_Component(1, v[1]);
            p.CalculationVariables = tuple;
            takeOverV = [D(v[0]), D(v[1])];
        }
        if (s.TakeOverC is not null || s.TakeOverV is not null)
        {
            p.PrepareDiagram();
        }

        rows.Add(["setup", s.Type.Name, D(p.AdditionalParameterValue), D(s.StepWidth), s.TestMode.ToString(), D(s.PhaseIndex),
            start1[0], start1[1], start2[0], start2[1], takeOverC[0], takeOverC[1], takeOverV[0], takeOverV[1]]);

        // StartIteration
        p.IsStartparameter1Set = true;
        p.IsStartparameter2Set = true;
        var startEnergy = p.GetEnergy();
        p.IsProtocol = false;
        p.SetAdditionalParameters();

        var constants = p.CalculationConstants;
        var variables = p.CalculationVariables;
        rows.Add(["start", D(startEnergy), D(p.StartEnergyRange.A), D(p.StartEnergyRange.B),
            constants is null ? "" : D(constants.get_Component(0)), constants is null ? "" : D(constants.get_Component(1)),
            D(variables.get_Component(0)), D(variables.get_Component(1)), D(Field<decimal>(p, "Y0"))]);

        for (var n = 1; n <= s.Steps; n++)
        {
            p.IterationStep(s.TestMode);
            if (n <= 10 || n % 100 == 0)
            {
                var p1 = Field<ClsMathpoint>(p, "Position1");
                var p2 = Field<ClsMathpoint>(p, "Position2");
                rows.Add(["step", D(n),
                    D(Field<decimal>(p, "u1")), D(Field<decimal>(p, "v1")), D(Field<decimal>(p, "u2")), D(Field<decimal>(p, "v2")),
                    D(p1.X), D(p1.Y), D(p2.X), D(p2.Y), Field<bool>(p, "IsSignumChanged").ToString(), D(p.GetEnergy())]);
            }
        }

        return rows;
    }

    private static T Field<T>(object o, string name) =>
        (T)typeof(ClsPendulumAbstract).GetField(name, Hidden)!.GetValue(o)!;

    private static string D(int i) => i.ToString(CultureInfo.InvariantCulture);
}
