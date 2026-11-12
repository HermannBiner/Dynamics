using System.Globalization;
using System.Text.Json;
using Dynamics.Core.General;
using Dynamics.Core.LSystem;
using static Dynamics.Tests.ReferenceData;

namespace Dynamics.Tests.LSystem;

public class LSystemReferenceTests
{
    private const string File = "lsystem.json";

    public static TheoryData<string> Scenarios()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "ReferenceData", File);
        return new(JsonSerializer.Deserialize<Dictionary<string, string[][]>>(System.IO.File.ReadAllText(path))!.Keys);
    }

    private static int Int(string s) => int.Parse(s, CultureInfo.InvariantCulture);

    private static string MoveLine(TurtleMove move) =>
        "C: " + move.From.X.ToString("N2", CultureInfo.InvariantCulture) + "/" + move.From.Y.ToString("N2", CultureInfo.InvariantCulture) +
        "-> N: " + move.To.X.ToString("N2", CultureInfo.InvariantCulture) + "/" + move.To.Y.ToString("N2", CultureInfo.InvariantCulture);

    /// <summary>A controller with the English default systems of Simulator (the export used LSystemsEN.json).</summary>
    internal static LSystemsController EnglishController()
    {
        var store = new MemoryLSystemStore();
        var controller = new LSystemsController(store);
        using var stream = typeof(LSystemsController).Assembly.GetManifestResourceStream("Dynamics.Core.LSystem.LSystemsEN.json")!;
        store.Write(controller.BackupKey, new StreamReader(stream).ReadToEnd());
        Assert.Null(controller.RestoreCopy());
        return controller;
    }

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void GenerationsMatchVB(string scenario)
    {
        var rows = Rows(File, scenario);
        var setup = rows[0];
        var controller = EnglishController();
        var index = controller.Systems.ToList().FindIndex(s => s.Name == setup[1]);
        Assert.True(index >= 0, setup[1]);
        if (setup[3] == "True")
        {
            controller.Systems[index].IsExtended = true;
        }

        controller.SelectSystem(index);
        Assert.Equal(Int(setup[4]), controller.Turtles.Count);
        var system = controller.ActualSystem;

        foreach (var row in rows.Skip(1))
        {
            controller.Start();
            Assert.Equal(IterationStatus.Running, controller.Status);
            Assert.Equal(Int(row[1]), controller.Generation);
            Assert.Equal(Int(row[2]), controller.Itinerary.Length);
            Assert.StartsWith(row[3], controller.Itinerary, StringComparison.Ordinal);

            var moves = new List<TurtleMove>();
            controller.Draw(int.MaxValue, moves.Add, null);
            Assert.Equal(IterationStatus.Stopped, controller.Status);

            var perTurtle = moves.Count / controller.Turtles.Count;
            for (var t = 0; t < controller.Turtles.Count; t++)
            {
                var expected = row.Skip(4 + 8 * t).Take(8).ToArray();
                var state = controller.Turtles[t].State;
                Assert.Equal(Dec(expected[0]), state.Position.X);
                Assert.Equal(Dec(expected[1]), state.Position.Y);
                Assert.Equal(Int(expected[2]), state.Angle);

                // VB read the step length before the scaling for the next generation
                var stepLength = Dec(expected[3]);
                Assert.Equal(system.Scaling == LSystemScaling.ByIteration ? stepLength * system.ScaleFactor : system.StartLength, state.StepLength);
                Assert.Equal(expected[4], LSystemColors.Colors[state.Color].Name);
                Assert.Equal(Int(expected[5]), perTurtle);
                if (perTurtle > 0)
                {
                    Assert.Equal(expected[6], MoveLine(moves[t * perTurtle]));
                    Assert.Equal(expected[7], MoveLine(moves[(t + 1) * perTurtle - 1]));
                }
            }
        }
    }
}
