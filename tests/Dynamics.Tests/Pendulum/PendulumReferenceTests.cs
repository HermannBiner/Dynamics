using System.Globalization;
using System.Text.Json;
using Dynamics.Core.General;
using Dynamics.Core.Pendulum;
using static Dynamics.Tests.ReferenceData;

namespace Dynamics.Tests.Pendulum;

/// <summary>
/// Replays the scenarios of tools/ReferenceExport/PendulumExport.cs with the C# pendulums
/// and compares every recorded value exactly with the VB results.
/// </summary>
public class PendulumReferenceTests
{
    public static TheoryData<string> Scenarios()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "ReferenceData", "pendulum.json");
        var data = JsonSerializer.Deserialize<Dictionary<string, string[][]>>(File.ReadAllText(path))!;
        return new TheoryData<string>(data.Keys);
    }

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void TrajectoryMatchesVB(string scenario)
    {
        var rows = Rows("pendulum.json", scenario);
        var setup = rows[0];
        Assert.Equal("setup", setup[0]);

        IPendulum p = setup[1] switch
        {
            "ClsDoublePendulum" => new DoublePendulum(),
            "ClsShakedPendulum" => new ShakedPendulum(),
            "ClsCombinedPendulum" => new CombinedPendulum(),
            _ => throw new InvalidOperationException(setup[1]),
        };
        var testMode = bool.Parse(setup[4]);

        // InitializeMe
        p.IsMainDS = true;
        p.StepWidth = int.Parse(setup[3], CultureInfo.InvariantCulture);
        p.PhasePortraitType = (PhasePortraitType)int.Parse(setup[5], CultureInfo.InvariantCulture);
        p.AdditionalParameterValue = int.Parse(setup[2], CultureInfo.InvariantCulture);

        // SetDefaultUserData (controller), ResetIteration
        p.SetDefaultUserData();
        p.SetAdditionalParameters();
        p.ResetIteration();

        if (setup[6] != "")
        {
            p.SetStartparameter1(new MathPoint(Dec(setup[6]), Dec(setup[7])));
            p.IsStartparameter1Set = true;
        }
        if (setup[8] != "")
        {
            p.SetStartparameter2(new MathPoint(Dec(setup[8]), Dec(setup[9])));
        }
        if (setup[10] != "")
        {
            p.CalculationConstants = Tuple(setup[10], setup[11]);
        }
        if (setup[12] != "")
        {
            p.CalculationVariables = Tuple(setup[12], setup[13]);
        }

        // StartIteration
        p.IsStartparameter1Set = true;
        p.IsStartparameter2Set = true;
        var startEnergy = p.GetEnergy();
        p.SetAdditionalParameters();

        var start = rows[1];
        Assert.Equal(Dec(start[1]), startEnergy);
        Assert.Equal(Dec(start[2]), p.StartEnergyRange!.A);
        Assert.Equal(Dec(start[3]), p.StartEnergyRange.B);
        var constants = p.CalculationConstants;
        Assert.Equal(start[4], constants is null ? "" : Str(constants[0]));
        Assert.Equal(start[5], constants is null ? "" : Str(constants[1]));
        Assert.Equal(Dec(start[6]), p.CalculationVariables[0]);
        Assert.Equal(Dec(start[7]), p.CalculationVariables[1]);
        Assert.Equal(Dec(start[8]), p.Y0);

        var n = 0;
        foreach (var row in rows.Skip(2))
        {
            var target = int.Parse(row[1], CultureInfo.InvariantCulture);
            while (n < target)
            {
                p.IterationStep(testMode);
                n++;
            }

            var (u1, v1, u2, v2) = p.State;
            var arms = p.GetArms();
            var position1 = arms[0].To;
            var position2 = arms.Count > 1 ? arms[1].To : new MathPoint();

            decimal[] expected = [.. row[2..10].Select(Dec), Dec(row[11])];
            decimal[] actual = [u1, v1, u2, v2, position1.X, position1.Y, position2.X, position2.Y, p.GetEnergy()];
            Assert.True(expected.SequenceEqual(actual),
                $"{scenario}, step {n}:\nexpected {string.Join("; ", expected)}\nactual   {string.Join("; ", actual)}");
            Assert.Equal(bool.Parse(row[10]), p.IsSignumChanged);
        }
    }

    private static NTuple Tuple(string a, string b)
    {
        var tuple = new NTuple(1);
        tuple[0] = Dec(a);
        tuple[1] = Dec(b);
        return tuple;
    }

    private static string Str(decimal d) => d.ToString(CultureInfo.InvariantCulture);
}
