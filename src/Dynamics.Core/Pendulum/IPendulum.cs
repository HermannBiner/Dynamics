using Dynamics.Core.General;

namespace Dynamics.Core.Pendulum;

/// <summary>Type of the phase portrait.</summary>
public enum PhasePortraitType
{
    /// <summary>Both coordinates are shown independently.</summary>
    Independent,

    /// <summary>The two coordinates are shown on a torus or cylinder.</summary>
    TorusOrCylinder,

    /// <summary>Poincaré section, see the mathematical documentation.</summary>
    PoincareSection,
}

/// <summary>Colour role of a drawn element; the UI maps it to a colour.</summary>
public enum DrawRole
{
    /// <summary>Fixed blue in Simulator (upper pendulum, first phase coordinate).</summary>
    Primary,

    /// <summary>Colour of the pendulum: red for the main pendulum, green for the shadow pendulum.</summary>
    PendulumColor,
}

/// <summary>A pendulum arm from a suspension point to a bob.</summary>
public readonly record struct PendulumArm(MathPoint From, MathPoint To, double BobRadius, DrawRole Role);

/// <summary>A point of the phase portrait; points accumulate until the iteration is reset.</summary>
public readonly record struct PhasePoint(MathPoint Point, DrawRole Role, int Size);

/// <summary>
/// A pendulum knows all rules of its movement. Unlike Simulator's IPendulum it does not draw:
/// it exposes its state in mathematical coordinates and the UI draws it.
/// </summary>
public interface IPendulum
{
    /// <summary>Name of the VB class, used as resource key for the display name.</summary>
    string ClassName { get; }

    /// <summary>True for the pendulum the user controls; false for the shadow pendulum.</summary>
    bool IsMainDS { get; set; }

    PhasePortraitType PhasePortraitType { get; set; }
    string LabelPhasePortrait { get; }
    Interval UInterval { get; }
    Interval VInterval { get; }

    string LabelProtocol { get; }

    /// <summary>Parameter of the track bar "additional parameter".</summary>
    GeneralParameter AdditionalParameter { get; }

    /// <summary>Value of the track bar "additional parameter".</summary>
    int AdditionalParameterValue { get; set; }

    /// <summary>Labels and ranges of the fields P1..Pn.</summary>
    IReadOnlyList<GeneralParameter> ValueParameterDefinition { get; }

    /// <summary>Constants (copy); null if the pendulum has none.</summary>
    NTuple? CalculationConstants { get; set; }

    /// <summary>Variables (copy); setting them resets the velocities.</summary>
    NTuple CalculationVariables { get; set; }

    bool IsStartparameter1Set { get; set; }
    bool IsStartparameter2Set { get; set; }

    /// <summary>Step width in units of 0.0005 (track bar value).</summary>
    int StepWidth { set; }

    /// <summary>Internal Runge-Kutta step width d.</summary>
    double StepWidthValue { get; }

    Interval? StartEnergyRange { get; }

    /// <summary>Runge-Kutta state (u1, v1, u2, v2).</summary>
    (double U1, double V1, double U2, double V2) State { get; }

    /// <summary>True if the Poincaré section was crossed in the last step.</summary>
    bool IsSignumChanged { get; }

    /// <summary>Y-coordinate of the suspension line.</summary>
    double Y0 { get; }

    /// <summary>Current arms and bobs.</summary>
    IReadOnlyList<PendulumArm> GetArms();

    /// <summary>Track of the last step (from old to new position of the traced bob).</summary>
    (MathPoint From, MathPoint To) GetTrackSegment();

    /// <summary>Phase-portrait output of the last step.</summary>
    IReadOnlyList<PhasePoint> GetPhasePortraitPoints();

    /// <summary>Tangent of the torus/cylinder phase portrait (redrawn each step), or null.</summary>
    (MathPoint From, MathPoint To)? GetPhasePortraitTangent();

    /// <summary>Additional background curve (e.g. equilibrium line), in mathematical coordinates.</summary>
    IReadOnlyList<MathPoint> GetBackgroundCurve();

    double GetAddParameterValue(int trackBarValue);

    void SetDefaultUserData();

    void SetAdditionalParameters();

    void ResetIteration();

    /// <summary>Sets the first start parameter from a point in mathematical coordinates (VB: mouse position).</summary>
    void SetStartparameter1(MathPoint position);

    /// <summary>Sets the second start parameter from a point in mathematical coordinates.</summary>
    void SetStartparameter2(MathPoint position);

    double GetEnergy();

    void IterationStep(bool isTestMode);

    /// <summary>Line for the protocol list: u1, v1, u2, v2, energy.</summary>
    string GetProtocolLine();

    IPendulum GetCopy();
}
