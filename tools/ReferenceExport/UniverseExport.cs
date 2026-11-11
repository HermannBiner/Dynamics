// Reference data of the VB universes (ClsNewtonUniverse, ClsNormedUniverse with ClsNewtonStar, ClsNormedStar).
// For every constellation the logic of ClsUniverseController is replayed: TakeOverConstellation (with zoom),
// StartIteration (step width through the track bar), then 300 loops of IterationStep and MoveActualPosition.
//
// Rows per scenario:
//   ["setup", universe, constellation index, step width, zoom, vFactor, centre x, centre y]
//   ["star", name, mass, user start x, y, user velocity x, y, totalMassOfOtherStars]
//   ["loop", n, then per star: u1, v1, u2, v2]  for n = 1..5 and every 25th loop
//   ["laws", n, energy, angular momentum, pulse x, pulse y]  sums over the stars at the same loops

using System.Globalization;
using System.Reflection;
using Simulator;

internal static class UniverseExport
{
    private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;

    private static string D(decimal d) => d.ToString(CultureInfo.InvariantCulture);
    private static string I(int i) => i.ToString(CultureInfo.InvariantCulture);

    private static decimal Field(IStar star, string name) =>
        (decimal)typeof(ClsStarAbstract).GetField(name, Hidden)!.GetValue(star)!;

    public static Dictionary<string, List<string[]>> Run()
    {
        var result = new Dictionary<string, List<string[]>>();
        foreach (var type in new[] { typeof(ClsNewtonUniverse), typeof(ClsNormedUniverse) })
        {
            var count = CreateUniverse(type, out _, out _).Constellations.Count;
            for (var i = 0; i < count; i++)
            {
                result[$"{type.Name}_{i}"] = RunConstellation(type, i);
            }
        }

        return result;
    }

    private static IUniverse CreateUniverse(Type type, out PictureBox pic, out PictureBox phase)
    {
        var universe = (IUniverse)Activator.CreateInstance(type)!;
        pic = new PictureBox { Width = 600, Height = 600 };
        phase = new PictureBox { Width = 321, Height = 320 };
        universe.PicDiagram = pic;
        universe.PicPhaseportrait = phase;
        universe.CreateConstellations();
        return universe;
    }

    private static List<string[]> RunConstellation(Type type, int index)
    {
        var ds = CreateUniverse(type, out var pic, out var phase);
        using var picDispose = pic;
        using var phaseDispose = phase;

        // FillDefaultStars
        ds.ActiveConstellation = ds.Constellations[index];
        ds.VFactor = ds.ActiveConstellation.VFactor;

        // TakeOverConstellation (ClearUniverse first; ChkZoom is checked by default)
        ds.ClearUniverse();
        decimal totalMass = 0;
        foreach (var star in ds.ActiveConstellation.Stars)
        {
            star.PicDiagram = ds.PicDiagram;
            star.PicGraphics = ds.PicGraphics;
            star.BmpDiagram = ds.BmpDiagram;
            star.BmpGraphics = ds.BmpGraphics;
            star.ActualStepWidth = 0;
            star.SetDefaultUserData();
            star.DrawStar(true);
            totalMass += star.OriginalMass;
            ds.ActiveStarCollection.Add(star);
        }

        foreach (var star in ds.ActiveStarCollection)
        {
            star.TotalMassOfOtherStars = totalMass - star.OriginalMass;
        }

        ds.RedrawUniverse(null, true);
        ds.ResetIteration();

        // StartIteration
        ds.ClearDiagrams();
        decimal locTotal = 0;
        foreach (var star in ds.ActiveStarCollection)
        {
            star.ResetIteration();
            locTotal += star.UserMass;
        }

        foreach (var star in ds.ActiveStarCollection)
        {
            star.TotalMassOfOtherStars = Math.Max(0, locTotal - star.UserMass);
        }

        // SetTrbFromStepWidth, then SetStepWidthFromTrb (track bar 1..72)
        var trackBar = Math.Clamp(ds.StepWidthToTrbValue(ds.ActiveConstellation.ProposedStepWidth), 1, 72);
        var stepWidth = ds.TrbValueToStepWidth(trackBar);
        foreach (var star in ds.ActiveStarCollection)
        {
            star.ActualStepWidth = stepWidth;
        }

        var centre = ds.GlobalCentreOfGravity;
        var rows = new List<string[]>
        {
            new[] { "setup", type.Name, I(index), D(stepWidth), D(ds.DiagramZoom), D(ds.VFactor), D(centre.X), D(centre.Y) },
        };
        foreach (var star in ds.ActiveStarCollection)
        {
            rows.Add(["star", star.Name, D(star.UserMass), D(star.UserStartPosition.X), D(star.UserStartPosition.Y),
                D(star.UserStartVelocity.X), D(star.UserStartVelocity.Y), D(star.TotalMassOfOtherStars)]);
        }

        for (var n = 1; n <= 300; n++)
        {
            foreach (var star in ds.ActiveStarCollection)
            {
                star.IterationStep(false);
            }

            foreach (var star in ds.ActiveStarCollection)
            {
                star.MoveActualPosition();
            }

            if (n <= 5 || n % 25 == 0)
            {
                var loop = new List<string> { "loop", I(n) };
                decimal energy = 0, angular = 0, px = 0, py = 0;
                foreach (var star in ds.ActiveStarCollection)
                {
                    loop.Add(D(Field(star, "u1")));
                    loop.Add(D(Field(star, "v1")));
                    loop.Add(D(Field(star, "u2")));
                    loop.Add(D(Field(star, "v2")));
                    energy += star.GetEnergy();
                    angular += star.GetAngularMomentum();
                    px += star.GetPulse().X;
                    py += star.GetPulse().Y;
                }

                rows.Add([.. loop]);
                rows.Add(["laws", I(n), D(energy), D(angular), D(px), D(py)]);
            }
        }

        return rows;
    }
}
