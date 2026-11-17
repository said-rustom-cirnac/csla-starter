using System.Globalization;

namespace CslaStarter.Domain.Platform.Texts;

/// <summary>The entry point for interface text. Code names a text by its English source string or an error-message code.</summary>
public static class Text
{
    private static volatile ITranslator _current = new SourceTextTranslator();

    public static ITranslator Current => _current;

    /// <summary>Installs the translator for the whole process. Tests never call this: they run in parallel.</summary>
    public static void Use(ITranslator translator)
    {
        ArgumentNullException.ThrowIfNull(translator);
        _current = translator;
    }

    public static string T(string source, params object?[] args) =>
        Apply(Current.Translate(source, CultureInfo.CurrentUICulture), args);

    public static string Error(int code, params object?[] args) =>
        Apply(Current.ErrorMessage(code, CultureInfo.CurrentUICulture), args);

    private static string Apply(string template, object?[] args) =>
        args.Length > 0 ? string.Format(CultureInfo.CurrentCulture, template, args) : template;
}
