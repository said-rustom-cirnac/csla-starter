using System.Text.Json;
using Bunit;
using Csla;
using CslaStarter.Components.Interop;
using Microsoft.Extensions.DependencyInjection;

namespace CslaStarter.Components.Tests.Fields;

public sealed class ChoiceFieldTests : IDisposable
{
    private readonly ComponentTestContext _context = new();

    public void Dispose() => _context.Dispose();

    [Fact]
    public void Choices_RenderOptionsAsJson_WithTheCurrentValueChecked()
    {
        var host = Render();

        var radios = Options(host.Find("gcds-radios[name=Note]"));
        Assert.Equal([("A note", true), ("Other", false)], radios);
        Assert.Equal([("1", false), ("2", false)], Options(host.Find("gcds-checkboxes[name=Tags]")));
    }

    [Fact]
    public async Task CheckboxGroup_Change_SetsTheCheckedValues()
    {
        var host = Render();

        await host.Find("gcds-checkboxes[name=Tags]").TriggerEventAsync("ongcdschange", new GcdsEventArgs { Value = "2,1" });

        host.WaitForAssertion(() => Assert.Equal([("1", true), ("2", true)], Options(host.Find("gcds-checkboxes[name=Tags]"))));
    }

    [Fact]
    public async Task Checkbox_Change_SetsTheFlag()
    {
        var host = Render();

        await host.Find("gcds-checkboxes[name=Active]").TriggerEventAsync("ongcdschange", new GcdsEventArgs { Value = "true" });

        host.WaitForAssertion(() => Assert.Equal([("true", true)], Options(host.Find("gcds-checkboxes[name=Active]"))));
    }

    private static List<(string Value, bool Checked)> Options(AngleSharp.Dom.IElement element)
    {
        using var json = JsonDocument.Parse(element.GetAttribute("options")!);
        return [.. json.RootElement.EnumerateArray().Select(o => (o.GetProperty("value").GetString()!, o.GetProperty("checked").GetBoolean()))];
    }

    private IRenderedComponent<ProbeFormHost> Render()
    {
        var portal = _context.Services.GetRequiredService<IDataPortal<HintProbe>>();
        var host = _context.Render<ProbeFormHost>(p => p.Add(h => h.Load, () => portal.CreateAsync()));
        host.WaitForElement("gcds-radios");
        return host;
    }
}
