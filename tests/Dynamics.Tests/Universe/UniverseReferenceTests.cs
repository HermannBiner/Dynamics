using System.Globalization;
using System.Text.Json;
using Dynamics.Core.Universe;
using static Dynamics.Tests.ReferenceData;

namespace Dynamics.Tests.Universe;

public class UniverseReferenceTests
{
    public static TheoryData<string> Scenarios()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "ReferenceData", "universe.json");
        return new TheoryData<string>(JsonSerializer.Deserialize<Dictionary<string, string[][]>>(File.ReadAllText(path))!.Keys);
    }

    private static int Int(string s) => int.Parse(s, CultureInfo.InvariantCulture);

    /// <summary>
    /// Same steps as the exporter: TakeOverConstellation with zoom and StartIteration. The trajectories (loop and laws
    /// rows) are not compared: the Runge-Kutta step was corrected in Dynamics (see UniverseRungeKuttaTests).
    /// </summary>
    [Theory]
    [MemberData(nameof(Scenarios))]
    public void ConstellationMatchesVB(string scenario)
    {
        // The VB language manager was German when the data was exported (names of the stars)
        CultureInfo.CurrentUICulture = new CultureInfo("de-CH");
        var rows = Rows("universe.json", scenario);
        var setup = rows[0];
        UniverseBase ds = setup[1] == "ClsNewtonUniverse" ? new NewtonUniverse() : new NormedUniverse();
        ds.CreateConstellations();
        ds.ActiveConstellation = ds.Constellations[Int(setup[2])];
        ds.VFactor = ds.ActiveConstellation.VFactor;

        ds.ClearUniverse();
        decimal totalMass = 0;
        foreach (var star in ds.ActiveConstellation.Stars)
        {
            star.ActualStepWidth = 0;
            star.SetDefaultUserData();
            totalMass += star.OriginalMass;
            ds.ActiveStarCollection.Add(star);
        }

        foreach (var star in ds.ActiveStarCollection)
        {
            star.TotalMassOfOtherStars = totalMass - star.OriginalMass;
        }

        ds.RedrawUniverse(null, true);
        ds.ResetIteration();

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

        var trackBar = Math.Clamp(ds.StepWidthToTrbValue(ds.ActiveConstellation.ProposedStepWidth), 1, 72);
        var stepWidth = ds.TrbValueToStepWidth(trackBar);
        foreach (var star in ds.ActiveStarCollection)
        {
            star.ActualStepWidth = stepWidth;
        }

        var centre = ds.GlobalCentreOfGravity;
        Assert.Equal([Dec(setup[3]), Dec(setup[4]), Dec(setup[5]), Dec(setup[6]), Dec(setup[7])],
            new[] { stepWidth, ds.DiagramZoom, ds.VFactor, centre.X, centre.Y });

        var starRows = rows.Where(r => r[0] == "star").ToList();
        Assert.Equal(starRows.Count, ds.ActiveStarCollection.Count);
        for (var i = 0; i < starRows.Count; i++)
        {
            var s = ds.ActiveStarCollection[i];
            Assert.Equal(starRows[i][1], s.Name);
            Assert.Equal(starRows[i].Skip(2).Select(Dec).ToArray(),
                new[] { s.UserMass, s.UserStartPosition.X, s.UserStartPosition.Y, s.UserStartVelocity.X, s.UserStartVelocity.Y, s.TotalMassOfOtherStars });
        }

    }
}
