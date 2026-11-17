using System.Text.RegularExpressions;

namespace CslaStarter.Architecture.Tests;

/// <summary>Every component keeps markup in .razor and code in .razor.cs, and markup carries no inline styles.</summary>
public partial class RazorConventionTests
{
    // The Csla* component kit and the layout shell. App.razor is the host document, not a component, so like
    // _Imports.razor it has no code-behind.
    private static readonly string ComponentKit = Path.Combine(RepoPaths.Src, "CslaStarter.Web", "Platform", "Components");
    private static readonly string[] NoCodeBehind = ["_Imports.razor", "App.razor"];

    [Fact]
    public void RazorFiles_HaveNoCodeBlocks()
    {
        var offenders = SourceFiles.Razor(RepoPaths.Src)
            .Where(f => CodeBlock().IsMatch(File.ReadAllText(f)))
            .Select(RepoPaths.Relative)
            .ToList();

        Assert.True(offenders.Count == 0, "Move these @code/@functions blocks to a .razor.cs file: " + string.Join(", ", offenders));
    }

    [Fact]
    public void ComponentRazorFiles_HaveCodeBehind()
    {
        var offenders = (Directory.Exists(ComponentKit) ? SourceFiles.Razor(ComponentKit) : [])
            .Where(f => !NoCodeBehind.Contains(Path.GetFileName(f)))
            .Where(f => !File.Exists(f + ".cs"))
            .Select(RepoPaths.Relative)
            .ToList();

        Assert.True(offenders.Count == 0, "Add a .razor.cs code-behind for: " + string.Join(", ", offenders));
    }

    // A style attribute would need 'unsafe-inline' in the content security policy.
    [Fact]
    public void Markup_HasNoInlineStyleAttribute() => Assert.DoesNotContain(
        SourceFiles.Under("src", "*.razor"), f => Regex.IsMatch(File.ReadAllText(f), @"\sstyle\s*="));

    [GeneratedRegex(@"@(code|functions)\s*\{")]
    private static partial Regex CodeBlock();
}
