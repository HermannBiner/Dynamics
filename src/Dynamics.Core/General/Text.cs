using System.Globalization;
using System.Resources;

namespace Dynamics.Core.General;

/// <summary>
/// Labels, messages and other texts in the current UI language.
/// Replaces ClsLanguageManager: the language follows CultureInfo.CurrentUICulture
/// (neutral = English, "de" = German) instead of a singleton with its own setting.
/// </summary>
public static class Text
{
    private static readonly ResourceManager Resources =
        new("Dynamics.Core.Resources.Labels", typeof(Text).Assembly);

    /// <summary>Returns the text for the key, or the key itself if there is no text.</summary>
    public static string Get(string key) => Get(key, CultureInfo.CurrentUICulture);

    public static string Get(string key, CultureInfo culture)
    {
        var text = Resources.GetString(key, culture);
        return string.IsNullOrEmpty(text) ? key : text;
    }

    /// <summary>
    /// Display name of a class implementing a dynamic system.
    /// If the resources have no entry, the "Cls" prefix is cut from the class name.
    /// </summary>
    public static string GetClassName(string className)
    {
        var text = Resources.GetString(className, CultureInfo.CurrentUICulture);
        if (!string.IsNullOrEmpty(text))
        {
            return text;
        }

        if (className.StartsWith("Cls", StringComparison.Ordinal) && className.Length > 3)
        {
            return className[3..];
        }

        throw new FormatException(Get("ClassNamingViolation"));
    }
}
