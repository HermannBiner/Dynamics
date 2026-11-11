using Dynamics.Core.General;

namespace Dynamics.Core.Pendulum;

/// <summary>
/// Pendulum oscillating as spring pendulum and thread pendulum at the same time,
/// see the mathematical documentation. Port of ClsCombinedPendulum.
/// u1 = length l, u2 = angle Phi.
/// </summary>
public class CombinedPendulum : PendulumBase
{
    private const decimal BobRadius = 0.02m;

    /// <summary>Length of the unstressed spring (l0 in the documentation).</summary>
    private const decimal L0 = 0.5m;

    /// <summary>Minimal length of the spring.</summary>
    private const decimal Lmin = 0.05m;

    private readonly GeneralParameter[] _valueParameter = new GeneralParameter[2];
    private MathPoint _origin = new(0, 0);

    /// <summary>Frequency of the spring pendulum alone (additional parameter).</summary>
    private decimal Omega;

    public CombinedPendulum()
    {
        MyLabelProtocol = Text.Get("Protocol") + ": u1, v1, u2, v2, Etot";

        // Omega has to be bigger than a lower limit, see the documentation.
        var lowerLimitOmega = Convert.ToInt32(Math.Ceiling(Math.Max(
            Math.Sqrt((double)(g / (1 - L0))), Math.Sqrt((double)(g / L0)))));

        AdditionalParameterValue = lowerLimitOmega + 5;
        MyAdditionalParameter = new GeneralParameter(AdditionalParameterValue, Text.Get("SpringPendulumFrequency"),
            new Interval(lowerLimitOmega, (decimal)(lowerLimitOmega + 10)), GeneralParameter.ParameterType.DS);

        _valueParameter[0] = new GeneralParameter(1, "L", new Interval(Lmin, 2 - MyY0),
            GeneralParameter.ParameterType.Variable, L0);
        _valueParameter[1] = new GeneralParameter(2, "Phi", new Interval(-(decimal)Math.PI, (decimal)Math.PI),
            GeneralParameter.ParameterType.Variable, (decimal)(Math.PI / 6));
        MyValueParameterDefinition.AddRange(_valueParameter);

        // No constants; variables: L = [0], Phi = [1]
        MyCalculationVariables = new NTuple(1);

        SetPhasePortraitParameters();
    }

    public override string ClassName => "ClsCombinedPendulum";

    public override IPendulum GetCopy() => new CombinedPendulum();

    public override decimal GetAddParameterValue(int trackBarValue) => trackBarValue;

    public override IReadOnlyList<PendulumArm> GetArms() =>
        [new PendulumArm(_origin, Position1, BobRadius, DrawRole.PendulumColor)];

    public override (MathPoint From, MathPoint To) GetTrackSegment() => (OldPosition1, Position1);

    /// <summary>Equilibrium line where the spring compensates gravitation, in polar coordinates around (0, Y0).</summary>
    public override IReadOnlyList<MathPoint> GetBackgroundCurve()
    {
        const int numberOfSteps = 500;
        var points = new List<MathPoint>(numberOfSteps + 1);

        for (var i = 0; i <= numberOfSteps; i++)
        {
            var angle = MathHelperPendulum.AngleInMinusPiAndPi((decimal)(-Math.PI + i * 2 * Math.PI / numberOfSteps));
            var r = L0 + (decimal)((double)g * Math.Cos((double)angle) / Math.Pow((double)Omega, 2));
            points.Add(new MathPoint(r * (decimal)Math.Sin((double)angle), MyY0 - r * (decimal)Math.Cos((double)angle)));
        }

        return points;
    }

    public override IReadOnlyList<PhasePoint> GetPhasePortraitPoints() =>
        MyPhasePortraitType == PhasePortraitType.PoincareSection
            ? MyIsSignumChanged ? [new PhasePoint(new MathPoint(u1, v1), DrawRole.PendulumColor, 2)] : []
            : base.GetPhasePortraitPoints();

    protected override void SetPosition()
    {
        Position1.X = MyCalculationVariables[0] * (decimal)Math.Sin((double)MyCalculationVariables[1]);
        Position1.Y = -MyCalculationVariables[0] * (decimal)Math.Cos((double)MyCalculationVariables[1]) + MyY0;
    }

    protected override void SetPhasePortraitParameters()
    {
        var phasePortrait = Text.Get("PhasePortrait");
        switch (MyPhasePortraitType)
        {
            case PhasePortraitType.TorusOrCylinder:
                MyLabelPhasePortrait = phasePortrait + ": l, Phi. Tangent: l', Phi'";
                MyUInterval = new Interval(Lmin, 1m);
                MyVInterval = new Interval((decimal)-Math.PI, (decimal)Math.PI);
                break;
            case PhasePortraitType.PoincareSection:
                MyLabelPhasePortrait = phasePortrait + ": Phi = 0, l, l'";
                MyUInterval = new Interval(Lmin, 2m);
                MyVInterval = new Interval(-10m, 10m);
                break;
            default:
                MyLabelPhasePortrait = phasePortrait + ": Blue: l, l'. Red: Phi, Phi'";
                MyUInterval = new Interval((decimal)-Math.PI, (decimal)Math.PI);
                MyVInterval = new Interval(-10m, 10m);
                break;
        }
    }

