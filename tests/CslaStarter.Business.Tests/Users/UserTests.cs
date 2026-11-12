using System.Globalization;
using System.Security.Claims;
using Csla.Testing;
using CslaStarter.Business;
using CslaStarter.Business.Types;
using CslaStarter.Business.Users;
using CslaStarter.Dal.Types;
using CslaStarter.Dal.Users;
using Microsoft.Extensions.DependencyInjection;

namespace CslaStarter.Business.Tests.Users;

public class UserTests
{
    private readonly FakeUserDal _dal = new();
    private readonly Guid _bobId = Guid.CreateVersion7();

    public UserTests() => _dal.Add(_bobId, "Bob", UserRoleType.Client);

    [Theory]
    [InlineData("en-CA", "Display name is required.")]
    [InlineData("fr-CA", "Nom affiché est obligatoire.")]
    public async Task UserEdit_DisplayNameRequired_InBothLanguages(string culture, string expected)
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            using var host = Host(TestPrincipals.Admin);
            var user = await host.GetDataPortal<UserEdit>().FetchAsync(_bobId);

            user.DisplayName = string.Empty;

            Assert.Contains(expected, user.BrokenRulesCollection.Select(r => r.Description));
            Assert.False(user.IsSavable);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public async Task UserEdit_EmployeeNumber_HiddenFromNonAdmin()
    {
        using var host = Host(TestPrincipals.Technical);
        var user = await host.GetDataPortal<UserEdit>().FetchAsync(_bobId);

        Assert.False(user.CanReadProperty(UserEdit.EmployeeNumberProperty));
        Assert.True(string.IsNullOrEmpty(user.EmployeeNumber), "the employee number leaked to a non-admin");
    }

    [Fact]
    public async Task UserEdit_NonAdmin_CannotEditRolesOrSave()
    {
        using var host = Host(TestPrincipals.Technical);
        var user = await host.GetDataPortal<UserEdit>().FetchAsync(_bobId);

        Assert.False(user.CanWriteProperty(UserEdit.RolesProperty));
        Assert.Throws<Csla.Security.SecurityException>(() => user.AssignRole(UserRoleType.Admin));
        Assert.False(UserEdit.CanEdit(host.ApplicationContext));
    }

    [Fact]
    public async Task UserEdit_Client_CannotFetch()
    {
        using var host = Host(TestPrincipals.Client);

        await Assert.ThrowsAnyAsync<Exception>(() => host.GetDataPortal<UserEdit>().FetchAsync(_bobId));
        Assert.Empty(_dal.Fetched);
    }

    [Fact]
    public async Task UserEdit_AdminAssignsRole_AndSaves()
    {
        using var host = Host(TestPrincipals.Admin);
        var user = await host.GetDataPortal<UserEdit>().FetchAsync(_bobId);

        user.AssignRole(UserRoleType.Technical);
        user.AssignRole(UserRoleType.Technical);
        user = await user.SaveAsync();

        Assert.Equal([UserRoleType.Client, UserRoleType.Technical], _dal.Users[_bobId].RoleCodes.Order());
        Assert.Equal(2, user.Roles.Count);
        Assert.False(user.IsDirty);
    }

    [Fact]
    public async Task UserProfile_FetchesOnlyTheSignedInUser()
    {
        _dal.Add(TestPrincipals.ClientId, "Cleo Client", UserRoleType.Client);
        using var host = Host(TestPrincipals.Client);

        var profile = await host.GetDataPortal<UserProfile>().FetchAsync();

        Assert.Equal(TestPrincipals.ClientId, profile.Id);
        Assert.Equal([TestPrincipals.ClientId], _dal.Fetched);
    }

    [Fact]
    public async Task UserProfile_PreferredLanguage_MustBeSupported()
    {
        _dal.Add(TestPrincipals.ClientId, "Cleo Client", UserRoleType.Client);
        using var host = Host(TestPrincipals.Client);
        var profile = await host.GetDataPortal<UserProfile>().FetchAsync();

        profile.PreferredLanguage = "de-DE";

        Assert.False(profile.IsValid);
        profile.PreferredLanguage = "fr-CA";
        Assert.True(profile.IsValid);
    }

    [Fact]
    public async Task UserList_Technical_CanList_ClientCannot()
    {
        using (var host = Host(TestPrincipals.Technical))
        {
            var list = await host.GetDataPortal<UserList>().FetchAsync(string.Empty, 0, 1, 25);
            Assert.Single(list);
            Assert.Equal(1, list.TotalCount);
        }

        using var clientHost = Host(TestPrincipals.Client);
        await Assert.ThrowsAnyAsync<Exception>(() => clientHost.GetDataPortal<UserList>().FetchAsync(string.Empty, 0, 1, 25));
    }

    private CslaTestHost Host(ClaimsPrincipal principal) => CslaTestHost.Create(o => o
        .AsPrincipal(principal)
        .ConfigureServices(s => s.AddCslaStarterBusiness().AddSingleton<IUserDal>(_dal).AddSingleton<ITypeDal, FakeTypeDal>()));
}
