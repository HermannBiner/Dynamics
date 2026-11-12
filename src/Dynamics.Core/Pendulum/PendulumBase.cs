using Dynamics.Core.General;

namespace Dynamics.Core.Pendulum;

/// <summary>
/// Parameters and Runge-Kutta frame common to all pendulums. Port of ClsPendulumAbstract.
/// VB mixed Decimal and Double; here everything is Double (step 6 of the port).
/// </summary>
public abstract class PendulumBase : IPendulum
{
    protected const double ShrinkFactor = 0.95;
    protected const double StepWidthUnit = 0.0005;

    /// <summary>Gravitational acceleration.</summary>
    protected const double g = 9.81;

    // The diagram is a square: MathInterval is used for X and Y.
    protected readonly Interval MathInterval = new(-1.0, 1.0);
    protected readonly Interval ShrinkedInterval;

    protected List<GeneralParameter> MyValueParameterDefinition = [];
    protected GeneralParameter MyAdditionalParameter = null!;
    protected NTuple? MyCalculationConstants;
    protected NTuple MyCalculationVariables = null!;
    protected Interval? MyStartEnergyRange;

    protected string MyLabelPhasePortrait = "";
    protected string MyLabelProtocol = "";
    protected Interval MyUInterval = null!;
    protected Interval MyVInterval = null!;
    protected PhasePortraitType MyPhasePortraitType;

    /// <summary>Internal step width.</summary>
    protected double d;

    protected double MyY0;

    // Positions of the pendulums
    protected readonly MathPoint Position1 = new();
    protected readonly MathPoint Position2 = new();
    protected readonly MathPoint OldPosition1 = new();
    protected readonly MathPoint OldPosition2 = new();

    // Runge-Kutta state
    protected double u1;
    protected double v1;
    protected double u2;
    protected double v2;

    protected bool MyIsSignumChanged;

    // Runge-Kutta intermediate values; NTuple(3) has the components 0..3 (k, h: step i in component i).
    protected readonly NTuple x = new(3);
    protected readonly NTuple k1 = new(3);
    protected readonly NTuple h1 = new(3);
    protected readonly NTuple k2 = new(3);
    protected readonly NTuple h2 = new(3);

    protected PendulumBase()
    {
        ShrinkedInterval = new Interval(MathInterval.A * ShrinkFactor, MathInterval.B * ShrinkFactor);
    }

    public abstract string ClassName { get; }

    public bool IsMainDS { get; set; }

    public PhasePortraitType PhasePortraitType
    {
        get => MyPhasePortraitType;
        set
        {
            MyPhasePortraitType = value;
            SetPhasePortraitParameters();
        }
    }

    public string LabelPhasePortrait => MyLabelPhasePortrait;
    public Interval UInterval => MyUInterval;
    public Interval VInterval => MyVInterval;
    public string LabelProtocol => MyLabelProtocol;

    public GeneralParameter AdditionalParameter => MyAdditionalParameter;
    public int AdditionalParameterValue { get; set; }

    public IReadOnlyList<GeneralParameter> ValueParameterDefinition => MyValueParameterDefinition;

    public NTuple? CalculationConstants
    {
        get
        {
            if (MyCalculationConstants is null)
            {
                return null;
            }

            var copy = new NTuple(MyCalculationConstants.Dimension);
            for (var i = 0; i <= MyCalculationConstants.Dimension; i++)
            {
                copy[i] = MyCalculationConstants[i];
            }
            return copy;
        }
        set
        {
            // Some pendulums have no constants.
            if (MyCalculationConstants is null || value is null)
            {
                return;
            }

            for (var i = 0; i <= MyCalculationConstants.Dimension; i++)
            {
                MyCalculationConstants[i] = value[i];
                SetPosition();
            }
        }
    }

    public NTuple CalculationVariables
    {
        get
        {
            var copy = new NTuple(1);
            copy[0] = MyCalculationVariables[0];
            copy[1] = MyCalculationVariables[1];
            return copy;
        }
        set
        {
            // The Runge-Kutta method is designed for exactly two variables.
            MyCalculationVariables[0] = value[0];
            MyCalculationVariables[1] = value[1];
            u1 = value[0];
            v1 = 0;
            u2 = value[1];
            v2 = 0;
            SetPosition();
        }
    }

    public bool IsStartparameter1Set { get; set; }
    public bool IsStartparameter2Set { get; set; }

    public int StepWidth
    {
        set => d = value * StepWidthUnit;
    }

    public double StepWidthValue => d;

    public Interval? StartEnergyRange => MyStartEnergyRange;

    public (double U1, double V1, double U2, double V2) State => (u1, v1, u2, v2);

    public bool IsSignumChanged => MyIsSignumChanged;

    public double Y0 => MyY0;

    public IterationStatus IterationStatus { get; protected set; }

