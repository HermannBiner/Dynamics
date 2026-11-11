// Reference data of the VB billiards (ClsEllipseBilliardTable/-ball, ClsOvalBilliardTable/-ball,
// ClsStadiumBilliardTable/-ball). The tables and balls draw into picture boxes, therefore on an STA thread.
//
// Rows per scenario:
//   ["table", class, c, a, b]
//   ["orbit", t0, alfa0]  then ["step", n, T, Phi] after each IterationStep (ball with phase portrait and protocol)
//   ["protocol", line, ...]
//   ["pairs", t0, alfa0]  then ["pair", n, t, alfa] for GetNextValuePair (ball without phase portrait)
//   ["mouse", px, py, T]  SetAndDrawUserStartposition; ["mouseend", px, py, phi, alfa] SetAndDrawUserEndposition
//   ["error", text]       if the VB code threw an exception

using System.Globalization;
using System.Reflection;
using Simulator;

internal static class BilliardExport
{
    private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;

    private static string D(decimal d) => d.ToString(CultureInfo.InvariantCulture);
    private static string I(int i) => i.ToString(CultureInfo.InvariantCulture);

    private static decimal Field(object ball, string name) =>
        (decimal)typeof(ClsBilliardBallAbstract).GetField(name, Hidden)!.GetValue(ball)!;

    public static Dictionary<string, List<string[]>> Run()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;

        var ellipse = typeof(ClsEllipseBilliardTable);
        var oval = typeof(ClsOvalBilliardTable);
        var stadium = typeof(ClsStadiumBilliardTable);

        (string Name, Type Table, decimal C, decimal T0, decimal Alfa0, bool Protocol)[] scenarios =
        [
            ("Ellipse_Default", ellipse, 0.8m, 0, 1, true),
            ("Ellipse_C05", ellipse, 0.5m, 1.2m, 0.3m, true),
            ("Ellipse_C15", ellipse, 1.5m, 0.7m, 1.9m, true),
            ("Ellipse_C2", ellipse, 2m, 3m, 0.05m, true),
            ("Oval_Default", oval, 0.8m, 0, 1, false),
            ("Oval_C05", oval, 0.5m, 2m, 0.7m, false),
            ("Oval_C15", oval, 1.5m, 4m, 2.5m, false),
            ("Stadium_Default", stadium, 0.8m, 0, 1, false),
            ("Stadium_C05", stadium, 0.5m, 1.5m, 0.4m, false),
            ("Stadium_C2", stadium, 2m, 2.5m, 1.3m, false),
        ];

        var result = new Dictionary<string, List<string[]>>();
        foreach (var s in scenarios)
        {
            result[s.Name] = RunScenario(s.Table, s.C, s.T0, s.Alfa0, s.Protocol);
        }

        // C-diagram columns: logic of ClsCDiagramController.IterationLoop (picture 580 x 580, start value 60/120)
        foreach (var (name, table, a, b, columns, trackBar) in new (string, Type, decimal, decimal, int[], int)[]
                 {
                     ("CDiagram_Ellipse", ellipse, 0.5m, 2, [1, 290, 580], 60),
                     ("CDiagram_Oval", oval, 0.5m, 2, [1, 200, 580], 60),
                     ("CDiagram_Stadium", stadium, 0.5m, 2, [1, 400], 30),
                 })
        {
            result[name] = RunCDiagram(table, new ClsInterval(a, b), columns, trackBar);
        }

