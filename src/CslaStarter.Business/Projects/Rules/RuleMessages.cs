using System.Globalization;

namespace CslaStarter.Business.Projects.Rules;

internal static class RuleMessages
{
    /// <summary>Formats a localized rule message with the property's friendly name (read in the current UI culture).</summary>
    public static string Format(Func<string> message, string friendlyName) =>
        string.Format(CultureInfo.CurrentCulture, message(), friendlyName);
}
