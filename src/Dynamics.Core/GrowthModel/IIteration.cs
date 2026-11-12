using Dynamics.Core.General;

namespace Dynamics.Core.GrowthModel;

/// <summary>
/// Iteration of a unimodal function x → F(x) with a steering parameter a (growth models).
/// To program a new unimodal function, just this interface has to be implemented. Port of IIteration.
/// </summary>
public interface IIteration
{
    /// <summary>Definition of the steering parameter a.</summary>
    GeneralParameter DSParameter { get; }

    /// <summary>Value of the steering parameter a.</summary>
    decimal ParameterA { get; set; }

    /// <summary>Definition of the iterated value x.</summary>
    GeneralParameter ValueParameter { get; }

    /// <summary>How many times F is performed in one iteration step: FN = F^Power.</summary>
    int Power { get; set; }

    /// <summary>Split points of the Feigenbaum diagram; the last one is the limit of the period doubling.</summary>
    IReadOnlyList<decimal> Splitpoints { get; }

    /// <summary>Critical point (start value of the Feigenbaum diagram).</summary>
    decimal CriticalPoint { get; }

    /// <summary>The parameter value with chaotic behaviour.</summary>
    decimal ChaoticParameterValue { get; }

    /// <summary>Start value that produces the given protocol (in case of chaos); see the mathematical documentation.</summary>
    decimal CalculateStartValueForProtocol(string targetProtocol);

    /// <summary>Start value near startValue whose orbit approaches the target value (in case of chaos).</summary>
    decimal CalculateStartValueForTargetValue(decimal startValue, decimal targetValue);

    /// <summary>F performed Power times.</summary>
    decimal FN(decimal x);

    /// <summary>
    /// Chaotic behaviour is only guaranteed at the border of the parameter range.
    /// VB showed the message NonChaoticBehaviour itself; here the controllers return it.
    /// </summary>
    bool IsBehaviourChaotic();
}

/// <summary>Base class of the growth models. Port of ClsGrowthModelAbstract.</summary>
public abstract class GrowthModelBase : IIteration
{
    /// <summary>Number of dual digits of the start value kept by CalculateStartValueForTargetValue.</summary>
    public const int StartDigits = 20;

    protected GrowthModelBase()
    {
        InitializeIterator();
    }

    public GeneralParameter DSParameter { get; protected set; } = null!;

    public GeneralParameter ValueParameter { get; protected set; } = null!;

    public virtual decimal ParameterA { get; set; }

    public int Power { get; set; }

    public IReadOnlyList<decimal> Splitpoints { get; protected set; } = [];

    public decimal CriticalPoint { get; protected set; }

    public decimal ChaoticParameterValue { get; protected set; }

    public bool IsBehaviourChaotic()
    {
        var a = ParameterA;
        return !((0 <= a && a < DSParameter.Range.B) || (DSParameter.Range.A < a && a <= 0));
    }

    public decimal FN(decimal x)
    {
        for (var i = 1; i <= Power; i++)
        {
            x = F(x);
        }

        return x;
    }

    public virtual decimal CalculateStartValueForProtocol(string targetProtocol)
    {
        // First a dual start value of the tent map that generates the protocol, then the conjugation
        var binaryTentmapStartvalue = MathHelperUnimodal.ProtocolToTentmapStartValueAsString(targetProtocol);
        var decimalTentmapStartvalue = MathHelperUnimodal.DualStringToDecimalNumber(binaryTentmapStartvalue, true);
        return TentmapToIteration(decimalTentmapStartvalue);
    }

    public virtual decimal CalculateStartValueForTargetValue(decimal startValue, decimal targetValue)
    {
        // Conjugated start and target value of the tent map
        var tentmapDecimalStartvalue = IterationToTentmap(startValue);
        var tentmapDecimalTargetvalue = IterationToTentmap(targetValue);

        var tentmapDualStartvalue = MathHelperUnimodal.DecimalNumberToDualString(tentmapDecimalStartvalue);
        var sign = Math.Sign(tentmapDecimalStartvalue);

        // The adjusting dual string is set behind exactly StartDigits digits of the start value, so the orbit reaches
        // the target after StartDigits steps of F (4 steps of the two-dimensional iteration with power 5).
        // Corrected: VB kept the 21 digits of DecimalNumberToDualString, so with power 5 the target was never hit directly.
        tentmapDualStartvalue = MathHelperUnimodal.FixNumberOfDigitsInDualNumber(tentmapDualStartvalue, StartDigits)[..StartDigits];

        // Corrected: VB did not fill the target up, so the "1" appended below moved a target with a short dual
        // number far away (0.5 = 0.1₂ became 0.111₂ ≈ 0.78 for the logistic growth).
        var tentmapDualTargetvalue = MathHelperUnimodal.FixNumberOfDigitsInDualNumber(
            MathHelperUnimodal.DecimalNumberToDualString(tentmapDecimalTargetvalue), StartDigits);

        // After k steps of the tent map the remaining digits are complemented if and only if the k-th digit is 1
        // (T(0.1b₂b₃…) = 0.b̄₂b̄₃…, a following 1 undoes it again). Then the target has to be complemented.
        // Corrected: VB complemented for an odd number of ones in the whole start value; for half of the start
        // values the orbit reached the mirrored target (logistic growth 0.1 instead of 0.9, parabola 0.8 instead of −0.8).
        if (tentmapDualStartvalue[^1] == '1')
        {
            tentmapDualTargetvalue = MathHelperUnimodal.ComplementDualString(tentmapDualTargetvalue);
        }

        // The last missing digit minimises the error interval
        tentmapDualTargetvalue += "1";
        tentmapDualStartvalue += tentmapDualTargetvalue;

        var adjustedTentmapStartvalue = MathHelperUnimodal.DualStringToDecimalNumber(tentmapDualStartvalue, true);
        return TentmapToIteration(sign * adjustedTentmapStartvalue);
    }

    protected abstract void InitializeIterator();

    /// <summary>The original iteration function.</summary>
    protected abstract decimal F(decimal x);

    /// <summary>Conjugation to the tent map.</summary>
    protected abstract decimal IterationToTentmap(decimal x);

    /// <summary>Conjugation from the tent map.</summary>
    protected abstract decimal TentmapToIteration(decimal u);
}
