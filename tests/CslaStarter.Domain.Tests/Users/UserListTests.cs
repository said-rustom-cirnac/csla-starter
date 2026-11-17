using CslaStarter.Domain.Platform.Users;

namespace CslaStarter.Domain.Tests.Users;

public sealed class UserListTests : IDisposable
{
    private readonly TestPortal _portal = new();

    public void Dispose() => _portal.Dispose();

    [Fact]
    public async Task List_ReturnsEveryUser_ReadOnly()
    { var list = await _portal.FetchAsync<UserList>(); Assert.Equal(["Ada", "Bea"], list.Select(u => u.DisplayName)); Assert.True(list.IsReadOnly); }
}
