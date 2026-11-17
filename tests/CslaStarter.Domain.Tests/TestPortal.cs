using System.Security.Claims;
using Csla.Configuration;
using Csla.Core;
using Csla.Testing;
using CslaStarter.Domain.Platform;
using CslaStarter.Domain.Platform.Security;
using CslaStarter.Domain.Platform.Users;
using CslaStarter.Domain.Tests.Users;
using Microsoft.Extensions.DependencyInjection;

namespace CslaStarter.Domain.Tests;

/// <summary>
/// CSLA configured as the host configures it, over <see cref="FakeUserDal"/> and a <see cref="CountingUnitOfWork"/>,
/// signed in as Ada. Each test creates its own portal, so nothing is shared between tests.
/// </summary>
internal sealed class TestPortal : IDisposable
{
    private readonly CslaTestHost _host;
    private TestPortal? _signedOut;

    public TestPortal()
        : this(SignedInAs(FakeUserDal.Ada))
    {
    }

    private TestPortal(ClaimsPrincipal principal)
    {
        Work = new CountingUnitOfWork();
        Dal = new FakeUserDal(Work);
        _host = CslaTestHost.Create(t => t
            .ConfigureCsla(o => o.DataPortal(dp => dp.UseLegacyOperationMethods = false))
            .AsPrincipal(principal)
            .ConfigureServices(services => services
                .AddSingleton<IUserDal>(Dal)
                .AddSingleton<IUnitOfWork>(Work)
                .AddScoped<ICurrentUser, CslaCurrentUser>()));
    }

    public FakeUserDal Dal { get; }

    public CountingUnitOfWork Work { get; }

    public Task<T> FetchAsync<T>(params object[] criteria)
        where T : ICslaObject =>
        _host.GetDataPortal<T>().FetchAsync(criteria);

    /// <summary>A portal of its own with an anonymous user; disposed with this one.</summary>
    public TestPortal SignedOut() => _signedOut ??= new TestPortal(new ClaimsPrincipal(new ClaimsIdentity()));

    public void Dispose()
    {
        _signedOut?.Dispose();
        _host.Dispose();
    }

    private static ClaimsPrincipal SignedInAs(UserDto user) =>
        new(new ClaimsIdentity(
            [new Claim(AppClaimTypes.UserId, user.Id.ToString()), new Claim(AppClaimTypes.DisplayName, user.DisplayName)],
            "Test"));
}
