using System.Globalization;
using Csla.Testing;
using CslaStarter.Business.Types;
using CslaStarter.Dal.Types;
using Microsoft.Extensions.DependencyInjection;

namespace CslaStarter.Business.Tests.Types;

public class TypeTests
{
    [Fact]
    public async Task CslaType_Description_FollowsUICulture()
    {
        using var host = Host();
        var roles = await host.GetDataPortal<UserRoleTypeList>().FetchAsync();
        var admin = roles.Find(UserRoleType.Admin)!;
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-CA");
            Assert.Equal("Administrateur", admin.Description);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-CA");
            Assert.Equal("Administrator", admin.Description);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public async Task TypeList_ActiveOrCurrent_KeepsRetiredCurrentValue()
    {
        using var host = Host();
        var roles = await host.GetDataPortal<UserRoleTypeList>().FetchAsync();

        Assert.DoesNotContain(roles.ActiveOrCurrent(null), r => r.Code == 9);
        Assert.Contains(roles.ActiveOrCurrent(9), r => r.Code == 9);
        Assert.Equal([1, 2, 3], roles.ActiveOrCurrent(null).Select(r => r.Code));
    }

    [Fact]
    public void UserRoleType_AdminMatchesSystemRole() =>
        Assert.Equal(Core.Security.SystemRoles.Admin, UserRoleType.Admin);

    private static CslaTestHost Host() =>
        CslaTestHost.Create(o => o.ConfigureServices(s => s.AddSingleton<ITypeDal, FakeTypeDal>()));
}
