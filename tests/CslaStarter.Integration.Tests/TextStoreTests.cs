using CslaStarter.Data.Platform.Texts;
using CslaStarter.Domain.Platform.Texts;
using Microsoft.Extensions.DependencyInjection;

namespace CslaStarter.Integration.Tests;

public sealed class TextStoreTests(PostgresFixture postgres) : IAsyncLifetime
{
    private TestDatabase _db = null!;
    private TextSeeder _seeder = null!;
    private ITextDal _text = null!;

    public async ValueTask InitializeAsync()
    {
        _db = await TestDatabase.CreateAsync(postgres);
        _seeder = _db.Services.GetRequiredService<TextSeeder>();
        _text = _db.Services.GetRequiredService<ITextDal>();
    }

    public ValueTask DisposeAsync() => _db.DisposeAsync();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Seeder_InsertsMissingRows_And_NeverOverwrites()
    {
        await _seeder.SeedAsync(Ct);
        await _db.ExecuteAsync("update error_message_type set en_desc = 'Edited', fr_desc = 'Modifié' where code = 1");
        await _db.ExecuteAsync("delete from error_message_type where code = 2");
        await _seeder.SeedAsync(Ct);

        var messages = await _text.GetErrorMessagesAsync(Ct);
        var required = messages.Single(m => m.Code == ErrorMessageType.Required);
        Assert.Equal(("Edited", "Modifié"), (required.EnDesc, required.FrDesc));
        var maxLength = messages.Single(m => m.Code == ErrorMessageType.MaxLength);
        Assert.Equal(("{0} must be {1} characters or fewer.", "Le champ {0} ne doit pas dépasser {1} caractères.", true),
            (maxLength.EnDesc, maxLength.FrDesc, maxLength.IsActive));
    }

    // The shipped translations.json is empty, so this seeds its own rows through the seeder's internal overload.
    [Fact]
    public async Task Seeder_InsertsTranslationRows_And_KeepsAnEditedOne()
    {
        await _db.ExecuteAsync("insert into translation_type (source_string, en_desc, fr_desc, is_active) values ('Save', 'Save now', 'Sauvegarder', true)");

        await _seeder.SeedAsync([new("Save", "Save", "Enregistrer"), new("Cancel", "Cancel", "Annuler")], [], Ct);

        var rows = await _text.GetTranslationsAsync(Ct);
        var save = rows.Single(t => t.SourceString == "Save");
        Assert.Equal(("Save now", "Sauvegarder"), (save.EnDesc, save.FrDesc));
        var cancel = rows.Single(t => t.SourceString == "Cancel");
        Assert.Equal(("Cancel", "Annuler", true), (cancel.EnDesc, cancel.FrDesc, cancel.IsActive));
    }

    [Fact]
    public async Task AddTranslation_Twice_KeepsOneRow_MarkedInFrench()
    {
        await _text.AddTranslationAsync("Brand new", Ct); await _text.AddTranslationAsync("Brand new", Ct);
        var row = (await _text.GetTranslationsAsync(Ct)).Single(t => t.SourceString == "Brand new");
        Assert.Equal(("Brand new", "fr-Brand new", true), (row.EnDesc, row.FrDesc, row.IsActive));
    }
}
