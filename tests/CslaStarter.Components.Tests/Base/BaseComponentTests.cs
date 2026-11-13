using Bunit;
using CslaStarter.Business.Tests;
using CslaStarter.Components.Base;
using CslaStarter.Components.Services;
using CslaStarter.Core.Realtime;
using CslaStarter.Core.Security;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace CslaStarter.Components.Tests.Base;

public sealed class BaseComponentTests : IDisposable
{
    private readonly ComponentTestContext _context = new(TestPrincipals.Technical);

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task Base_RunAsync_SetsBusyAndClearsIt()
    {
        var probe = _context.Render<Probe>();
        var gate = new TaskCompletionSource();

        var run = probe.InvokeAsync(() => probe.Instance.Run("save", _ => gate.Task));
        Assert.True(probe.Instance.Busy);
        gate.SetResult();

        Assert.True(await run);
        Assert.False(probe.Instance.Busy);
    }

    [Fact]
    public async Task Base_RunAsync_AccessDenied_ShowsToastAndWritesSecurityEvent()
    {
        var probe = _context.Render<Probe>();

        var ok = await probe.InvokeAsync(() => probe.Instance.Run("delete", _ => throw new Csla.Security.SecurityException("no")));

        Assert.False(ok);
        var toast = Assert.Single(_context.Services.GetService<IToastService>()!.Toasts);
        Assert.Equal(ToastKind.Error, toast.Kind);
        var written = Assert.Single(_context.SecurityEvents.Written);
        Assert.Equal((SecurityEventKind.AccessDenied, TestPrincipals.TechnicalId), (written.Kind, written.UserId));
        Assert.Contains("delete", written.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Base_RunAsync_Unexpected_ShowsAReference_NotTheDetail()
    {
        var probe = _context.Render<Probe>();
        string? announced = null;
        _context.Services.GetService<ILiveAnnouncer>()!.Announced += (message, _) => announced = message;

        await probe.InvokeAsync(() => probe.Instance.Run("load", _ => throw new InvalidOperationException("connection string leaked")));

        var toast = Assert.Single(_context.Services.GetService<IToastService>()!.Toasts);
        Assert.DoesNotContain("connection string", toast.Message, StringComparison.Ordinal);
        Assert.Equal(toast.Message, announced);
        Assert.Empty(_context.SecurityEvents.Written);
    }

    [Fact]
    public async Task Base_Subscribe_IgnoresOwnOrigin_AndRendersOnTheRenderer()
    {
        var probe = _context.Render<Probe>();
        var origin = _context.Services.GetService<CircuitOrigin>()!.Id;

        await _context.Hub.DispatchAsync(new EntityChange("Project", "p-1", ChangeKind.Updated, 2, null, "Me", origin));
        await _context.Hub.DispatchAsync(new EntityChange("Project", "p-1", ChangeKind.Updated, 3, null, "Jane", "other-circuit"));

        probe.WaitForAssertion(() => Assert.Equal("Jane", probe.Find("output").TextContent));
        Assert.Equal(1, probe.Instance.Received);
    }

    [Fact]
    public async Task Base_Dispose_RemovesSubscriptions_AndCancelsToken()
    {
        var probe = _context.Render<Probe>().Instance;
        var token = probe.Token;

        await _context.DisposeComponentsAsync();
        await _context.Hub.DispatchAsync(new EntityChange("Project", "p-1", ChangeKind.Updated, 3, null, "Jane", "other-circuit"));

        Assert.True(token.IsCancellationRequested);
        Assert.Equal(0, probe.Received);
    }

    [Fact]
    public async Task Base_OnResync_RunsOnTheRenderer()
    {
        var probe = _context.Render<Probe>();

        await _context.Hub.RaiseResyncAsync();

        probe.WaitForAssertion(() => Assert.Equal(1, probe.Instance.Resyncs));
    }

    [Fact]
    public void Base_ComponentId_IsStableAndUnique()
    {
        var first = _context.Render<Probe>();
        var second = _context.Render<Probe>();
        var id = first.Instance.Id;

        first.Render();

        Assert.Equal(id, first.Instance.Id);
        Assert.NotEqual(id, second.Instance.Id);
    }

    private sealed class Probe : CslaBaseComponent
    {
        private string _lastChangedBy = string.Empty;

        public int Received { get; private set; }

        public int Resyncs { get; private set; }

        public bool Busy => IsBusy;

        public string Id => ComponentId;

        public CancellationToken Token => DisposalToken;

        public Task<bool> Run(string operation, Func<CancellationToken, Task> work) => RunAsync(operation, work);

        protected override void OnInitialized()
        {
            base.OnInitialized();
            Subscribe(ChangeTopic.ForRecord("Project", "p-1"), change =>
            {
                Received++;
                _lastChangedBy = change.ChangedByName;
                StateHasChanged();
                return Task.CompletedTask;
            });
            OnResync(() =>
            {
                Resyncs++;
                return Task.CompletedTask;
            });
        }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "output");
            builder.AddContent(1, _lastChangedBy);
            builder.CloseElement();
        }
    }
}
