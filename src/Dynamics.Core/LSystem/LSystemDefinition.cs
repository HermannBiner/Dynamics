namespace Dynamics.Core.LSystem;

/// <summary>Type of an L-system (VB ClsLSystem.EnLSystemType).</summary>
public enum LSystemType
{
    /// <summary>Without a tree structure.</summary>
    Standard,

    /// <summary>With a tree structure; option: budding.</summary>
    Tree,

    /// <summary>Dragon curve; option: extended (four turtles).</summary>
    Dragon,
}

/// <summary>How the step length shrinks (VB ClsLSystemsController.EnScaling).</summary>
public enum LSystemScaling
{
    /// <summary>The step length is multiplied by the scale factor for every generation.</summary>
    ByIteration,

    /// <summary>The step length is multiplied by the scale factor for every branch "[".</summary>
    ByBranch,
}

/// <summary>
/// Definition of an L-system. Port of ClsLSystem; the property names are those of the JSON file of Simulator
/// (LSystemsEN.json / LSystemsDE.json), enums as numbers.
/// </summary>
public sealed class LSystemDefinition
{
    public int ID { get; set; }
    public string Name { get; set; } = "";
    public LSystemType Type { get; set; }
    public string Axiom { get; set; } = "";
    public decimal ScaleFactor { get; set; }

    /// <summary>Angles in degrees.</summary>
    public int AngleLeft { get; set; }

    public int AngleRight { get; set; }

    /// <summary>Index in <see cref="LSystemColors"/>.</summary>
    public int Color { get; set; }

    /// <summary>Dragon curve with four turtles.</summary>
    public bool IsExtended { get; set; }

    public bool IsColorFixed { get; set; }

    /// <summary>Before a branch ends with "]", a bud is drawn.</summary>
    public bool IsBudding { get; set; }

    public decimal StartLength { get; set; }
    public LSystemScaling Scaling { get; set; }
    public decimal StartPositionX { get; set; }
    public decimal StartPositionY { get; set; }
    public Dictionary<string, string> Rules { get; set; } = [];

    public LSystemDefinition Clone()
    {
        var copy = (LSystemDefinition)MemberwiseClone();
        copy.Rules = new Dictionary<string, string>(Rules);
        return copy;
    }
}

/// <summary>The 26 colors of the L-systems; the number is the color code in "(n)". Port of ClsColorList.</summary>
public static class LSystemColors
{
    public static readonly IReadOnlyList<(string Name, int Argb)> Colors =
    [
        ("Black", unchecked((int)0xFF000000)),
        ("Brown", unchecked((int)0xFFA52A2A)),
        ("Red", unchecked((int)0xFFFF0000)),
        ("Tomato", unchecked((int)0xFFFF6347)),
        ("BurlyWood", unchecked((int)0xFFDEB887)),
        ("Orange", unchecked((int)0xFFFFA500)),
        ("Goldenrod", unchecked((int)0xFFDAA520)),
        ("Gold", unchecked((int)0xFFFFD700)),
        ("Olive", unchecked((int)0xFF808000)),
        ("Yellow", unchecked((int)0xFFFFFF00)),
        ("OliveDrab", unchecked((int)0xFF6B8E23)),
        ("GreenYellow", unchecked((int)0xFFADFF2F)),
        ("ForestGreen", unchecked((int)0xFF228B22)),
        ("Lime", unchecked((int)0xFF00FF00)),
        ("Turquoise", unchecked((int)0xFF40E0D0)),
        ("Aqua", unchecked((int)0xFF00FFFF)),
        ("DeepSkyBlue", unchecked((int)0xFF00BFFF)),
        ("SteelBlue", unchecked((int)0xFF4682B4)),
        ("DodgerBlue", unchecked((int)0xFF1E90FF)),
        ("Navy", unchecked((int)0xFF000080)),
        ("Blue", unchecked((int)0xFF0000FF)),
        ("BlueViolet", unchecked((int)0xFF8A2BE2)),
        ("Violet", unchecked((int)0xFFEE82EE)),
        ("Purple", unchecked((int)0xFF800080)),
        ("Magenta", unchecked((int)0xFFFF00FF)),
        ("DeepPink", unchecked((int)0xFFFF1493)),
    ];

    public static int MaxCode => Colors.Count - 1;
}

/// <summary>Replaces every character with a rule by its target. Port of ClsLSystemExpander.</summary>
public sealed class LSystemExpander(Dictionary<string, string> rules)
{
    public string Expand(string itinerary)
    {
        var next = new System.Text.StringBuilder(itinerary.Length * 2);
        foreach (var c in itinerary)
        {
            if (rules.TryGetValue(c.ToString(), out var target))
            {
                next.Append(target);
            }
            else
            {
                next.Append(c);
            }
        }

        return next.ToString();
    }
}
