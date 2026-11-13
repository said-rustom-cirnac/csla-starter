using Bunit;
using Csla;
using CslaStarter.Business.Drafts;
using CslaStarter.Business.Projects;
using CslaStarter.Business.Tests;
using CslaStarter.Components.Drafts;
using CslaStarter.Components.Forms;
using CslaStarter.Components.Interop;
using CslaStarter.Components.Tests.Forms;
using CslaStarter.Core.Drafts;
using CslaStarter.Core.Resources;
using Microsoft.Extensions.DependencyInjection;

namespace CslaStarter.Components.Tests.Drafts;

public sealed class FormDraftTests : IDisposable
{
    private readonly ComponentTestContext _context = new(TestPrincipals.Admin);
    private readonly Guid _projectId = Guid.CreateVersion7();

    public FormDraftTests() => _context.Projects.Add(_projectId, "P-1");

    public void Dispose() => _context.Dispose();

    private DraftKey Key => new(TestPrincipals.AdminId, "Project", _projectId.ToString());

    [Fact]
    public async Task Form_Edit_SavesADraft2SecondsAfterTheLastEdit()
    {
        var host = Render();
        await Field(host, "Name").TriggerEventAsync("ongcdsinput", new GcdsEventArgs { Value = "Mine" });

        _context.Time.Advance(TimeSpan.FromSeconds(1));
        Assert.False(_context.Drafts.Drafts.ContainsKey(Key));
        _context.Time.Advance(TimeSpan.FromSeconds(1.1));

        host.WaitForAssertion(() => Assert.True(_context.Drafts.Drafts.ContainsKey(Key)));
        var draft = _context.Drafts.Drafts[Key];
        Assert.Equal(5u, draft.Version);
        Assert.Equal("Mine", DraftCodec.Deserialize<ProjectEdit>(_context.Services.GetRequiredService<ApplicationContext>(), draft.Payload).Name);
    }

    [Fact]
    public void Form_Clean_SavesNoDraft()
    {
        Render();

        _context.Time.Advance(TimeSpan.FromSeconds(10));

        Assert.Empty(_context.Drafts.Drafts);
    }

    [Fact]
    public async Task Form_Save_DeletesTheDraft()
    {
        var host = Render();
        var form = host.FindComponent<CslaForm<ProjectEdit>>().Instance;
        await Field(host, "Name").TriggerEventAsync("ongcdsinput", new GcdsEventArgs { Value = "Mine" });
        _context.Time.Advance(TimeSpan.FromSeconds(3));
        host.WaitForAssertion(() => Assert.True(_context.Drafts.Drafts.ContainsKey(Key)));

        Assert.True(await host.InvokeAsync(form.SaveAsync));
        _context.Time.Advance(TimeSpan.FromSeconds(3));

        Assert.False(_context.Drafts.Drafts.ContainsKey(Key));
    }

    [Fact]
    public async Task Form_FlushAll_SavesTheDraftAtOnce()
    {
        var host = Render();
        await Field(host, "Name").TriggerEventAsync("ongcdsinput", new GcdsEventArgs { Value = "Mine" });

        await host.InvokeAsync(() => _context.Services.GetRequiredService<DraftFlushRegistry>().FlushAllAsync());

        Assert.True(_context.Drafts.Drafts.ContainsKey(Key));
    }

    [Fact]
    public async Task Form_OpenWithDraft_OffersRestore_WarnsWhenRecordChanged()
    {
        await StoreDraftAsync("From the draft", version: 5);
        _context.Projects.Projects[_projectId] = _context.Projects.Projects[_projectId] with { Version = 6 };
        var host = Render();

        var prompt = host.WaitForElement("[data-csla-draft]");
        Assert.Contains(Messages.Draft_RecordChanged, prompt.TextContent, StringComparison.Ordinal);
        await prompt.QuerySelector("gcds-button[data-action=restore]")!.TriggerEventAsync("ongcdsclick", EventArgs.Empty);

        host.WaitForAssertion(() => Assert.Equal("From the draft", Field(host, "Name").GetAttribute("value")));
        Assert.Empty(host.FindAll("[data-csla-draft]"));
        Assert.True(host.FindComponent<CslaForm<ProjectEdit>>().Instance.IsDirty);
    }

    [Fact]
    public async Task Form_OpenWithDraft_Discard_DeletesIt()
    {
        await StoreDraftAsync("From the draft", version: 5);
        var host = Render();

        var prompt = host.WaitForElement("[data-csla-draft]");
        Assert.DoesNotContain(Messages.Draft_RecordChanged, prompt.TextContent, StringComparison.Ordinal);
        await prompt.QuerySelector("gcds-button[data-action=discard]")!.TriggerEventAsync("ongcdsclick", EventArgs.Empty);

        Assert.False(_context.Drafts.Drafts.ContainsKey(Key));
        Assert.Equal("P-1", Field(host, "Name").GetAttribute("value"));
    }

    [Fact]
    public void Form_OpenWithUnreadableDraft_IgnoresAndDeletesIt()
    {
        _context.Drafts.Drafts[Key] = new Draft(5, [1, 2, 3], DateTimeOffset.UnixEpoch);

        var host = Render();

        Assert.Empty(host.FindAll("[data-csla-draft]"));
        Assert.False(_context.Drafts.Drafts.ContainsKey(Key));
    }

    private async Task StoreDraftAsync(string name, uint version)
    {
        var project = await _context.Services.GetRequiredService<IDataPortal<ProjectEdit>>().FetchAsync(_projectId);
        project.Name = name;
        _context.Drafts.Drafts[Key] = new Draft(version, DraftCodec.Serialize(_context.Services.GetRequiredService<ApplicationContext>(), project), new DateTimeOffset(2026, 11, 12, 10, 42, 0, TimeSpan.Zero));
    }

    private static AngleSharp.Dom.IElement Field(IRenderedComponent<LiveFormHost> host, string name) => host.Find($"gcds-input[name={name}]");

    private IRenderedComponent<LiveFormHost> Render()
    {
        var portal = _context.Services.GetRequiredService<IDataPortal<ProjectEdit>>();
        var host = _context.Render<LiveFormHost>(p => p.Add(h => h.Load, () => portal.FetchAsync(_projectId)));
        host.WaitForElement("gcds-input[name=Name]");
        return host;
    }
}
