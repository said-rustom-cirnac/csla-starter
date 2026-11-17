using CslaStarter.Domain.Platform.Users;

namespace CslaStarter.Domain.Tests.Users;

public sealed class UserListTests : IDisposable
{
    private readonly TestPortal _portal = new();

    public void Dispose() => _portal.Dispose();

    [Fact]
    public async Task List_ReturnsEveryUser_ReadOnly()
    {
        var list = await _portal.FetchAsync<UserList>();
        Assert.Equal([FakeUserDal.Ada.Id, FakeUserDal.Bea.Id], list.Select(u => u.Id));
        Assert.Equal(["Ada", "Bea"], list.Select(u => u.DisplayName));
        Assert.True(list.IsReadOnly);
    }

    [Fact]
    public void Row_Label_FollowsTheLanguage()
    {
        using (Culture.Use("en-CA")) Assert.Equal("Display name", UserInfo.DisplayNameProperty.FriendlyName);
        using (Culture.Use("fr-CA")) Assert.Equal("fr-Display name", UserInfo.DisplayNameProperty.FriendlyName);
    }
}
