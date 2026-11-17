using CslaStarter.Domain.Platform.Users;

namespace CslaStarter.Domain.Tests.Users;

public sealed class UserEditTests : IDisposable
{
    private static readonly Guid Id = FakeUserDal.Ada.Id;

    private readonly TestPortal _portal = new();

    public void Dispose() => _portal.Dispose();

    [Fact]
    public async Task Fetch_LoadsTheUser()
    { var user = await _portal.FetchAsync<UserEdit>(Id); Assert.Equal("Ada", user.DisplayName); Assert.Equal(7u, user.Version); Assert.False(user.IsDirty); Assert.False(user.IsNew); }

    [Theory, InlineData(""), InlineData("   ")]
    public async Task DisplayName_Blank_BreaksTheRequiredRule(string value)
    {
        using var _ = Culture.Use("en-CA");
        var user = await _portal.FetchAsync<UserEdit>(Id);
        user.DisplayName = value;
        Assert.False(user.IsValid);
        Assert.Equal("Display name is required.", user.BrokenRulesCollection.GetFirstBrokenRule(UserEdit.DisplayNameProperty)!.Description);
    }

    [Fact]
    public async Task DisplayName_Allows200_Rejects201()
    {
        using var _ = Culture.Use("en-CA");
        var user = await _portal.FetchAsync<UserEdit>(Id);
        user.DisplayName = new string('a', 200); Assert.True(user.IsValid);
        user.DisplayName = new string('a', 201);
        Assert.Equal("Display name must be 200 characters or fewer.", user.BrokenRulesCollection.GetFirstBrokenRule(UserEdit.DisplayNameProperty)!.Description);
    }

    [Fact]
    public async Task Label_And_Message_FollowTheLanguage()
    {
        using var _ = Culture.Use("fr-CA");
        Assert.Equal("fr-Display name", UserEdit.DisplayNameProperty.FriendlyName);
        var user = await _portal.FetchAsync<UserEdit>(Id);
        user.DisplayName = "";
        Assert.Equal("Le champ fr-Display name est obligatoire.", user.BrokenRulesCollection.GetFirstBrokenRule(UserEdit.DisplayNameProperty)!.Description);
    }

    [Fact]
    public async Task Save_StagesTheChange_CommitsOnce_And_TakesTheNewVersion()
    {
        var user = await _portal.FetchAsync<UserEdit>(Id);
        user.DisplayName = "Ada L.";
        user = await user.SaveAsync();
        Assert.Equal(new UserDto(Id, "Ada L.", 7), _portal.Dal.Staged.Single());
        Assert.Equal(1, _portal.Work.Commits);
        Assert.Equal(8u, user.Version); Assert.False(user.IsDirty);
    }

    [Fact]
    public async Task Save_Invalid_Throws_And_StagesNothing()
    {
        var user = await _portal.FetchAsync<UserEdit>(Id);
        user.DisplayName = "";
        await Assert.ThrowsAsync<Csla.Rules.ValidationException>(() => user.SaveAsync());
        Assert.Empty(_portal.Dal.Staged); Assert.Equal(0, _portal.Work.Commits);
    }

    [Fact]
    public async Task Fetch_SignedOut_IsRefused() =>
        await Assert.ThrowsAsync<Csla.Security.SecurityException>(() => _portal.SignedOut().FetchAsync<UserEdit>(Id));
}
