using System.Globalization;

namespace CslaStarter.Domain.Platform.Texts;

/// <summary>Turns an English source string or an error-message code into text for a culture.</summary>
public interface ITranslator
{
    string Translate(string source, CultureInfo culture);

    string ErrorMessage(int code, CultureInfo culture);
}
