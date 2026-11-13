using System.Security.Claims;
using Bunit;
using Csla.Testing;
using CslaStarter.Business;
using CslaStarter.Business.Tests;
using CslaStarter.Business.Tests.Projects;
using CslaStarter.Business.Tests.Users;
using CslaStarter.Components;
using CslaStarter.Core.Drafts;
using CslaStarter.Core.Realtime;
using CslaStarter.Core.Security;
using CslaStarter.Dal.Projects;
using CslaStarter.Dal.Types;
using CslaStarter.Dal.Users;
using CslaStarter.Infrastructure.Realtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace CslaStarter.Components.Tests;

/// <summary>
/// A bUnit context playing one circuit: CSLA (through CslaTestHost) as the given user over fake DALs, the real
/// change hub and presence tracker, in-memory drafts, recorded security events and a controllable clock.
/// </summary>
public sealed class ComponentTestContext : BunitContext
{
    public ComponentTestContext(ClaimsPrincipal? user = null)
    {
        Types.Rows["ProjectStatusType"] = [new(1, "Planned", "Planifié", true, 1), new(2, "Active", "Actif", true, 2), new(3, "On hold", "En suspens", true, 3), new(4, "Closed", "Fermé", true, 4)];
        Types.Rows["UserRoleType"] = [new(1, "Administrator", "Administrateur", true, 1), new(2, "Client", "Client", true, 2), new(3, "Technical", "Technique", true, 3)];
        Host = CslaTestHost.Create(o => o
            .AsPrincipal(user ?? TestPrincipals.Admin)
            .ConfigureCsla(csla => csla.UseCslaStarterRules())
            .ConfigureServices(s => s
                .AddCslaStarterBusiness()
                .AddSingleton<IProjectDal>(Projects)
                .AddSingleton<ITypeDal>(Types)
                .AddSingleton<IUserDal>(Users)));
        _scope = Host.CreateScope();

        Services.AddCslaStarterComponents();
        Services.AddSingleton<TimeProvider>(Time);
        Services.AddSingleton<IEntityChangeHub>(Hub);
        Services.AddSingleton<IPresenceTracker>(Presence);
        Services.AddSingleton<IDraftStore>(Drafts);
        Services.AddSingleton<ISecurityEventLog>(SecurityEvents);
        Services.AddFallbackServiceProvider(_scope.ServiceProvider);
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private readonly IServiceScope _scope;

    public CslaTestHost Host { get; }

    public FakeProjectDal Projects { get; } = new();

    public FakeTypeDal Types { get; } = new();

    public FakeUserDal Users { get; } = new();

    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 11, 12, 10, 42, 0, TimeSpan.Zero));

    public EntityChangeHub Hub { get; } = new(NullLogger<EntityChangeHub>.Instance);

    public PresenceTracker Presence => _presence ??= new PresenceTracker(new InMemoryPresenceTransport(), Time, NullLogger<PresenceTracker>.Instance);

    public InMemoryDraftStore Drafts { get; } = new();

    public RecordingSecurityEventLog SecurityEvents { get; } = new();

    private PresenceTracker? _presence;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _presence?.Dispose();
            _scope.Dispose();
            Host.Dispose();
        }

        base.Dispose(disposing);
    }
}

public sealed class RecordingSecurityEventLog : ISecurityEventLog
{
    public List<SecurityEvent> Written { get; } = [];

    public Task WriteAsync(SecurityEvent securityEvent, CancellationToken cancellationToken)
    {
        Written.Add(securityEvent);
        return Task.CompletedTask;
    }
}

public sealed class InMemoryDraftStore : IDraftStore
{
    public Dictionary<DraftKey, Draft> Drafts { get; } = [];

    public Task SaveAsync(DraftKey key, uint? version, byte[] payload, CancellationToken cancellationToken)
    {
        Drafts[key] = new Draft(version, payload, DateTimeOffset.UnixEpoch);
        return Task.CompletedTask;
    }

    public Task<Draft?> GetAsync(DraftKey key, CancellationToken cancellationToken) => Task.FromResult(Drafts.GetValueOrDefault(key));

    public Task DeleteAsync(DraftKey key, CancellationToken cancellationToken)
    {
        Drafts.Remove(key);
        return Task.CompletedTask;
    }
}