        return result;
    }

    private static List<string[]> RunScenario(Type tableType, decimal c, decimal t0, decimal alfa0, bool protocol)
    {
        var rows = new List<string[]>();
        using var pic = new PictureBox { Width = 600, Height = 600 };
        using var phase = new PictureBox { Width = 320, Height = 320 };
        using var list = new ListBox();

        var table = (IBilliardTable)Activator.CreateInstance(tableType)!;
        table.PicDiagram = pic;
        table.PhasePortrait = phase;
        table.ValueProtocol = list;
        table.C = c;
        rows.Add(["table", tableType.Name, D(table.C), D(table.A), D(table.B)]);

        // Orbit with IterationStep (as BtnTakeOver + Start)
        var ball = table.GetBilliardBall();
        ball.BallColor = Brushes.Red;
        ball.BallSpeed = 1000;
        ball.IsProtocol = protocol;
        rows.Add(["orbit", D(t0), D(alfa0)]);
        try
        {
            ball.Startparameter = t0;
            ball.Startangle = alfa0;
            for (var n = 1; n <= 60; n++)
            {
                ball.IterationStep();
                rows.Add(["step", I(n), D(Field(ball, "T")), D(Field(ball, "Phi"))]);
            }
        }
        catch (Exception ex)
        {
            rows.Add(["error", ex.GetType().Name]);
        }

        var entries = new List<string> { "protocol" };
        foreach (var item in list.Items)
        {
            entries.Add(Convert.ToString(item, CultureInfo.InvariantCulture)!);
        }
        rows.Add([.. entries]);

        // Value pairs (as the C-diagram, ball without phase portrait)
        var pairTable = (IBilliardTable)Activator.CreateInstance(tableType)!;
        pairTable.PicDiagram = pic;
        pairTable.C = c;
        var pairBall = pairTable.GetBilliardBall();
        var pair = new ClsDecValuePair(t0, alfa0);
        rows.Add(["pairs", D(t0), D(alfa0)]);
        try
        {
            for (var n = 1; n <= 200; n++)
            {
                var next = pairBall.GetNextValuePair(pair);
                pair.X = next.X;
                pair.Y = next.Y;
                rows.Add(["pair", I(n), D(pair.X), D(pair.Y)]);
            }
        }
        catch (Exception ex)
        {
            rows.Add(["error", ex.GetType().Name]);
        }

        // Mouse: start position, then end position (as MouseUp with IsDefinitive)
        foreach (var (sx, sy, ex, ey) in new[] { (300, 40, 520, 400), (100, 300, 450, 150), (480, 510, 120, 160), (590, 300, 20, 310) })
        {
            var mouseBall = table.GetBilliardBall();
            mouseBall.BallColor = Brushes.Blue;
            try
            {
                var t = mouseBall.SetAndDrawUserStartposition(new Point(sx, sy), true);
                mouseBall.Startparameter = t;
                rows.Add(["mouse", I(sx), I(sy), D(t)]);
                var phi = mouseBall.SetAndDrawUserEndposition(new Point(ex, ey), true);
                var alfa = mouseBall.CalculateAlfa(mouseBall.Startparameter, phi);
                rows.Add(["mouseend", I(ex), I(ey), D(phi), D(alfa)]);
            }
            catch (Exception exception)
            {
                rows.Add(["error", exception.GetType().Name]);
            }
        }

        return rows;
    }

    private static List<string[]> RunCDiagram(Type tableType, ClsInterval range, int[] columns, int trackBar)
    {
        const int width = 580, height = 580;
        using var pic = new PictureBox { Width = 600, Height = 600 };
        var table = (IBilliardTable)Activator.CreateInstance(tableType)!;
        table.PicDiagram = pic;
        var ball = table.GetBilliardBall();
        var rows = new List<string[]> { new[] { "cdiagram", tableType.Name, D(range.A), D(range.B), I(trackBar) } };

        foreach (var p in columns)
        {
            table.C = range.A + (range.IntervalWidth * p / width);
            ball.A = table.A;
            ball.B = table.B;
            var tRange = table.ValueParameterList[0].Range;
            var alfaRange = table.ValueParameterList[1].Range;
            var pair = new ClsDecValuePair(tRange.A + tRange.IntervalWidth / 3, alfaRange.A + trackBar * alfaRange.IntervalWidth / 120);
            var row = new List<string> { "column", I(p), D(table.C) };
            try
            {
                for (var n = 0; n <= height; n++)
                {
                    row.Add(D(pair.X) + ";" + D(pair.Y));
                    var next = ball.GetNextValuePair(pair);
                    pair.X = next.X;
                    pair.Y = next.Y;
                }
            }
            catch (Exception ex)
            {
                row.Add("error " + ex.GetType().Name);
            }

            rows.Add([.. row]);
        }

        return rows;
    }
}
