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
        await _db.ExecuteAsync("update error_message_type set fr_desc = 'Modifié' where code = 1");
        await _seeder.SeedAsync(Ct);
        var required = (await _text.GetErrorMessagesAsync(Ct)).Single(m => m.Code == ErrorMessageType.Required);
        Assert.Equal("{0} is required.", required.EnDesc); Assert.Equal("Modifié", required.FrDesc);
    }

    [Fact]
    public async Task AddTranslation_Twice_KeepsOneRow_MarkedInFrench()
    {
        await _text.AddTranslationAsync("Brand new", Ct); await _text.AddTranslationAsync("Brand new", Ct);
        var row = (await _text.GetTranslationsAsync(Ct)).Single(t => t.SourceString == "Brand new");
        Assert.Equal(("Brand new", "fr-Brand new", true), (row.EnDesc, row.FrDesc, row.IsActive));
    }
}
