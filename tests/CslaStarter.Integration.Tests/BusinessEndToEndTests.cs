using System.Globalization;
using System.Security.Claims;
using Csla;
using Csla.Testing;
using CslaStarter.Business;
using CslaStarter.Business.Projects;
using CslaStarter.Core.Security;
using CslaStarter.Dal;
using CslaStarter.Dal.Users;
using CslaStarter.Data;
using Microsoft.Extensions.DependencyInjection;

namespace CslaStarter.Integration.Tests;

/// <summary>
/// Business objects on the real stack: CSLA from DI, the data access context read from the principal, the real DALs,
/// PostgreSQL as app_runtime. This is where rules, record-level access and the shared DbContext meet.
/// </summary>
public sealed class BusinessEndToEndTests(PostgresFixture postgres) : IAsyncLifetime
{
    private TestDatabase _db = null!;
    private Guid _admin;
    private Guid _client;

    public async ValueTask InitializeAsync()
    {
        _db = await TestDatabase.CreateAsync(postgres);
        _admin = await _db.AddUserAsync("Ada Admin", 1);
        _client = await _db.AddUserAsync("Cleo Client", 2);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Project_AdminCreatesEditsAndSaves_ClientMemberSeesIt()
    {
        using var admin = Host(_admin, 1);
        var project = await admin.GetDataPortal<ProjectEdit>().CreateAsync();
        project.Code = "E2E-1";
        project.Name = "End to end";
        project.AddTask("First");
        project.AddMember(_client, "Cleo Client");
        await project.WaitForIdle(TimeSpan.FromSeconds(10));
        project = await project.SaveAsync();

        // A fresh fetch runs the rules inside the data portal; the database-backed ones must not share its DbContext.
        var fetched = await admin.GetDataPortal<ProjectEdit>().FetchAsync(project.Id);
        fetched.Name = "Renamed";
        fetched = await fetched.SaveAsync();

        using var client = Host(_client, 2);
        var seen = await client.GetDataPortal<ProjectEdit>().FetchAsync(project.Id);
        Assert.Equal(("Renamed", 1, fetched.Version), (seen.Name, seen.Tasks.Count, seen.Version));
        Assert.Null(seen.Budget);
    }

    [Fact]
    public async Task Project_ClientNonMember_GetsNotFound()
    {
        using var admin = Host(_admin, 1);
        var project = await admin.GetDataPortal<ProjectEdit>().CreateAsync();
        project.Code = "E2E-2";
        project.Name = "Hidden";
        await project.WaitForIdle(TimeSpan.FromSeconds(10));
        project = await project.SaveAsync();

        using var client = Host(_client, 2);
        var error = await Assert.ThrowsAsync<DataPortalException>(() => client.GetDataPortal<ProjectEdit>().FetchAsync(project.Id));
        Assert.IsType<NotFoundException>(error.BusinessException);
    }

    [Fact]
    public async Task Project_DuplicateCodeSavedTogether_IsAValidationError()
    {
        using var admin = Host(_admin, 1);
        var first = await admin.GetDataPortal<ProjectEdit>().CreateAsync();
        var second = await admin.GetDataPortal<ProjectEdit>().CreateAsync();
        foreach (var project in new[] { first, second })
        {
            project.Code = "E2E-SAME";
            project.Name = "Same code";
            await project.WaitForIdle(TimeSpan.FromSeconds(10));
        }

        first = await first.SaveAsync();
        var error = await Assert.ThrowsAsync<DataPortalException>(() => second.SaveAsync());

        Assert.IsType<Csla.Rules.ValidationException>(error.BusinessException);
        Assert.False(first.IsNew);
    }

    private CslaTestHost Host(Guid userId, params int[] roles) => CslaTestHost.Create(o => o
        .AsPrincipal(Principal(userId, roles))
        .ConfigureCsla(csla => csla.UseCslaStarterRules())
        .ConfigureServices(services => services
            .AddLogging()
            .AddCslaStarterBusiness()
            .AddCslaStarterData(_db.RuntimeConnectionString)
            .AddSingleton<IUserRoleCacheInvalidator, NoInvalidation>()));

    private static ClaimsPrincipal Principal(Guid userId, int[] roles)
    {
        var claims = new List<Claim> { new(ClaimTypes.Name, userId.ToString()), new(AppClaimTypes.UserId, userId.ToString()) };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r.ToString(CultureInfo.InvariantCulture))));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test", ClaimTypes.Name, ClaimTypes.Role));
    }

    private sealed class NoInvalidation : IUserRoleCacheInvalidator
    {
        public void Invalidate(string tenantId, string objectId)
        {
        }
    }
}
