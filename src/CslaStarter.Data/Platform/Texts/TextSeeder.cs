using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace CslaStarter.Data.Platform.Texts;

/// <summary>
/// Inserts the shipped text rows (<c>Seed/translations.json</c> and <c>Seed/error-messages.json</c>, compiled into this
/// assembly) that the database lacks, in one short transaction. It never changes a row that exists, so text edited in
/// an environment is kept. A platform store: it uses its own short-lived context.
/// </summary>
public sealed class TextSeeder(IDbContextFactory<AppDbContext> contexts)
{
    private const string TranslationsResource = "translations.json";
    private const string ErrorMessagesResource = "error-messages.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task SeedAsync(CancellationToken ct)
    {
        var translations = Read<TranslationSeed>(TranslationsResource);
        var messages = Read<ErrorMessageSeed>(ErrorMessagesResource);

        var sources = translations.Select(t => t.Source).ToArray();
        var translationsEn = translations.Select(t => t.En).ToArray();
        var translationsFr = translations.Select(t => t.Fr).ToArray();
        var codes = messages.Select(m => m.Code).ToArray();
        var messagesEn = messages.Select(m => m.En).ToArray();
        var messagesFr = messages.Select(m => m.Fr).ToArray();

        await using var db = await contexts.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlAsync(
            $"""
            insert into translation_type (source_string, en_desc, fr_desc, is_active)
            select source, en, fr, true from unnest({sources}, {translationsEn}, {translationsFr}) as seed (source, en, fr)
            on conflict (source_string) do nothing
            """,
            ct);
        await db.Database.ExecuteSqlAsync(
            $"""
            insert into error_message_type (code, en_desc, fr_desc, is_active)
            select code, en, fr, true from unnest({codes}, {messagesEn}, {messagesFr}) as seed (code, en, fr)
            on conflict (code) do nothing
            """,
            ct);
        await transaction.CommitAsync(ct);
    }

    private static List<T> Read<T>(string resource)
    {
        using var stream = typeof(TextSeeder).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Embedded resource {resource} is missing.");
        return JsonSerializer.Deserialize<List<T>>(stream, JsonOptions) ?? [];
    }

    private sealed record TranslationSeed(string Source, string En, string Fr);

    private sealed record ErrorMessageSeed(int Code, string En, string Fr);
}
