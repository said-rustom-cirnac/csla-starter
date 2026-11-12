using CslaStarter.Core.Security;
using CslaStarter.Data;
using CslaStarter.Infrastructure.DataProtection;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CslaStarter.Integration.Tests;

public sealed class DataProtectionTests(PostgresFixture postgres) : IAsyncLifetime
{
    private TestDatabase _db = null!;

    public async ValueTask InitializeAsync() => _db = await TestDatabase.CreateAsync(postgres);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public void DataProtection_KeysPersistToDatabase()
    {
        using var first = Provider("Development");
        using var second = Provider("Development");

        var protectedText = first.GetRequiredService<IDataProtectionProvider>().CreateProtector("test").Protect("hello");

        Assert.Equal("hello", second.GetRequiredService<IDataProtectionProvider>().CreateProtector("test").Unprotect(protectedText));
    }

    [Fact]
    public void DataProtection_Production_WithoutKeyProtection_Throws()
    {
        var error = Assert.Throws<InvalidOperationException>(() => Provider("Production"));
        Assert.Contains("DataProtection:CertificateThumbprint", error.Message, StringComparison.Ordinal);
    }

    private ServiceProvider Provider(string environment)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<IDataAccessContext>(_ => FakeDataAccessContext.Tester);
        services.AddCslaStarterData(_db.RuntimeConnectionString);
        services.AddCslaStarterDataProtection(new ConfigurationBuilder().Build(), new TestHostEnvironment(environment));
        return services.BuildServiceProvider(validateScopes: true);
    }
}
