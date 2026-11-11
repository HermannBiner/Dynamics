using Dynamics.Core.General;

namespace Dynamics.Core.Pendulum;

/// <summary>
/// Thread pendulum hanging on a horizontally shaken suspension, see the mathematical documentation.
/// Port of ClsShakedPendulum. The shaker position u1 is computed directly; only (u2, v2) is integrated.
/// </summary>
public class ShakedPendulum : PendulumBase
{
    private const decimal BobRadius = 0.02m;

    private readonly GeneralParameter[] _valueParameter = new GeneralParameter[4];
    private readonly MathPoint _origin = new(0, 0);

    /// <summary>Time.</summary>
    private decimal t;

    /// <summary>Frequency of the shaker.</summary>
    private decimal Omega;

    public ShakedPendulum()
    {
        MyLabelPhasePortrait = Text.Get("PhasePortrait") + ": -- ";
        MyLabelProtocol = Text.Get("Protocol") + ": u, v, Etot";

        _valueParameter[0] = new GeneralParameter(1, "A", ShrinkedInterval,
            GeneralParameter.ParameterType.Constant, ShrinkFactor / 2);
        _valueParameter[1] = new GeneralParameter(2, "l", new Interval(0.05m, 0.98m),
            GeneralParameter.ParameterType.Constant, 0.5m);
        _valueParameter[2] = new GeneralParameter(3, "x", ShrinkedInterval,
            GeneralParameter.ParameterType.Variable, ShrinkFactor / 2);
        _valueParameter[3] = new GeneralParameter(4, "Phi", new Interval(-(decimal)Math.PI, (decimal)Math.PI),
            GeneralParameter.ParameterType.Variable, (decimal)(Math.PI / 4));
        MyValueParameterDefinition.AddRange(_valueParameter);

        AdditionalParameterValue = 2;
        MyAdditionalParameter = new GeneralParameter(AdditionalParameterValue, Text.Get("FrequencyRatio"),
            new Interval(0.2m, 10m), GeneralParameter.ParameterType.DS);

        // Constants: A = [0], l = [1]; variables: x = [0], Phi = [1]
        MyCalculationConstants = new NTuple(1);
        MyCalculationVariables = new NTuple(1);

        SetPhasePortraitParameters();
    }

    public override string ClassName => "ClsShakedPendulum";

    private NTuple C => MyCalculationConstants!;

    public override IPendulum GetCopy() => new ShakedPendulum();

    // VB: CDec(AddParameter / 5) is a Double division.
    public override decimal GetAddParameterValue(int trackBarValue) => (decimal)(trackBarValue / 5.0);

    public override IReadOnlyList<PendulumArm> GetArms() =>
    [
        new PendulumArm(_origin, Position1, BobRadius, DrawRole.Primary),
        new PendulumArm(Position1, Position2, BobRadius, DrawRole.PendulumColor),
    ];

    // The track of the shaker is a straight line and not interesting.
    public override (MathPoint From, MathPoint To) GetTrackSegment() => (OldPosition2, Position2);

    protected override void SetPosition()
    {
        Position1.X = MyCalculationVariables[0];
        Position1.Y = 0;

        Position2.X = Position1.X + C[1] * (decimal)Math.Sin((double)MyCalculationVariables[1]);
        Position2.Y = -C[1] * (decimal)Math.Cos((double)MyCalculationVariables[1]);
    }

    protected override void SetPhasePortraitParameters()
    {
        var phasePortrait = Text.Get("PhasePortrait");
        switch (MyPhasePortraitType)
        {
            case PhasePortraitType.TorusOrCylinder:
                MyLabelPhasePortrait = phasePortrait + ": x, Phi. Tangent: x', Phi'";
                MyUInterval = new Interval(-1m, 1m);
                MyVInterval = new Interval((decimal)-Math.PI, (decimal)Math.PI);
                break;
            case PhasePortraitType.PoincareSection:
                MyLabelPhasePortrait = phasePortrait + ": x = 0, Phi, Phi'";
                MyUInterval = new Interval((decimal)-Math.PI, (decimal)Math.PI);
                MyVInterval = new Interval(-10m, 10m);
                break;
            default:
                MyLabelPhasePortrait = phasePortrait + ": Blue: x, x'. Red: Phi, Phi'";
                MyUInterval = new Interval((decimal)-Math.PI, (decimal)Math.PI);
                MyVInterval = new Interval(-10m, 10m);
                break;
        }
    }

    public override void SetAdditionalParameters()
    {
        Omega = (decimal)((double)GetAddParameterValue(AdditionalParameterValue) * Math.Sqrt((double)(g / C[1])));

        if (IsMainDS)
        {
            ResetIteration();
            SetStartEnergyRange();
        }

        SetPosition();
    }

    // Determined by experiments.
    protected override void SetStartEnergyRange() => MyStartEnergyRange = new Interval(0m, 30m);

