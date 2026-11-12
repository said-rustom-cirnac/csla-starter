using Csla.Testing;

namespace CslaStarter.Business.Tests.Rules;

public class RuleInspectorTests
{
    [Fact]
    public async Task RuleInspector_FindsRequiredAndMaxLength()
    {
        using var host = CslaTestHost.Create();
        var probe = await host.GetDataPortal<RuleProbe>().CreateAsync();

        Assert.True(probe.PropertyHasRule(nameof(RuleProbe.Name), "Required"));
        Assert.False(probe.PropertyHasRule(nameof(RuleProbe.Note), "Required"));
        Assert.Equal(50, probe.MaxLengthOf(nameof(RuleProbe.Name)));
        Assert.Null(probe.MaxLengthOf(nameof(RuleProbe.Note)));
    }
}
