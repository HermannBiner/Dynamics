// Reference data of the VB L-systems (ClsLSystemExpander, ClsLTurtle), with the generation loop of
// ClsLSystemsController.StartIteration (the controller needs its form). The turtles draw into a picture box and write
// every move into a list box, therefore on an STA thread.
//
// Per system of Data/LSystemsEN.json (and the dragon curve extended with four turtles):
//   ["setup", name, type, extended, turtles]
//   ["generation", g, itinerary length, itinerary prefix (200 chars),
//      per turtle: x, y, angle, step length, color name, number of moves, first move line, last move line]
// The state is read after the generation is drawn, before the itinerary is expanded for the next one.

using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Simulator;

internal static class LSystemExport
{
    private const int MaxItineraryLength = 120000;
    private const int MaxGenerations = 7;

    private static string D(decimal d) => d.ToString(CultureInfo.InvariantCulture);
    private static string I(int i) => i.ToString(CultureInfo.InvariantCulture);

    public static Dictionary<string, List<string[]>> Run(string dataFile)
    {
        var result = new Dictionary<string, List<string[]>>();
        var systems = JsonSerializer.Deserialize<List<ClsLSystem>>(File.ReadAllText(dataFile))!;
        var scenarios = systems.Select(s => (s, false)).ToList();
        var dragon = JsonSerializer.Deserialize<List<ClsLSystem>>(File.ReadAllText(dataFile))!.First(s => s.Type == ClsLSystem.EnLSystemType.Dragon);
        dragon.IsExtended = true;
        scenarios.Add((dragon, true));

        foreach (var (system, extended) in scenarios)
        {
            var colors = new ClsColorList();
            var expander = new ClsLSystemExpander { Rules = system.Rules };
            var turtles = new List<ClsLTurtle> { new() };

            // SetLSystem
            if (system.Type == ClsLSystem.EnLSystemType.Dragon && system.IsExtended)
            {
                system.IsColorFixed = true;
                turtles[0].DefaultColor = colors.GetColor(2);
                foreach (var color in new[] { 12, 23, 20 })
                {
                    var turtle = new ClsLTurtle { DefaultColor = colors.GetColor(color) };
                    turtles.Add(turtle);
                }
            }
            else
            {
                system.IsColorFixed = false;
            }

            var picture = new System.Windows.Forms.PictureBox { Width = 600, Height = 600 };
            var debug = new System.Windows.Forms.ListBox();
            foreach (var turtle in turtles)
            {
                turtle.PicDiagram = picture;
                turtle.ScaleFactor = system.ScaleFactor;
                turtle.AngleLeft = system.AngleLeft;
                turtle.AngleRight = system.AngleRight;
                turtle.IsBudding = system.IsBudding;
                turtle.IsColorFixed = system.IsColorFixed;
                if (!turtle.IsColorFixed)
                {
                    turtle.DefaultColor = colors.GetColor(system.Color);
                }

                turtle.Scaling = system.Scaling;
                turtle.StartLength = system.StartLength;
                turtle.LstDebug = debug;
            }

            // ResetIteration
            var itinerary = system.Axiom;
            foreach (var turtle in turtles)
            {
                turtle.Reset();
                turtle.StartLength = system.StartLength;
            }

            var rows = new List<string[]>
            {
                new[] { "setup", system.Name, system.Type.ToString(), extended.ToString(), I(turtles.Count) },
            };

            for (var g = 1; g <= MaxGenerations && itinerary.Length <= MaxItineraryLength; g++)
            {
                // StartIteration
                foreach (var turtle in turtles)
                {
                    turtle.WorkingStatus = ClsLTurtle.EnWorkingStatus.Ready;
                    turtle.StartPosition = new ClsMathpoint(system.StartPositionX, system.StartPositionY);
                    turtle.CurrentAngle = turtles.IndexOf(turtle) * 90;
                    turtle.Generation += 1;
                }

                if (!(itinerary.Contains('F') || itinerary.Contains('G')))
                {
                    itinerary = expander.ExpandItinerary(itinerary);
                    foreach (var turtle in turtles)
                    {
                        turtle.Scale();
                    }
                }

                debug.Items.Clear();
                var row = new List<string> { "generation", I(g), I(itinerary.Length), itinerary[..Math.Min(200, itinerary.Length)] };
                foreach (var turtle in turtles)
                {
                    var before = debug.Items.Count;
                    turtle.DrawNextGeneration(itinerary).GetAwaiter().GetResult();
                    var state = (ClsLTurtleState)typeof(ClsLTurtle).GetField("MyTurtleStatus", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(turtle)!;
                    var moves = debug.Items.Count - before;
                    row.AddRange([
                        D(state.Position.X), D(state.Position.Y), I(state.Angle), D(state.StepLength), state.Color.Color.Name, I(moves),
                        moves > 0 ? (string)debug.Items[before]! : "", moves > 0 ? (string)debug.Items[debug.Items.Count - 1]! : "",
                    ]);
                }

                rows.Add([.. row]);

                // Generation finished
                itinerary = expander.ExpandItinerary(itinerary);
                foreach (var turtle in turtles)
                {
                    turtle.Scale();
                }
            }

            result[(extended ? "Extended " : "") + system.Name] = rows;
        }

        return result;
    }
}
