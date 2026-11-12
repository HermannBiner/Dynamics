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
    private double M;

    /// <summary>Mu = M / (1 + M).</summary>
    private double Mu;

    private readonly NTuple _pendulumSize = new(1);
    private readonly MathPoint _origin = new(0, 0);

    public DoublePendulum()
    {
        MyLabelProtocol = Text.Get("Parameterlist") + ": u1, v1, u2, v2, Etot";

        _valueParameter[0] = new GeneralParameter(1, "L1", new Interval(0.1, ShrinkedInterval.B),
            GeneralParameter.ParameterType.Constant, 0.7);
        _valueParameter[1] = new GeneralParameter(2, "L2", new Interval(0.1, ShrinkedInterval.B),
            GeneralParameter.ParameterType.Constant, 0.2);
        _valueParameter[2] = new GeneralParameter(3, "Phi 1", new Interval(-Math.PI, Math.PI),
            GeneralParameter.ParameterType.Variable, Math.PI / 4);
        _valueParameter[3] = new GeneralParameter(4, "Phi 2", new Interval(-Math.PI, Math.PI),
            GeneralParameter.ParameterType.Variable, Math.PI / 6);
        MyValueParameterDefinition.AddRange(_valueParameter);

        MyLabelPhasePortrait = Text.Get("PhasePortrait") + ": -- ";

        // Track bar value 2 means m2 = m1.
        AdditionalParameterValue = 2;
        MyAdditionalParameter = new GeneralParameter(AdditionalParameterValue, Text.Get("MassRatioM"),
            new Interval(0.0, 4.0), GeneralParameter.ParameterType.DS);

        // Constants: L1 = [0], L2 = [1]; variables: Phi1 = [0], Phi2 = [1]
        MyCalculationConstants = new NTuple(1);
        MyCalculationVariables = new NTuple(1);

        SetPhasePortraitParameters();
    }

    public override string ClassName => "ClsDoublePendulum";

    private NTuple C => MyCalculationConstants!;

    public override IPendulum GetCopy() => new DoublePendulum();

    public override double GetAddParameterValue(int trackBarValue) => Math.Pow(2, trackBarValue - 2);

    public override IReadOnlyList<PendulumArm> GetArms() =>
    [
        new PendulumArm(_origin, Position1, _pendulumSize[0], DrawRole.Primary),
        new PendulumArm(Position1, Position2, _pendulumSize[1], DrawRole.PendulumColor),
    ];

    // The track of the upper pendulum is a circle and not interesting.
    public override (MathPoint From, MathPoint To) GetTrackSegment() => (OldPosition2, Position2);

    protected override void SetPosition()
    {
        Position1.X = C[0] * Math.Sin(MyCalculationVariables[0]);
        Position1.Y = -C[0] * Math.Cos(MyCalculationVariables[0]);

        Position2.X = Position1.X + C[1] * Math.Sin(MyCalculationVariables[1]);
        Position2.Y = Position1.Y - C[1] * Math.Cos(MyCalculationVariables[1]);
    }

    protected override void SetPhasePortraitParameters()
    {
        var phasePortrait = Text.Get("PhasePortrait");
        switch (MyPhasePortraitType)
        {
            case PhasePortraitType.TorusOrCylinder:
                MyLabelPhasePortrait = phasePortrait + ": Phi1, Phi2. Tangent: Phi1', Phi2'";
                MyUInterval = new Interval(-Math.PI, Math.PI);
                MyVInterval = new Interval(-Math.PI, Math.PI);
                break;
            case PhasePortraitType.PoincareSection:
                MyLabelPhasePortrait = phasePortrait + ": Phi1 = 0, Phi2, Phi2'";
                MyUInterval = new Interval(-Math.PI, Math.PI);
                MyVInterval = new Interval(-10.0, 10.0);
                break;
            default:
                MyLabelPhasePortrait = phasePortrait + ": Blue: Phi1, Phi1'. Red: Phi2, Phi2'";
                MyUInterval = new Interval(-Math.PI, Math.PI);
                MyVInterval = new Interval(-10.0, 10.0);
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
            _pendulumSize[0] = 0.01;
            _pendulumSize[1] = M * _pendulumSize[0];
        }
        else
        {
            _pendulumSize[1] = 0.01;
            _pendulumSize[0] = _pendulumSize[1] / M;
        }
    }

    public override void SetStartparameter1(MathPoint position)
    {
        // L1 at most Range.B to leave room for L2, at least Range.A because of the divisor in the equations.
        var range = MyValueParameterDefinition[0].Range;
        var l1 = Math.Max(range.A,
            Math.Min(Math.Sqrt(position.X * position.X + position.Y * position.Y), range.B));

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
        var l2 = Math.Max(range.A,
            Math.Min(Math.Sqrt(deltaX * deltaX + deltaY * deltaY), 0.95 - C[0]));
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

    public override double GetEnergy()
    {
        // Norm: m1 = 1, m2 = M. Zero level of the potential energy at y = -1.
        // Kinetic energy of m1: L1^2 * v1^2 / 2
        var eKin = Math.Pow(C[0] * v1, 2) / 2;

        // Kinetic energy of m2
        eKin += M * (Math.Pow(C[0] * v1, 2) +
            2 * C[0] * v1 * C[1] * v2 * Math.Cos(u1 - u2) +
            Math.Pow(C[1] * v2, 2)) / 2;

        // Potential energy of m1 and m2
        var ePot = g * ((1 - C[0] * Math.Cos(u1)) * (1 + M) -
            M * C[1] * Math.Cos(u2));

        return eKin + ePot;
    }

    protected override double G1(NTuple x)
    {
        var deltaU = x[0] - x[2];
        var cos = Math.Cos(deltaU);

        // L2*v2^2 + L1*cos(deltaU)*v1^2
        var temp = C[1] * Math.Pow(x[3], 2) +
            C[0] * cos * Math.Pow(x[1], 2);

        // * -Mu * sin(deltaU)
        temp *= -Mu * Math.Sin(deltaU);

        // + g * (Mu*cos(deltaU)*sin(u2) - sin(u1))
        temp += g * (Mu * cos * Math.Sin(x[2]) - Math.Sin(x[0]));

        // / L1*(1 - Mu*cos^2(deltaU))
        temp /= C[0] * (1 - Mu * Math.Pow(cos, 2));

        return temp;
    }

    protected override double G2(NTuple x)
    {
        var deltaU = x[0] - x[2];
        var cos = Math.Cos(deltaU);

        // L1*v1^2 + Mu*L2*cos(deltaU)*v2^2
        var temp = C[0] * Math.Pow(x[1], 2) +
            Mu * C[1] * cos * Math.Pow(x[3], 2);

        temp *= Math.Sin(deltaU);

        // + g * (cos(deltaU)*sin(u1) - sin(u2))
        temp += g * (cos * Math.Sin(x[0]) - Math.Sin(x[2]));

        // / L2*(1 - Mu*cos^2(deltaU))
        temp /= C[1] * (1 - Mu * Math.Pow(cos, 2));

        return temp;
    }

    // Test mode: two independent thread pendulums.
    protected override double G1Test(NTuple x) => -g * (Math.Sin(x[0]) / C[0]);

    protected override double G2Test(NTuple x) => -g * (Math.Sin(x[2]) / C[1]);
}
