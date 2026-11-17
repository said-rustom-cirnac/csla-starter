using CslaStarter.Domain.Platform;
using CslaStarter.Domain.Platform.Users;
using Microsoft.Extensions.DependencyInjection;

namespace CslaStarter.Integration.Tests;

public sealed class UserDalTests(PostgresFixture postgres) : IAsyncLifetime
{
    private TestDatabase _db = null!;
    private IUserDal _users = null!;

    public async ValueTask InitializeAsync()
    {
        _db = await TestDatabase.CreateAsync(postgres);
        _users = _db.Services.GetRequiredService<IUserDal>();
    }

    public ValueTask DisposeAsync() => _db.DisposeAsync();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_UnknownUser_ThrowsNotFound() =>
        await Assert.ThrowsAsync<NotFoundException>(() => _users.GetAsync(Guid.CreateVersion7(), Ct));
}
