using Csla;
using CslaStarter.Business.Projects;
using CslaStarter.Business.Tests;
using CslaStarter.Components.Forms;
using CslaStarter.Dal.Projects;
using Microsoft.Extensions.DependencyInjection;

namespace CslaStarter.Components.Tests.Fields;

public sealed class FieldHintsTests : IDisposable
{
    private readonly ComponentTestContext _context = new(TestPrincipals.Technical);
    private readonly Guid _projectId = Guid.CreateVersion7();

    public FieldHintsTests() => _context.Projects.Add(_projectId, "P-1");

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task FieldHints_RequiredAndMaxLength_ComeFromRules()
    {
        var project = await _context.Services.GetRequiredService<IDataPortal<ProjectEdit>>().FetchAsync(_projectId);

        var code = FieldHints.Describe(project, Property(project, nameof(ProjectEdit.Code)), hint: null, showError: true, checkingText: "Checking");
        var description = FieldHints.Describe(project, Property(project, nameof(ProjectEdit.Description)), hint: null, showError: true, checkingText: "Checking");

        Assert.Equal((true, (int?)20, true, false), (code.Required, code.MaxLength, code.Visible, code.ReadOnly));
        Assert.Equal((false, (int?)2000), (description.Required, description.MaxLength));
    }

    [Fact]
    public async Task FieldHints_NoWrite_IsReadOnly_AndNeverRequired()
    {
        var project = await _context.Services.GetRequiredService<IDataPortal<ProjectEdit>>().FetchAsync(_projectId);

        var budget = FieldHints.Describe(project, Property(project, nameof(ProjectEdit.Budget)), hint: null, showError: true, checkingText: "Checking");

        Assert.Equal((true, true, false), (budget.Visible, budget.ReadOnly, budget.Required));
    }

    [Fact]
    public async Task FieldHints_ErrorsOnlyWhenShown()
    {
        var project = await _context.Services.GetRequiredService<IDataPortal<ProjectEdit>>().FetchAsync(_projectId);
        project.Name = string.Empty;

        Assert.Null(FieldHints.Describe(project, Property(project, nameof(ProjectEdit.Name)), null, showError: false, "Checking").Error);
        Assert.False(string.IsNullOrEmpty(FieldHints.Describe(project, Property(project, nameof(ProjectEdit.Name)), null, showError: true, "Checking").Error));
    }

    [Fact]
    public async Task FieldHints_WarningAndInfo_BecomeTheHint_UnlessOneIsGiven()
    {
        var probe = await _context.Services.GetRequiredService<IDataPortal<HintProbe>>().CreateAsync();

        var derived = FieldHints.Describe(probe, Property(probe, nameof(HintProbe.Note)), hint: null, showError: true, "Checking");
        var given = FieldHints.Describe(probe, Property(probe, nameof(HintProbe.Note)), hint: "Given", showError: true, "Checking");

        Assert.Contains("Short notes read better.", derived.Hint, StringComparison.Ordinal);
        Assert.Contains("Notes are visible to the team.", derived.Hint, StringComparison.Ordinal);
        Assert.Null(derived.Error);
        Assert.Equal("Given", given.Hint);
    }

    private static Csla.Blazor.PropertyInfo Property(object model, string name) => new(model, name, " ");
}
