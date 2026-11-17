namespace CslaStarter.Domain.Platform.Texts;

/// <summary>
/// Reads the translation and error-message rows, and adds a translation that code asked for and the database lacks.
/// Each call stands alone, so the translator (a singleton) can use it at any time.
/// </summary>
public interface ITextDal
{
    Task<IReadOnlyList<TranslationDto>> GetTranslationsAsync(CancellationToken ct);

    Task<IReadOnlyList<ErrorMessageDto>> GetErrorMessagesAsync(CancellationToken ct);

    /// <summary>
    /// Adds <paramref name="sourceString"/> with English the source string and French marked <c>fr-</c>, unless a row
    /// for it exists. Safe when several servers add the same string at once.
    /// </summary>
    Task AddTranslationAsync(string sourceString, CancellationToken ct);
}
