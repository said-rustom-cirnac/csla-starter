using Bunit;
using Csla;
using CslaStarter.Business.Projects;
using CslaStarter.Business.Tests;
using CslaStarter.Components.Interop;
using CslaStarter.Core.Auditing;
using Microsoft.Extensions.DependencyInjection;

namespace CslaStarter.Components.Tests.Buttons;

public sealed class ButtonTests : IDisposable
{
    private readonly Guid _projectId = Guid.CreateVersion7();
    private ComponentTestContext _context = new(TestPrincipals.Admin);

    public ButtonTests() => _context.Projects.Add(_projectId, "P-1");

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task Button_Save_DisabledUntilSavable_AndWhileBusy()
    {
        var host = Render();
        Assert.True(Button(host, "Save").HasAttribute("disabled"));

        await Input(host, "Name", "Renamed");
        host.WaitForAssertion(() => Assert.False(Button(host, "Save").HasAttribute("disabled")));

        _context.Projects.CodeCheckGate = new TaskCompletionSource();
        await Input(host, "Code", "P-2");
        host.WaitForAssertion(() => Assert.True(Button(host, "Save").HasAttribute("disabled")));
        _context.Projects.CodeCheckGate.SetResult();
        host.WaitForAssertion(() => Assert.False(Button(host, "Save").HasAttribute("disabled")));
    }

    [Fact]
    public async Task Button_Save_SavesThroughTheForm()
    {
        var host = Render();
        await Input(host, "Name", "Renamed");

        await Click(host, "gcds-button[data-kind=Save]");

        host.WaitForAssertion(() => Assert.Equal("Renamed", _context.Projects.LastSaved?.Name));
    }

    [Fact]
    public async Task Button_Cancel_UndoesTheEdits()
    {
        var host = Render();
        await Input(host, "Name", "Renamed");

        await Click(host, "gcds-button[data-kind=Cancel]");

        host.WaitForAssertion(() => Assert.Equal("P-1", host.Find("gcds-input[name=Name]").GetAttribute("value")));
        Assert.True(Button(host, "Cancel").HasAttribute("disabled"));
    }

    [Fact]
    public void Button_Delete_HiddenWithoutPermission()
    {
        _context.Dispose();
        _context = new ComponentTestContext(TestPrincipals.Technical);
        _context.Projects.Add(_projectId, "P-1");

        var host = Render();

        Assert.Empty(host.FindAll("gcds-button[data-kind=Delete]"));
    }

    [Fact]
    public async Task Button_Delete_ConfirmsFirst_ThenDeletes()
    {
        var host = Render();

        await Click(host, "gcds-button[data-kind=Delete]");
        Assert.True(_context.Projects.Projects.ContainsKey(_projectId));
        await Click(host, "[data-csla-confirm] gcds-button[data-action=confirm]");

        host.WaitForAssertion(() => Assert.Equal(1, host.Instance.DeletedCount));
        Assert.False(_context.Projects.Projects.ContainsKey(_projectId));
    }

    [Fact]
    public async Task Field_HistoryButton_OpensTheFieldsHistory()
    {
        _context.AuditReader.Entries.Add(new AuditEntry(1, DateTimeOffset.UnixEpoch, null, "Jane", "Update", "Project", _projectId.ToString(), [new AuditChange("Name", "Old", "New", false, null)]));
        var host = Render();

        await Click(host, "gcds-input[name=Name] ~ gcds-button[data-action=history]");

        host.WaitForAssertion(() => Assert.Contains("Old → New", host.Find("[data-csla-field-history]").TextContent, StringComparison.Ordinal));
        Assert.Equal(("Project", _projectId.ToString(), "Name"), (_context.AuditReader.LastQuery!.EntityType, _context.AuditReader.LastQuery.EntityKey, _context.AuditReader.LastQuery.Property));
    }

    private static AngleSharp.Dom.IElement Button(IRenderedComponent<ButtonFormHost> host, string kind) => host.Find($"gcds-button[data-kind={kind}]");

    private static Task Input(IRenderedComponent<ButtonFormHost> host, string name, string value) =>
        host.InvokeAsync(() => host.Find($"gcds-input[name={name}]").TriggerEventAsync("ongcdsinput", new GcdsEventArgs { Value = value }));

    private static Task Click(IRenderedComponent<ButtonFormHost> host, string selector) =>
        host.InvokeAsync(() => host.Find(selector).TriggerEventAsync("ongcdsclick", new GcdsEventArgs()));

    private IRenderedComponent<ButtonFormHost> Render()
    {
        var portal = _context.Services.GetRequiredService<IDataPortal<ProjectEdit>>();
        var host = _context.Render<ButtonFormHost>(p => p.Add(h => h.Load, () => portal.FetchAsync(_projectId)));
        host.WaitForElement("gcds-input[name=Name]");
        return host;
    }
}