    public override void SetAdditionalParameters()
    {
        Omega = GetAddParameterValue(AdditionalParameterValue);
        MyY0 = (decimal)((double)g / Math.Pow((double)Omega, 2) / (double)(1 - L0));
        _origin = new MathPoint(0, MyY0);

        if (IsMainDS)
        {
            ResetIteration();
            SetStartEnergyRange();
        }

        SetPosition();
    }

    protected override void SetStartEnergyRange()
    {
        var emin = -(decimal)Math.Pow((double)(g / Omega), 2) / 2;
        var emax = (decimal)Math.Pow((double)(Omega * (1 - MyY0 - L0)), 2) / 2 + g * (L0 + 1 - MyY0);
        MyStartEnergyRange = new Interval(emin, emax);
    }

    public override void SetStartparameter1(MathPoint position)
    {
        var px = Math.Min(Math.Max(MathInterval.A * ShrinkFactor, position.X), MathInterval.B * ShrinkFactor);
        var py = Math.Min(Math.Max(MathInterval.A * ShrinkFactor, position.Y), MathInterval.B * ShrinkFactor);
        py -= MyY0;

        var l = Math.Min(_valueParameter[0].Range.B, (decimal)Math.Sqrt((double)(px * px + py * py)));
        MyCalculationVariables[0] = l;

        var phi = MathHelperPendulum.AngleInMinusPiAndPi(MathHelperPendulum.GetAngle(px, py));
        MyCalculationVariables[1] = phi;

        SetStartEnergyRange();
        SetPosition();

        u1 = l;
        v1 = 0;
        u2 = phi;
        v2 = 0;
    }

    // The whole start position is set with the first parameter.
    public override void SetStartparameter2(MathPoint position)
    {
    }

    public override void SetDefaultUserData()
    {
        MyCalculationVariables[0] = _valueParameter[0].DefaultValue;
        u1 = MyCalculationVariables[0];
        v1 = 0;
        MyCalculationVariables[1] = _valueParameter[1].DefaultValue;
        u2 = MyCalculationVariables[1];
        v2 = 0;

        // Also sets Omega, Y0, origin and the start energy range.
        SetAdditionalParameters();
    }

    public override void IterationStep(bool isTestMode)
    {
        SaveOldPositions();

        RungeKuttaStep(isTestMode);

        if (u1 < Lmin)
        {
            u1 = Lmin;
            v1 = -v1;
        }

        u2 = MathHelperPendulum.AngleInMinusPiAndPi(u2);

        MyIsSignumChanged = MyCalculationVariables[1] * u2 <= 0;
        MyCalculationVariables[0] = u1;
        MyCalculationVariables[1] = u2;

        SetPosition();
    }

    public override decimal GetEnergy()
    {
        // Norm m = 1. Kinetic: (v1^2 + l^2*v2^2)/2
        var eKin = (decimal)(Math.Pow((double)v1, 2) + Math.Pow((double)(u1 * v2), 2)) / 2;

        var ePot = g * (L0 - (decimal)((double)u1 * Math.Cos((double)u2)));

        // Spring constant D = Omega^2 (m = 1)
        var eSpring = (decimal)Math.Pow((double)(Omega * (u1 - L0)), 2) / 2;

        return eKin + ePot + eSpring;
    }

    // Corrected: VB caught arithmetic errors, showed a message box and went on with a partial value.
    // Here an overflow propagates; the UI stops the iteration and shows a message.
    protected override decimal G1(NTuple x)
    {
        var temp = x[0] * (decimal)Math.Pow((double)x[3], 2);
        temp += (decimal)(-Math.Pow((double)Omega, 2) * (double)(x[0] - L0) + (double)g * Math.Cos((double)x[2]));
        return temp;
    }

    // (-2*v2*v1 - g*sin(u2)) / u1, with u1 = l > 0
    protected override decimal G2(NTuple x) =>
        (-2 * x[3] * x[1] - g * (decimal)Math.Sin((double)x[2])) / x[0];

    // Test mode: u1 = l oscillates as a simple spring pendulum around L0, u2 = Phi as a simple thread pendulum
    // of length L0. Corrected: VB applied the thread equation to l and the spring equation to Phi.
    protected override decimal G1Test(NTuple x) => -(x[0] - L0) * (decimal)Math.Pow((double)Omega, 2);

    protected override decimal G2Test(NTuple x) => -g * (decimal)Math.Sin((double)x[2]) / L0;
}
