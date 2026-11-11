using Dynamics.Core.General;

namespace Dynamics.Core.Pendulum;

/// <summary>
/// Double pendulum, see the mathematical documentation. Port of ClsDoublePendulum.
/// Note from the original: the test mode (Runge-Kutta) does not represent a real double pendulum.
/// </summary>
public class DoublePendulum : PendulumBase
{
    private readonly GeneralParameter[] _valueParameter = new GeneralParameter[4];

    /// <summary>Mass ratio: m2 = M * m1.</summary>
    private decimal M;

    /// <summary>Mu = M / (1 + M).</summary>
    private decimal Mu;

    private readonly NTuple _pendulumSize = new(1);
    private readonly MathPoint _origin = new(0, 0);

    public DoublePendulum()
    {
        MyLabelProtocol = Text.Get("Parameterlist") + ": u1, v1, u2, v2, Etot";

        _valueParameter[0] = new GeneralParameter(1, "L1", new Interval(0.1m, ShrinkedInterval.B),
            GeneralParameter.ParameterType.Constant, 0.7m);
        _valueParameter[1] = new GeneralParameter(2, "L2", new Interval(0.1m, ShrinkedInterval.B),
            GeneralParameter.ParameterType.Constant, 0.2m);
        _valueParameter[2] = new GeneralParameter(3, "Phi 1", new Interval(-(decimal)Math.PI, (decimal)Math.PI),
            GeneralParameter.ParameterType.Variable, (decimal)(Math.PI / 4));
        _valueParameter[3] = new GeneralParameter(4, "Phi 2", new Interval(-(decimal)Math.PI, (decimal)Math.PI),
            GeneralParameter.ParameterType.Variable, (decimal)(Math.PI / 6));
        MyValueParameterDefinition.AddRange(_valueParameter);

        MyLabelPhasePortrait = Text.Get("PhasePortrait") + ": -- ";

        // Track bar value 2 means m2 = m1.
        AdditionalParameterValue = 2;
        MyAdditionalParameter = new GeneralParameter(AdditionalParameterValue, Text.Get("MassRatioM"),
            new Interval(0m, 4m), GeneralParameter.ParameterType.DS);

        // Constants: L1 = [0], L2 = [1]; variables: Phi1 = [0], Phi2 = [1]
        MyCalculationConstants = new NTuple(1);
        MyCalculationVariables = new NTuple(1);

        SetPhasePortraitParameters();
    }

    public override string ClassName => "ClsDoublePendulum";

    private NTuple C => MyCalculationConstants!;

    public override IPendulum GetCopy() => new DoublePendulum();

    public override decimal GetAddParameterValue(int trackBarValue) => (decimal)Math.Pow(2, trackBarValue - 2);

    public override IReadOnlyList<PendulumArm> GetArms() =>
    [
        new PendulumArm(_origin, Position1, _pendulumSize[0], DrawRole.Primary),
        new PendulumArm(Position1, Position2, _pendulumSize[1], DrawRole.PendulumColor),
    ];

    // The track of the upper pendulum is a circle and not interesting.
    public override (MathPoint From, MathPoint To) GetTrackSegment() => (OldPosition2, Position2);

    protected override void SetPosition()
    {
        Position1.X = C[0] * (decimal)Math.Sin((double)MyCalculationVariables[0]);
        Position1.Y = -C[0] * (decimal)Math.Cos((double)MyCalculationVariables[0]);

        Position2.X = Position1.X + C[1] * (decimal)Math.Sin((double)MyCalculationVariables[1]);
        Position2.Y = Position1.Y - C[1] * (decimal)Math.Cos((double)MyCalculationVariables[1]);
    }

    protected override void SetPhasePortraitParameters()
    {
        var phasePortrait = Text.Get("PhasePortrait");
        switch (MyPhasePortraitType)
        {
            case PhasePortraitType.TorusOrCylinder:
                MyLabelPhasePortrait = phasePortrait + ": Phi1, Phi2. Tangent: Phi1', Phi2'";
                MyUInterval = new Interval((decimal)-Math.PI, (decimal)Math.PI);
                MyVInterval = new Interval((decimal)-Math.PI, (decimal)Math.PI);
                break;
            case PhasePortraitType.PoincareSection:
                MyLabelPhasePortrait = phasePortrait + ": Phi1 = 0, Phi2, Phi2'";
                MyUInterval = new Interval((decimal)-Math.PI, (decimal)Math.PI);
                MyVInterval = new Interval(-10m, 10m);
                break;
            default:
                MyLabelPhasePortrait = phasePortrait + ": Blue: Phi1, Phi1'. Red: Phi2, Phi2'";
                MyUInterval = new Interval((decimal)-Math.PI, (decimal)Math.PI);
                MyVInterval = new Interval(-10m, 10m);
                break;
        }
    }

    public override void SetAdditionalParameters()
    {
        M = GetAddParameterValue(AdditionalParameterValue);
        Mu = M / (1 + M);
        SetPendulumSize();

        if (IsMainDS)
        {
            ResetIteration();
            SetStartEnergyRange();
        }

        SetPosition();
    }

    protected override void SetStartEnergyRange()
    {
        var emin = g * ((1 - C[0]) * (1 + M) - M * C[1]);
        var emax = g * ((1 + C[0]) * (1 + M) + M * C[1]);
        MyStartEnergyRange = new Interval(emin, emax);
    }

    private void SetPendulumSize()
    {
        // The smaller bob has radius 0.01; m2 = M * m1.
        if (M >= 1)
        {
            _pendulumSize[0] = 0.01m;
            _pendulumSize[1] = M * _pendulumSize[0];
        }
        else
        {
            _pendulumSize[1] = 0.01m;
            _pendulumSize[0] = _pendulumSize[1] / M;
        }
    }

