using System.Globalization;

namespace CslaStarter.Domain.Platform.Texts;

/// <summary>The default translator: English is the source string, French is the source marked "fr-", and error messages come from the seed file.</summary>
public sealed class SourceTextTranslator : ITranslator
{
    public string Translate(string source, CultureInfo culture) =>
        culture.TwoLetterISOLanguageName == "fr" ? "fr-" + source : source;

    public string ErrorMessage(int code, CultureInfo culture) => ErrorMessageFallback.Get(code, culture);
}
