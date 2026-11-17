namespace CslaStarter.Integration.Tests;

public sealed class SchemaTests(PostgresFixture postgres) : IAsyncLifetime
{
    private TestDatabase _db = null!;

    public async ValueTask InitializeAsync() => _db = await TestDatabase.CreateAsync(postgres);

    public ValueTask DisposeAsync() => _db.DisposeAsync();

    [Fact]
    public async Task Schema_HasTheSlice1Tables_InSnakeCase() =>
        Assert.Superset(new HashSet<string> { "app_user", "translation_type", "error_message_type" }, await _db.TableNamesAsync());
}