    public override void SetStartparameter1(MathPoint position)
    {
        // L1 at most Range.B to leave room for L2, at least Range.A because of the divisor in the equations.
        var range = MyValueParameterDefinition[0].Range;
        var l1 = (decimal)Math.Max((double)range.A,
            Math.Min(Math.Sqrt((double)(position.X * position.X + position.Y * position.Y)), (double)range.B));

        var l2 = Math.Min(C[1], ShrinkFactor - l1);

        var phi1 = MathHelperPendulum.GetAngle(position.X, position.Y);

        C[0] = l1;
        C[1] = l2;
        MyCalculationVariables[0] = MathHelperPendulum.AngleInMinusPiAndPi(phi1);

        SetPosition();
        SetStartEnergyRange();

        u1 = phi1;
        v1 = 0;
    }

    public override void SetStartparameter2(MathPoint position)
    {
        var deltaX = position.X - Position1.X;
        var deltaY = position.Y - Position1.Y;

        // L1 + L2 at most 0.95 to stay visible.
        var range = MyValueParameterDefinition[1].Range;
        var l2 = (decimal)Math.Max((double)range.A,
            Math.Min(Math.Sqrt((double)(deltaX * deltaX + deltaY * deltaY)), 0.95 - (double)C[0]));
        var phi2 = MathHelperPendulum.GetAngle(deltaX, deltaY);

        C[1] = l2;
        MyCalculationVariables[1] = MathHelperPendulum.AngleInMinusPiAndPi(phi2);

        SetPosition();
        SetStartEnergyRange();

        u2 = phi2;
        v2 = 0;
    }

    public override void SetDefaultUserData()
    {
        C[0] = _valueParameter[0].DefaultValue;
        C[1] = _valueParameter[1].DefaultValue;

        MyCalculationVariables[0] = _valueParameter[2].DefaultValue;
        u1 = MyCalculationVariables[0];
        v1 = 0;
        MyCalculationVariables[1] = _valueParameter[3].DefaultValue;
        u2 = MyCalculationVariables[1];
        v2 = 0;

        SetAdditionalParameters();
        SetPendulumSize();
    }

    public override void IterationStep(bool isTestMode)
    {
        SaveOldPositions();

        RungeKuttaStep(isTestMode);

        u1 = MathHelperPendulum.AngleInMinusPiAndPi(u1);
        u2 = MathHelperPendulum.AngleInMinusPiAndPi(u2);

        MyIsSignumChanged = MyCalculationVariables[0] * u1 <= 0;
        MyCalculationVariables[0] = u1;
        MyCalculationVariables[1] = u2;

        SetPosition();
    }

    public override decimal GetEnergy()
    {
        // Norm: m1 = 1, m2 = M. Zero level of the potential energy at y = -1.
        // Kinetic energy of m1: L1^2 * v1^2 / 2
        var eKin = (decimal)(Math.Pow((double)(C[0] * v1), 2) / 2);

        // Kinetic energy of m2
        eKin += (decimal)((double)M * (Math.Pow((double)(C[0] * v1), 2) +
            (double)(2 * C[0] * v1 * C[1] * v2) * Math.Cos((double)(u1 - u2)) +
            Math.Pow((double)(C[1] * v2), 2)) / 2);

        // Potential energy of m1 and m2
        var ePot = g * (decimal)((1 - (double)C[0] * Math.Cos((double)u1)) * (double)(1 + M) -
            (double)(M * C[1]) * Math.Cos((double)u2));

        return eKin + ePot;
    }

    protected override decimal G1(NTuple x)
    {
        var deltaU = x[0] - x[2];
        var cos = Math.Cos((double)deltaU);

        // L2*v2^2 + L1*cos(deltaU)*v1^2
        var temp = (decimal)((double)C[1] * Math.Pow((double)x[3], 2) +
            (double)C[0] * cos * Math.Pow((double)x[1], 2));

        // * -Mu * sin(deltaU)
        temp *= -Mu * (decimal)Math.Sin((double)deltaU);

        // + g * (Mu*cos(deltaU)*sin(u2) - sin(u1))
        temp += g * (decimal)((double)Mu * cos * Math.Sin((double)x[2]) - Math.Sin((double)x[0]));

        // / L1*(1 - Mu*cos^2(deltaU))
        temp /= C[0] * (1 - Mu * (decimal)Math.Pow(cos, 2));

        return temp;
    }

    protected override decimal G2(NTuple x)
    {
        var deltaU = x[0] - x[2];
        var cos = Math.Cos((double)deltaU);

        // L1*v1^2 + Mu*L2*cos(deltaU)*v2^2
        var temp = (decimal)((double)C[0] * Math.Pow((double)x[1], 2) +
            (double)(Mu * C[1]) * cos * Math.Pow((double)x[3], 2));

        temp *= (decimal)Math.Sin((double)deltaU);

        // + g * (cos(deltaU)*sin(u1) - sin(u2))
        temp += g * (decimal)(cos * Math.Sin((double)x[0]) - Math.Sin((double)x[2]));

        // / L2*(1 - Mu*cos^2(deltaU))
        temp /= C[1] * (1 - Mu * (decimal)Math.Pow(cos, 2));

        return temp;
    }

    // Test mode: two independent thread pendulums.
    protected override decimal G1Test(NTuple x) => -g * (decimal)(Math.Sin((double)x[0]) / (double)C[0]);

    protected override decimal G2Test(NTuple x) => -g * (decimal)(Math.Sin((double)x[2]) / (double)C[1]);
}