    public override void SetStartparameter1(MathPoint position)
    {
        var a = (decimal)Math.Max((double)ShrinkedInterval.A * 0.6,
            Math.Min((double)position.X, (double)ShrinkedInterval.B * 0.6));

        t = 0;
        C[0] = a;
        MyCalculationVariables[0] = a;

        SetPosition();
        SetStartEnergyRange();

        // u1, v1 are not integrated; the shaker position is computed directly.
        u1 = 0;
        v1 = 0;
    }

    public override void SetStartparameter2(MathPoint position)
    {
        var deltaX = position.X - Position1.X;
        var deltaY = position.Y - Position1.Y;

        // L at most 0.98 to stay visible, at least 0.1.
        var l = (decimal)Math.Max(0.1, Math.Min(0.98 - (double)C[0],
            Math.Sqrt((double)(deltaX * deltaX + deltaY * deltaY))));

        var phi = MathHelperPendulum.GetAngle(deltaX, deltaY);
        MyCalculationVariables[1] = MathHelperPendulum.AngleInMinusPiAndPi(phi);

        // |x + L*sin(Phi)| should be at most ShrinkFactor; critical only if sign(x) = sign(Phi).
        if (Math.Abs((double)position.X + (double)l * Math.Sin((double)phi)) > (double)ShrinkFactor &&
            Math.Sign(phi) == Math.Sign(u1))
        {
            l = (decimal)Math.Min((double)l, (double)(1 - Math.Abs(u1)) / Math.Sin((double)Math.Abs(phi)));
        }

        C[1] = l;

        SetPosition();
        SetStartEnergyRange();

        u2 = phi;
        v2 = 0;
    }

    public override void SetDefaultUserData()
    {
        C[0] = _valueParameter[0].DefaultValue;
        C[1] = _valueParameter[1].DefaultValue;

        MyCalculationVariables[0] = _valueParameter[2].DefaultValue;
        MyCalculationVariables[1] = _valueParameter[3].DefaultValue;
        u2 = MyCalculationVariables[1];
        v2 = 0;

        t = 0;

        SetAdditionalParameters();
    }

    public override void IterationStep(bool isTestMode)
    {
        SaveOldPositions();

        x[0] = 0;
        x[1] = 0;
        x[2] = u2;
        x[3] = v2;
        Evaluate(0, isTestMode);

        x[2] = u2 + d * k2[0] / 2;
        x[3] = v2 + d * h2[0] / 2;
        Evaluate(1, isTestMode);

        x[2] = u2 + d * k2[1] / 2;
        x[3] = v2 + d * h2[1] / 2;
        Evaluate(2, isTestMode);

        x[2] = u2 + d * k2[2];
        x[3] = v2 + d * h2[2];
        Evaluate(3, isTestMode);

        t += d;

        u1 = (decimal)((double)C[0] * Math.Cos((double)(Omega * t)));
        v1 = -(decimal)((double)C[0] * Math.Sin((double)(Omega * t)));
        u2 += d * (k2[0] + 2 * k2[1] + 2 * k2[2] + k2[3]) / 6;
        v2 += d * (h2[0] + 2 * h2[1] + 2 * h2[2] + h2[3]) / 6;

        u2 = MathHelperPendulum.AngleInMinusPiAndPi(u2);

        MyIsSignumChanged = MyCalculationVariables[0] * u1 <= 0;
        MyCalculationVariables[0] = u1;
        MyCalculationVariables[1] = u2;

        SetPosition();
    }

    private void Evaluate(int step, bool isTestMode)
    {
        k1[step] = 0;
        h1[step] = 0;
        k2[step] = F2(x);
        h2[step] = isTestMode ? G2Test(x) : G2(x);
    }

    public override decimal GetEnergy()
    {
        // Increases if this pendulum takes energy from the shaker.
        var sinOmegaT = Math.Sin((double)(Omega * t));

        var eKin = (decimal)Math.Pow((double)(C[0] * Omega) * sinOmegaT, 2);
        eKin -= (decimal)((double)(2 * C[0] * Omega) * sinOmegaT * (double)C[1] * (double)v2 * Math.Cos((double)u2));
        eKin += (decimal)Math.Pow((double)(C[1] * v2), 2);
        eKin /= 2;

        var ePot = -g * (decimal)Math.Cos((double)u2) * C[1];

        return eKin + ePot;
    }

    protected override decimal G1(NTuple x) => 0;

    protected override decimal G1Test(NTuple x) => 0;

    protected override decimal G2(NTuple x)
    {
        var temp = (decimal)((double)C[0] * Math.Pow((double)Omega, 2) / (double)C[1]);
        temp *= (decimal)(Math.Cos((double)(Omega * t)) * Math.Cos((double)x[2]));
        temp -= (decimal)((double)(g / C[1]) * Math.Sin((double)x[2]));
        return temp;
    }

    // Test mode: simple thread pendulum of length l. Corrected: VB divided by A (constant 0) instead of l.
    protected override decimal G2Test(NTuple x) => -g * (decimal)(Math.Sin((double)x[2]) / (double)C[1]);
}
