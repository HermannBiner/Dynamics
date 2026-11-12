using System.Globalization;
using Dynamics.Core.General;

namespace Dynamics.Core.Pendulum;

/// <summary>Energy of the main pendulum compared with the start energy (VB: colour of the energy bar).</summary>
public enum EnergyState
{
    Ok,
    TooHigh,
    TooLow,
}

/// <summary>
/// UI-independent part of ClsPendulumController: selection of the pendulum, shadow pendulum,
/// start parameters, iteration and energy control. The UI reads the state and draws.
/// </summary>
public class PendulumController
{
    private const double EnergyTolerance = 0.01;
    private const double DeltaShadowDS = 0.005;

    /// <summary>Default of the step-width track bar (1..20).</summary>
    public const int DefaultStepWidth = 10;

    public PendulumController()
    {
        // VB found the pendulums by reflection, so own classes appear automatically.
        PendulumTypes = typeof(IPendulum).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IPendulum).IsAssignableFrom(t))
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ToList();
        SelectPendulum(0);
    }

    public IReadOnlyList<Type> PendulumTypes { get; }

    public IPendulum MainDS { get; private set; } = null!;

    /// <summary>Copy of the main pendulum with slightly changed start values, or null.</summary>
    public IPendulum? ShadowDS { get; private set; }

    public IterationStatus Status { get; private set; }

    /// <summary>Number of steps.</summary>
    public int N { get; private set; }

    public double StartEnergy { get; private set; }

    public int StepWidth { get; private set; } = DefaultStepWidth;

    public PhasePortraitType PhasePortraitType => MainDS.PhasePortraitType;

    public bool IsProtocol { get; set; }

    public bool IsMouseDown { get; private set; }

    /// <summary>Display names of the pendulums.</summary>
    public IEnumerable<string> PendulumNames => PendulumTypes.Select(t => Text.GetClassName(Create(t).ClassName));

    /// <summary>Display names of the phase-portrait types.</summary>
    public static IEnumerable<string> PhasePortraitNames => Enum.GetNames<PhasePortraitType>().Select(Text.Get);

    /// <summary>Track-bar range of the additional parameter.</summary>
    public (int Min, int Max) AdditionalParameterRange =>
        ((int)MainDS.AdditionalParameter.Range.A, (int)MainDS.AdditionalParameter.Range.B);

    public string AdditionalParameterLabel =>
        $"{MainDS.AdditionalParameter.Name}: {MainDS.GetAddParameterValue(MainDS.AdditionalParameterValue)}";

    public string StepWidthLabel => $"{Text.Get("StepWidth")}: {MainDS.StepWidthValue}";

    public bool IsEditable => Status is IterationStatus.Stopped;

    private static IPendulum Create(Type type) => (IPendulum)Activator.CreateInstance(type)!;

    /// <summary>VB: SetDS + InitializeMe + SetDefaultUserData + ResetIteration.</summary>
    public void SelectPendulum(int index)
    {
        MainDS = Create(PendulumTypes[index]);
        MainDS.IsMainDS = true;
        MainDS.StepWidth = StepWidth;
        MainDS.PhasePortraitType = PhasePortraitType.Independent;
        SetDefaultUserData();
        ResetIteration();
    }

    public void ResetIteration()
    {
        MainDS.ResetIteration();
        ShadowDS = null;
        N = 0;
        StartEnergy = 0;
        IsMouseDown = false;
        Status = IterationStatus.Stopped;
    }

    public void SetDefaultUserData()
    {
        MainDS.SetDefaultUserData();
        MainDS.SetAdditionalParameters();
    }

    /// <summary>VB: SetTrbAdditionalParameter.</summary>
    public void SetAdditionalParameter(int trackBarValue)
    {
        MainDS.AdditionalParameterValue = trackBarValue;

        // This also sets the additional parameters and resets the main pendulum.
        MainDS.SetDefaultUserData();
        ShadowDS = null;
    }

    public void SetPhasePortraitType(PhasePortraitType type)
    {
        MainDS.PhasePortraitType = type;
        if (ShadowDS is not null)
        {
            ShadowDS.PhasePortraitType = type;
        }
    }

    public void SetStepWidth(int stepWidth)
    {
        StepWidth = stepWidth;
        MainDS.StepWidth = stepWidth;
        if (ShadowDS is not null)
        {
            ShadowDS.StepWidth = stepWidth;
        }
    }

    /// <summary>Start parameters P1..Pn: constants first, then variables (VB: TxtP1..TxtP6).</summary>
    public IReadOnlyList<double> GetStartParameterValues()
    {
        var values = new List<double>();
        if (MainDS.CalculationConstants is { } constants)
        {
            for (var i = 0; i <= constants.Dimension; i++)
            {
                values.Add(constants[i]);
            }
        }

        var variables = MainDS.CalculationVariables;
        for (var i = 0; i <= variables.Dimension; i++)
        {
            values.Add(variables[i]);
        }

        return values;
    }

    /// <summary>Parses a parameter text (current culture or invariant) and checks its range (VB: ClsCheckUserData).</summary>
    public bool TryParseParameter(int index, string text, out double value)
    {
        var ok = double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) ||
                 double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        return ok && MainDS.ValueParameterDefinition[index].Range.IsNumberInInterval(value);
    }

    /// <summary>VB: TakeOverStartParameter. Returns false if a value is invalid (nothing is changed then).</summary>
    public bool TakeOverStartParameter(IReadOnlyList<string> texts)
    {
        var values = new double[MainDS.ValueParameterDefinition.Count];
        for (var i = 0; i < values.Length; i++)
        {
            if (i >= texts.Count || !TryParseParameter(i, texts[i], out values[i]))
            {
                return false;
            }
        }

        ShadowDS = null;

        var next = 0;
        if (MainDS.CalculationConstants is { } constants)
        {
            var tuple = new NTuple(constants.Dimension);
            for (var i = 0; i <= constants.Dimension; i++)
            {
                tuple[i] = values[next++];
            }
            MainDS.CalculationConstants = tuple;
        }

        var variables = new NTuple(MainDS.CalculationVariables.Dimension);
        for (var i = 0; i <= variables.Dimension; i++)
        {
            variables[i] = values[next++];
        }
        MainDS.CalculationVariables = variables;
        MainDS.IsStartparameter1Set = true;
        MainDS.IsStartparameter2Set = true;
        return true;
    }

    /// <summary>
    /// VB: CreateOrRemovePendulum. Its ResetIteration already removes an existing shadow,
    /// so a new shadow is always created; it is removed by resetting the iteration.
    /// </summary>
    public void CreateShadow()
    {
        ResetIteration();

        // The position of the main pendulum is fixed now.
        MainDS.IsStartparameter1Set = true;
        MainDS.IsStartparameter2Set = true;

        var shadow = MainDS.GetCopy();
        shadow.IsMainDS = false;
        shadow.StepWidth = StepWidth;
        shadow.AdditionalParameterValue = MainDS.AdditionalParameterValue;
        shadow.PhasePortraitType = MainDS.PhasePortraitType;

        if (MainDS.CalculationConstants is { } constants)
        {
            shadow.CalculationConstants = constants;
        }

        var variables = MainDS.CalculationVariables;
        for (var i = 0; i <= variables.Dimension; i++)
        {
            variables[i] += DeltaShadowDS;
        }
        shadow.CalculationVariables = variables;
        shadow.IsStartparameter1Set = true;
        shadow.IsStartparameter2Set = true;
        shadow.SetAdditionalParameters();

        ShadowDS = shadow;
    }

    /// <summary>
    /// VB: StartIteration. Returns true if the iteration may run now
    /// (the UI then calls <see cref="Step"/> until it stops).
    /// </summary>
    public bool Start()
    {
        if (Status == IterationStatus.Stopped)
        {
            if (ShadowDS is not null)
            {
                ShadowDS.IsStartparameter1Set = true;
                ShadowDS.IsStartparameter2Set = true;
                ShadowDS.SetAdditionalParameters();
            }

            MainDS.IsStartparameter1Set = true;
            MainDS.IsStartparameter2Set = true;
            StartEnergy = MainDS.GetEnergy();
            MainDS.SetAdditionalParameters();
            Status = IterationStatus.Ready;
        }

        if (Status is IterationStatus.Ready or IterationStatus.Interrupted)
        {
            Status = IterationStatus.Running;
            return true;
        }

        return false;
    }

    /// <summary>One step of the main and the shadow pendulum.</summary>
    public void Step()
    {
        N++;
        MainDS.IterationStep(false);
        ShadowDS?.IterationStep(false);
    }

    public void Stop()
    {
        if (Status == IterationStatus.Running)
        {
            Status = IterationStatus.Interrupted;
        }
    }

    /// <summary>Energy state and position (0..1) of the energy bar.</summary>
    public (EnergyState State, double Fraction) GetEnergyBar()
    {
        var energy = MainDS.GetEnergy();
        var range = MainDS.StartEnergyRange!;
        var tolerance = EnergyTolerance * range.IntervalWidth;

        var state = energy > StartEnergy + tolerance ? EnergyState.TooHigh
            : energy < StartEnergy - tolerance ? EnergyState.TooLow
            : EnergyState.Ok;

        var fraction = (double)((energy - range.A) / range.IntervalWidth);
        return (state, Math.Clamp(fraction, 0, 1));
    }

    /// <summary>VB: MouseDown. Returns true if the mouse now moves a start parameter.</summary>
    public bool MouseDown(MathPoint position)
    {
        if (Status == IterationStatus.Stopped &&
            !(MainDS.IsStartparameter1Set && MainDS.IsStartparameter2Set))
        {
            IsMouseDown = true;
            MouseMove(position);
        }

        return IsMouseDown;
    }

    /// <summary>VB: MouseMove. Returns true if a start parameter was changed.</summary>
    public bool MouseMove(MathPoint position)
    {
        if (!IsMouseDown)
        {
            return false;
        }

        if (!MainDS.IsStartparameter1Set)
        {
            MainDS.SetStartparameter1(position);
        }
        else if (!MainDS.IsStartparameter2Set)
        {
            MainDS.SetStartparameter2(position);
        }

        return true;
    }

    /// <summary>VB: MouseUp. The first start parameter is fixed; the second one is fixed when starting.</summary>
    public void MouseUp()
    {
        if (!IsMouseDown)
        {
            return;
        }

        if (!MainDS.IsStartparameter1Set)
        {
            MainDS.IsStartparameter1Set = true;
        }

        IsMouseDown = false;
    }
}
