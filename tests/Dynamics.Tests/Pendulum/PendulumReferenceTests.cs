using System.Globalization;
using System.Text.Json;
using Dynamics.Core.General;
using Dynamics.Core.Pendulum;
using static Dynamics.Tests.ReferenceData;

namespace Dynamics.Tests.Pendulum;

/// <summary>
/// Replays the scenarios of tools/ReferenceExport/PendulumExport.cs with the C# pendulums
/// and compares the recorded values with the VB results: the start exactly up to rounding, the chaotic
/// trajectories as long as Double and Decimal can agree (see <see cref="ReferenceData.FollowsVB"/>).
/// </summary>
public class PendulumReferenceTests(Xunit.Abstractions.ITestOutputHelper output)
{
    private Xunit.Abstractions.ITestOutputHelper Output { get; } = output;

    /// <summary>Tolerance of a trajectory row (relative, Double against Decimal).</summary>
    private const double TrajectoryTolerance = 1e-6;

    /// <summary>Rows that have to follow VB before the trajectories may part (measured 2026-11-12: 27 of 40 for the most
    /// chaotic scenarios, all 40 for the others).</summary>
    private const int MinimumSteps = 25;

    /// <summary>
    /// The test modes of the shaken and the combined pendulum are excluded: their VB errors were corrected
    /// in Dynamics, they are checked by <see cref="PendulumTestModeTests"/> instead.
    /// </summary>
    public static TheoryData<string> Scenarios()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "ReferenceData", "pendulum.json");
        var data = JsonSerializer.Deserialize<Dictionary<string, string[][]>>(File.ReadAllText(path))!;
        return new TheoryData<string>(data.Keys.Where(k => k is not ("Shaked_TestMode" or "Combined_TestMode")));
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
        Close(start[1], startEnergy);
        Close(start[2], p.StartEnergyRange!.A);
        Close(start[3], p.StartEnergyRange.B);
        var constants = p.CalculationConstants;
        Assert.Equal(start[4] == "", constants is null);
        if (constants is not null)
        {
            Close(start[4], constants[0]);
            Close(start[5], constants[1]);
        }

        Close(start[6], p.CalculationVariables[0]);
        Close(start[7], p.CalculationVariables[1]);
        Close(start[8], p.Y0);

        // The pendulums are chaotic: Double follows VB's Decimal trajectory for a while, then they part.
        var trajectory = rows.Skip(2).ToArray();
        var n = 0;
        var matching = FollowsVB(scenario, trajectory.Length, i =>
        {
            var row = trajectory[i];
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

            double[] expected = [.. row[2..10].Select(Dec), Dec(row[11])];
            double[] actual = [u1, v1, u2, v2, position1.X, position1.Y, position2.X, position2.Y, p.GetEnergy()];
            return expected.Zip(actual).All(pair => IsClose(pair.First, pair.Second, TrajectoryTolerance));
        }, MinimumSteps);
        Output.WriteLine($"{scenario}: {matching} of {trajectory.Length} rows (up to step {n}) follow VB");
    }

    private static NTuple Tuple(string a, string b)
    {
        var tuple = new NTuple(1);
        tuple[0] = Dec(a);
        tuple[1] = Dec(b);
        return tuple;
    }
}
