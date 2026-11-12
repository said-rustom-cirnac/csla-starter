using System.Text.RegularExpressions;

namespace CslaStarter.Architecture.Tests;

/// <summary>Every component keeps markup in .razor and code in .razor.cs.</summary>
public partial class RazorConventionTests
{
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
        var offenders = SourceFiles.Razor(Path.Combine(RepoPaths.Src, "CslaStarter.Components"))
            .Where(f => Path.GetFileName(f) != "_Imports.razor")
            .Where(f => !File.Exists(f + ".cs"))
            .Select(RepoPaths.Relative)
            .ToList();

        Assert.True(offenders.Count == 0, "Add a .razor.cs code-behind for: " + string.Join(", ", offenders));
    }

    [GeneratedRegex(@"@(code|functions)\s*\{")]
    private static partial Regex CodeBlock();
}
