using Bunit;
using Csla;
using CslaStarter.Business.Tests;
using CslaStarter.Business.Users;
using CslaStarter.Components.Dialogs;
using CslaStarter.Components.Fields;
using CslaStarter.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace CslaStarter.Components.Tests.Dialogs;

public sealed class DialogTests : IDisposable
{
    private readonly ComponentTestContext _context = new(TestPrincipals.Admin);

    public void Dispose() => _context.Dispose();

    [Fact]
    public void Dialogs_LabelThemselvesWithIdsOfTheirOwn()
    {
        var first = Confirm();
        var second = Confirm();

        var ids = new[] { first, second }.Select(d => d.Find("dialog").GetAttribute("aria-describedby")!).ToList();

        Assert.NotEqual(ids[0], ids[1]);
        Assert.Equal("Sure?", first.Find($"#{ids[0]}").TextContent);
        Assert.Equal("Sure?", second.Find($"#{ids[1]}").TextContent);
    }

    [Fact]
    public async Task Dialog_OpensModal_FocusesTheSafeChoice_EscCancels_AndClosesOnDispose()
    {
        var cancels = 0;
        var dialog = Confirm(() => cancels++);

        _context.DialogModule.VerifyInvoke("showModal");
        Assert.True(dialog.Find("gcds-button[data-action=cancel]").HasAttribute("autofocus"));
        Assert.False(dialog.Find("gcds-button[data-action=confirm]").HasAttribute("autofocus"));

        await dialog.InvokeAsync(() => dialog.Find("dialog").TriggerEventAsync("oncancel", EventArgs.Empty));
        Assert.Equal(1, cancels);

        await _context.DisposeComponentsAsync();
        _context.DialogModule.VerifyInvoke("close");
    }

    [Fact]
    public void DraftPrompt_IsALabelledRegion_NotAnUnfocusedAlertDialog()
    {
        var prompt = _context.Render<CslaDraftPrompt>(p => p.Add(d => d.SavedAt, DateTimeOffset.UnixEpoch));

        var region = prompt.Find("[data-csla-draft]");
        Assert.Equal("region", region.GetAttribute("role"));
        Assert.NotNull(prompt.Find($"#{region.GetAttribute("aria-labelledby")}"));
    }

    [Fact]
    public async Task ConflictDialog_MasksSensitiveValues()
    {
        var bob = Guid.CreateVersion7();
        _context.Users.Add(bob, "Bob", 2);
        var portal = _context.Services.GetRequiredService<IDataPortal<UserEdit>>();
        var theirs = await portal.FetchAsync(bob);
        var mine = await portal.FetchAsync(bob);
        theirs.EmployeeNumber = "E-THEIRS";
        mine.EmployeeNumber = "E-MINE";

        var differences = FieldDiff.Between(theirs, mine);
        var dialog = _context.Render<CslaConflictDialog>(p => p.Add(d => d.Differences, differences));

        Assert.True(Assert.Single(differences, d => d.Property == nameof(UserEdit.EmployeeNumber)).IsSensitive);
        Assert.DoesNotContain("E-THEIRS", dialog.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("E-MINE", dialog.Markup, StringComparison.Ordinal);
        Assert.Contains(FieldValue.MaskText, dialog.Markup, StringComparison.Ordinal);
    }

    private IRenderedComponent<CslaConfirmDialog> Confirm(Action? onCancel = null) =>
        _context.Render<CslaConfirmDialog>(p => p
            .Add(d => d.Message, "Sure?")
            .Add(d => d.ConfirmText, "Yes")
            .Add(d => d.CancelText, "No")
            .Add(d => d.OnCancel, onCancel ?? (() => { })));
}