    public void ResetIteration()
    {
        // The UI clears its canvases and protocol itself.
        if (IsMainDS)
        {
            IterationStatus = IterationStatus.Stopped;
            IsStartparameter1Set = false;
            IsStartparameter2Set = false;
        }
    }

    public virtual IReadOnlyList<MathPoint> GetBackgroundCurve() => [];

    public virtual IReadOnlyList<PhasePoint> GetPhasePortraitPoints() => MyPhasePortraitType switch
    {
        PhasePortraitType.TorusOrCylinder => [new PhasePoint(new MathPoint(u1, u2), DrawRole.PendulumColor, 1)],
        PhasePortraitType.PoincareSection => MyIsSignumChanged
            ? [new PhasePoint(new MathPoint(u2, v2), DrawRole.PendulumColor, 2)]
            : [],
        _ =>
        [
            new PhasePoint(new MathPoint(u1, v1), DrawRole.Primary, 1),
            new PhasePoint(new MathPoint(u2, v2), DrawRole.PendulumColor, 1),
        ],
    };

    public (MathPoint From, MathPoint To)? GetPhasePortraitTangent() =>
        MyPhasePortraitType == PhasePortraitType.TorusOrCylinder
            ? (new MathPoint(u1, u2), new MathPoint(u1 + v1 / 10, u2 + v2 / 10))
            : null;

    public string GetProtocolLine() =>
        $"{u1:N11}, {v1:N11}, {u2:N11}, {v2:N11}, {GetEnergy():N11}";

    public abstract IReadOnlyList<PendulumArm> GetArms();
    public abstract (MathPoint From, MathPoint To) GetTrackSegment();
    public abstract double GetAddParameterValue(int trackBarValue);
    public abstract void SetDefaultUserData();
    public abstract void SetAdditionalParameters();
    public abstract void SetStartparameter1(MathPoint position);
    public abstract void SetStartparameter2(MathPoint position);
    public abstract double GetEnergy();
    public abstract void IterationStep(bool isTestMode);
    public abstract IPendulum GetCopy();

    protected abstract void SetPosition();
    protected abstract void SetStartEnergyRange();
    protected abstract void SetPhasePortraitParameters();

    protected void SaveOldPositions()
    {
        OldPosition1.X = Position1.X;
        OldPosition1.Y = Position1.Y;
        OldPosition2.X = Position2.X;
        OldPosition2.Y = Position2.Y;
    }

    // Right-hand sides of the differential equations; u1' = F1, v1' = G1, u2' = F2, v2' = G2.
    protected virtual double F1(NTuple x) => x[1];
    protected virtual double F2(NTuple x) => x[3];
    protected abstract double G1(NTuple x);
    protected abstract double G2(NTuple x);
    protected abstract double G1Test(NTuple x);
    protected abstract double G2Test(NTuple x);

    /// <summary>
    /// One classical Runge-Kutta step for (u1, v1, u2, v2), as in ClsDoublePendulum and ClsCombinedPendulum.
    /// </summary>
    protected void RungeKuttaStep(bool isTestMode)
    {
        x[0] = u1;
        x[1] = v1;
        x[2] = u2;
        x[3] = v2;
        Evaluate(0, isTestMode);

        x[0] = u1 + d * k1[0] / 2;
        x[1] = v1 + d * h1[0] / 2;
        x[2] = u2 + d * k2[0] / 2;
        x[3] = v2 + d * h2[0] / 2;
        Evaluate(1, isTestMode);

        x[0] = u1 + d * k1[1] / 2;
        x[1] = v1 + d * h1[1] / 2;
        x[2] = u2 + d * k2[1] / 2;
        x[3] = v2 + d * h2[1] / 2;
        Evaluate(2, isTestMode);

        x[0] = u1 + d * k1[2];
        x[1] = v1 + d * h1[2];
        x[2] = u2 + d * k2[2];
        x[3] = v2 + d * h2[2];
        Evaluate(3, isTestMode);

        u1 += d * (k1[0] + 2 * k1[1] + 2 * k1[2] + k1[3]) / 6;
        v1 += d * (h1[0] + 2 * h1[1] + 2 * h1[2] + h1[3]) / 6;
        u2 += d * (k2[0] + 2 * k2[1] + 2 * k2[2] + k2[3]) / 6;
        v2 += d * (h2[0] + 2 * h2[1] + 2 * h2[2] + h2[3]) / 6;
        NumericCheck.EnsureFinite(u1, v1, u2, v2);
    }

    private void Evaluate(int step, bool isTestMode)
    {
        k1[step] = F1(x);
        k2[step] = F2(x);
        h1[step] = isTestMode ? G1Test(x) : G1(x);
        h2[step] = isTestMode ? G2Test(x) : G2(x);
    }
}
