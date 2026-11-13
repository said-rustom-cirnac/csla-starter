using System.Security.Claims;
using Bunit;
using Csla.Testing;
using CslaStarter.Business;
using CslaStarter.Business.Tests;
using CslaStarter.Business.Tests.Auditing;
using CslaStarter.Business.Tests.Projects;
using CslaStarter.Business.Tests.Users;
using CslaStarter.Components;
using CslaStarter.Core.Auditing;
using CslaStarter.Core.Drafts;
using CslaStarter.Core.Realtime;
using CslaStarter.Core.Security;
using CslaStarter.Core.Sessions;
using CslaStarter.Dal.Projects;
using CslaStarter.Dal.Types;
using CslaStarter.Dal.Users;
using CslaStarter.Infrastructure.Realtime;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
                .AddSingleton<IUserDal>(Users)
                .AddSingleton<IAuditHistoryReader>(AuditReader)));
        _scope = Host.CreateScope();

        Services.AddCslaStarterComponents();
        Services.AddSingleton<TimeProvider>(Time);
        Services.AddSingleton<IEntityChangeHub>(Hub);
        Services.AddSingleton<IPresenceTracker>(Presence);
        Services.AddSingleton<IDraftStore>(Drafts);
        Services.AddSingleton<ISecurityEventLog>(SecurityEvents);
        Services.AddSingleton<ISessionActivity>(Session);
        Services.AddSingleton<AntiforgeryStateProvider>(new TestAntiforgery());
        Services.AddSingleton<ILoggerFactory>(new LoggerFactory([new ListLoggerProvider(Logs)]));
        Services.AddFallbackServiceProvider(_scope.ServiceProvider);
        JSInterop.Mode = JSRuntimeMode.Loose;
        SessionModule = JSInterop.SetupModule("./_content/CslaStarter.Components/session.js");
        SessionModule.Mode = JSRuntimeMode.Loose;
        ShortcutsModule = JSInterop.SetupModule("./_content/CslaStarter.Components/shortcuts.js");
        ShortcutsModule.Mode = JSRuntimeMode.Loose;
        DialogModule = JSInterop.SetupModule("./_content/CslaStarter.Components/dialog.js");
        DialogModule.Mode = JSRuntimeMode.Loose;
    }

    private readonly IServiceScope _scope;

    public CslaTestHost Host { get; }

    public FakeProjectDal Projects { get; } = new();

    public FakeTypeDal Types { get; } = new();

    public FakeUserDal Users { get; } = new();

    public FakeAuditHistoryReader AuditReader { get; } = new();

    public FakeSessionActivity Session { get; } = new();

    public BunitJSModuleInterop SessionModule { get; }

    public BunitJSModuleInterop ShortcutsModule { get; }


    public BunitJSModuleInterop DialogModule { get; }

    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 11, 12, 10, 42, 0, TimeSpan.Zero));

    public EntityChangeHub Hub { get; } = new(NullLogger<EntityChangeHub>.Instance);

    public PresenceTracker Presence => _presence ??= new PresenceTracker(new InMemoryPresenceTransport(), Time, NullLogger<PresenceTracker>.Instance);

    public InMemoryDraftStore Drafts { get; } = new();

    public RecordingSecurityEventLog SecurityEvents { get; } = new();

    /// <summary>Everything the components logged, for asserting on (or reading when a test fails).</summary>
    public List<string> Logs { get; } = [];

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

public sealed class FakeSessionActivity : ISessionActivity
{
    public SessionPhase Phase { get; private set; }

    public TimeSpan Remaining { get; set; } = TimeSpan.FromMinutes(2);

    public int Activity { get; private set; }

    public event Action<SessionPhase>? PhaseChanged;

    public void RecordActivity() => Activity++;

    public void Enter(SessionPhase phase)
    {
        Phase = phase;
        PhaseChanged?.Invoke(phase);
    }
}

public sealed class TestAntiforgery : AntiforgeryStateProvider
{
    public override AntiforgeryRequestToken? GetAntiforgeryToken() => new("test-token", "__RequestVerificationToken");
}

public sealed class ListLoggerProvider(List<string> logs) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new ListLogger(logs, categoryName);

    public void Dispose()
    {
    }

    private sealed class ListLogger(List<string> logs, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (logs)
            {
                logs.Add($"{logLevel} {category}: {formatter(state, exception)} {exception}");
            }
        }
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
