using CslaStarter.Core.Security;
using CslaStarter.Data;
using CslaStarter.Data.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace CslaStarter.Integration.Tests;

public sealed class SecurityEventLogTests(PostgresFixture postgres) : IAsyncLifetime
{
    private TestDatabase _db = null!;

    public async ValueTask InitializeAsync() => _db = await TestDatabase.CreateAsync(postgres);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task SecurityEventLog_WritesRow()
    {
        var at = new DateTimeOffset(2026, 11, 12, 15, 30, 0, TimeSpan.Zero);
        var userId = Guid.CreateVersion7();
        var log = new SecurityEventLog(new AppDatabaseConnection(_db.RuntimeConnectionString), new FakeTimeProvider(at), NullLogger<SecurityEventLog>.Instance);

        await log.WriteAsync(new SecurityEvent(SecurityEventKind.AccessDenied, userId, "Ada", "Edit Project 42", "corr-9"), TestContext.Current.CancellationToken);

        await using var context = _db.CreateContext();
        var row = await context.SecurityEvents.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal((int)SecurityEventKind.AccessDenied, row.Kind);
        Assert.Equal((userId, "Ada", "Edit Project 42", "corr-9", at), (row.UserId, row.UserName, row.Detail, row.CorrelationId, row.OccurredAt));
    }
}
