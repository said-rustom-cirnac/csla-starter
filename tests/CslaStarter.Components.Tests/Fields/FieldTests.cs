using System.Globalization;
using Bunit;
using Csla;
using CslaStarter.Business.Projects;
using CslaStarter.Business.Tests;
using CslaStarter.Business.Types;
using CslaStarter.Components.Interop;
using CslaStarter.Components.Types;
using CslaStarter.Core.Resources;
using CslaStarter.Dal.Projects;
using Microsoft.Extensions.DependencyInjection;

namespace CslaStarter.Components.Tests.Fields;

public sealed class FieldTests : IDisposable
{
    private readonly Guid _projectId = Guid.CreateVersion7();
    private ComponentTestContext _context = new(TestPrincipals.Admin);

    public FieldTests() => _context.Projects.Add(_projectId, "P-1");

    public void Dispose() => _context.Dispose();

    [Fact]
    public void Form_SetsNovalidate()
    {
        var host = Render();

        Assert.True(host.Find("form").HasAttribute("novalidate"));
    }

    [Fact]
    public void TextField_RendersGcdsAttributes_FromTheObject()
    {
        var host = Render();

        var code = host.Find("gcds-input[name=Code]");
        Assert.Equal(ProjectEdit.CodeProperty.FriendlyName, code.GetAttribute("label"));
        Assert.Equal("20", code.GetAttribute("maxlength"));
        Assert.Equal("other", code.GetAttribute("validate-on"));
        Assert.True(code.HasAttribute("required"));
        Assert.Equal("P-1", code.GetAttribute("value"));
        Assert.False(string.IsNullOrEmpty(code.Id));
        Assert.Null(code.GetAttribute("error-message"));
    }

    [Fact]
    public async Task TextField_Input_SetsTheProperty_AndShowsTheError()
    {
        var host = Render();

        await host.Find("gcds-input[name=Code]").TriggerEventAsync("ongcdsinput", new GcdsEventArgs { Value = "lower case" });

        host.WaitForAssertion(() =>
        {
            var code = host.Find("gcds-input[name=Code]");
            Assert.Equal("lower case", code.GetAttribute("value"));
            Assert.False(string.IsNullOrEmpty(code.GetAttribute("error-message")));
        });
    }

    [Fact]
    public async Task Field_Changed_ShowsMarker()
    {
        var host = Render();
        Assert.Null(host.Find("gcds-input[name=Code]").ParentElement!.GetAttribute("data-changed"));

        await host.Find("gcds-input[name=Code]").TriggerEventAsync("ongcdsinput", new GcdsEventArgs { Value = "P-2" });

        Assert.NotNull(host.Find("gcds-input[name=Code]").ParentElement!.GetAttribute("data-changed"));
    }

    [Fact]
    public async Task TextField_AsyncRule_ShowsChecking()
    {
        _context.Projects.CodeCheckGate = new TaskCompletionSource();
        var host = Render();
        Assert.Null(host.Find("gcds-input[name=Code]").GetAttribute("hint"));

        await host.Find("gcds-input[name=Code]").TriggerEventAsync("ongcdsinput", new GcdsEventArgs { Value = "P-2" });

        Assert.Equal(Messages.Field_Checking, host.Find("gcds-input[name=Code]").GetAttribute("hint"));
        _context.Projects.CodeCheckGate.SetResult();
        host.WaitForAssertion(() => Assert.NotEqual(Messages.Field_Checking, host.Find("gcds-input[name=Code]").GetAttribute("hint")));
    }

    [Fact]
    public void NumberField_NoWrite_IsReadOnlyWithoutRequired()
    {
        _context.Dispose();
        _context = new ComponentTestContext(TestPrincipals.Technical);
        _context.Projects.Add(_projectId, "P-1");
        var host = Render();

        var budget = host.Find("gcds-input[name=Budget]");
        Assert.True(budget.HasAttribute("readonly"));
        Assert.False(budget.HasAttribute("required"));
        Assert.Equal("1000", budget.GetAttribute("value"));
    }

    [Fact]
    public void NumberField_Unreadable_IsNotRendered()
    {
        _context.Dispose();
        _context = new ComponentTestContext(TestPrincipals.Client);
        _context.Projects.Add(_projectId, "P-1", members: TestPrincipals.ClientId);
        var host = Render();

        Assert.Empty(host.FindAll("gcds-input[name=Budget]"));
    }

    [Fact]
    public async Task NumberField_Input_ParsesInvariantNumbers()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-CA");
            var host = Render();

            await host.Find("gcds-input[name=Budget]").TriggerEventAsync("ongcdsinput", new GcdsEventArgs { Value = "1234.5" });

            Assert.Equal("1234.5", host.Find("gcds-input[name=Budget]").GetAttribute("value"));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public async Task DateField_Change_SetsTheDate()
    {
        var host = Render();

        await host.Find("gcds-date-input[name=StartDate]").TriggerEventAsync("ongcdschange", new GcdsEventArgs { Value = "2026-12-01" });

        Assert.Equal("2026-12-01", host.Find("gcds-date-input[name=StartDate]").GetAttribute("value"));
    }

    [Fact]
    public async Task Select_TypeList_RendersOptionsInTheCurrentLanguage()
    {
        var statuses = await _context.Services.GetRequiredService<IDataPortal<ProjectStatusTypeList>>().FetchAsync();
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-CA");
            var host = Render(TypeOptions.From(statuses, current: 2));

            var options = host.FindAll("gcds-select[name=StatusCode] option").Select(o => (o.GetAttribute("value"), o.TextContent.Trim())).ToList();
            Assert.Contains(("2", "Actif"), options);
            Assert.Equal("2", host.Find("gcds-select[name=StatusCode]").GetAttribute("value"));
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    private IRenderedComponent<ProjectFormHost> Render(IReadOnlyList<KeyValuePair<int, string>>? statuses = null)
    {
        var portal = _context.Services.GetRequiredService<IDataPortal<ProjectEdit>>();
        var host = _context.Render<ProjectFormHost>(p => p
            .Add(h => h.Load, () => portal.FetchAsync(_projectId))
            .Add(h => h.Statuses, statuses ?? [new(1, "Planned"), new(2, "Active")]));
        host.WaitForElement("gcds-input[name=Code]");
        return host;
    }
}
