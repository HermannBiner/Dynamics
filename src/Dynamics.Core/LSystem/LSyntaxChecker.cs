using System.Text.RegularExpressions;
using Dynamics.Core.General;

namespace Dynamics.Core.LSystem;

/// <summary>The input fields of an L-system while it is edited (VB: the text boxes of FrmLSystems).</summary>
public sealed class LSystemForm
{
    public string Name { get; set; } = "";
    public string Axiom { get; set; } = "";
    public string ScaleFactor { get; set; } = "";
    public string AngleLeft { get; set; } = "";
    public string AngleRight { get; set; } = "";
    public int Color { get; set; }
    public string StartLength { get; set; } = "";
    public LSystemScaling Scaling { get; set; }
    public LSystemType Type { get; set; }

    /// <summary>Budding for a tree, extended for a dragon curve.</summary>
    public bool IsOptionChecked { get; set; }

    public string X { get; set; } = "";
    public string Y { get; set; } = "";
}

/// <summary>
/// Checks an L-system before it is saved. Port of ClsLSyntaxChecker; returns the messages instead of showing them.
/// </summary>
public static partial class LSyntaxChecker
{
    public const string AllowedAxiomChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZfg+-";
    public const string AllowedRuleChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZfg+-()[]0123456789";

    /// <summary>
    /// VB: ParameterValuesAreAllowed. Corrected: the start length was checked with the value of the scale factor;
    /// the name was checked for uniqueness only for a new system (now also when it is renamed); the start position
    /// was not checked (VB failed for a non-numeric value).
    /// </summary>
    public static List<string> ParameterValuesAreAllowed(LSystemForm form, IReadOnlyDictionary<string, string> rules,
        IEnumerable<LSystemDefinition> otherSystems)
    {
        var errors = new List<string>();
        if (form.Name == "")
        {
            errors.Add(Text.Get("NameMissing"));
        }
        else if (otherSystems.Any(s => s.Name == form.Name))
        {
            errors.Add(Text.Get("NameAlreadyUsed"));
        }

        if (form.Axiom == "")
        {
            errors.Add(Text.Get("AxiomMissing"));
        }
        else if (form.Axiom.Any(c => !AllowedAxiomChars.Contains(c)))
        {
            errors.Add(Text.Get("AxiomContainsIllegalCharacters"));
        }

        if (!UserDataCheck.TryParse(form.ScaleFactor, out var scaleFactor))
        {
            errors.Add(Text.Get("ScaleFactorMissing"));
        }
        else if (scaleFactor <= 0 || scaleFactor > 1)
        {
            errors.Add(Text.Get("ScaleFactorIn0to1"));
        }

        if (!UserDataCheck.TryParse(form.StartLength, out var startLength))
        {
            errors.Add(Text.Get("StartLengthMissing"));
        }
        else if (startLength <= 0 || startLength > 13)
        {
            errors.Add(Text.Get("InvalidStartLength"));
        }

        if (!UserDataCheck.TryParse(form.AngleLeft, out var left) || !UserDataCheck.TryParse(form.AngleRight, out var right))
        {
            errors.Add(Text.Get("AngleMissing"));
        }
        else
        {
            if (left < 0 || left > 360)
            {
                errors.Add(Text.Get("AngleIn0to360"));
            }

            if (right < 0 || right > 360)
            {
                errors.Add(Text.Get("AngleIn0to360"));
            }
        }

        if (!UserDataCheck.TryParse(form.X, out var x) || !UserDataCheck.TryParse(form.Y, out var y) ||
            !Turtle.MathInterval.IsNumberInInterval(x) || !Turtle.MathInterval.IsNumberInInterval(y))
        {
            errors.Add(Text.Get("StartPositionIn0to13"));
        }

        if (rules.Count == 0)
        {
            errors.Add(Text.Get("NoRulesExisting"));
        }
        else
        {
            foreach (var (source, target) in rules)
            {
                if (source.Any(c => !AllowedRuleChars.Contains(c)))
                {
                    errors.Add(Text.Get("SourceContainsIllegalCharacters"));
                }

                if (target.Any(c => !AllowedRuleChars.Contains(c)))
                {
                    errors.Add(Text.Get("TargetContainsIllegalCharacters"));
                }
            }
        }

        return errors;
    }

    /// <summary>
    /// VB: RulesAreConsistent: a rule for every letter of the axiom and the targets (except the moves), color codes
    /// (0) to (25), balanced brackets. Corrected: VB demanded a rule for G and g, although they only move the turtle.
    /// </summary>
    public static List<string> RulesAreConsistent(string axiom, IReadOnlyDictionary<string, string> rules)
    {
        var errors = new List<string>();
        var allTargetChars = axiom + string.Concat(rules.Values);
        var allSourceChars = string.Concat(rules.Keys);
        foreach (var c in allTargetChars.Distinct())
        {
            if ("+-()[]FfGg".Contains(c) || char.IsDigit(c))
            {
                continue;
            }

            if (!allSourceChars.Contains(c))
            {
                errors.Add(Text.Get("NoRuleForChar") + ": " + c);
            }
        }

        var targets = string.Concat(rules.Values);
        if (targets.Count(c => c == '(') != targets.Count(c => c == ')'))
        {
            errors.Add(Text.Get("BracketsMissing()"));
        }
        else
        {
            foreach (var target in rules.Values)
            {
                foreach (Match match in AllBrackets().Matches(target))
                {
                    var color = ValidColor().Match(match.Value);
                    if (!color.Success)
                    {
                        errors.Add(Text.Get("ColorCodeInvalid"));
                    }
                    else if (int.Parse(color.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) > LSystemColors.MaxCode)
                    {
                        errors.Add(Text.Get("ColorCodeInvalid") + ": " + color.Groups[1].Value);
                    }
                }
            }
        }

        if (targets.Count(c => c == '[') != targets.Count(c => c == ']'))
        {
            errors.Add(Text.Get("BracketsMissing[]"));
        }

        return errors;
    }

    [GeneratedRegex(@"\([^\)]*\)")]
    private static partial Regex AllBrackets();

    [GeneratedRegex(@"^\((\d{1,2})\)$")]
    private static partial Regex ValidColor();
}
