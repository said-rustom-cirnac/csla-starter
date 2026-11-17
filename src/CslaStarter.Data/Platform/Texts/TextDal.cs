using CslaStarter.Domain.Platform.Texts;
using Microsoft.EntityFrameworkCore;

namespace CslaStarter.Data.Platform.Texts;

/// <summary>
/// The text rows, a platform store: each call uses its own short-lived context and depends only on the context
/// factory, so the translator (a singleton) can hold it.
/// </summary>
public sealed class TextDal(IDbContextFactory<AppDbContext> contexts) : ITextDal
{
    public async Task<IReadOnlyList<TranslationDto>> GetTranslationsAsync(CancellationToken ct)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        return await db.Translations.AsNoTracking()
            .OrderBy(t => t.Code)
            .Select(t => new TranslationDto(t.Code, t.SourceString, t.EnDesc, t.FrDesc, t.IsActive))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ErrorMessageDto>> GetErrorMessagesAsync(CancellationToken ct)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        return await db.ErrorMessages.AsNoTracking()
            .OrderBy(e => e.Code)
            .Select(e => new ErrorMessageDto(e.Code, e.EnDesc, e.FrDesc, e.IsActive))
            .ToListAsync(ct);
    }

    public async Task AddTranslationAsync(string sourceString, CancellationToken ct)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        await db.Database.ExecuteSqlAsync(
            $"""
            insert into translation_type (source_string, en_desc, fr_desc, is_active)
            values ({sourceString}, {sourceString}, {"fr-" + sourceString}, true)
            on conflict (source_string) do nothing
            """,
            ct);
    }
}
