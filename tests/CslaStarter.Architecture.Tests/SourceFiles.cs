namespace CslaStarter.Architecture.Tests;

/// <summary>Source files under a folder, skipping build output.</summary>
public static class SourceFiles
{
    public static IEnumerable<string> Razor(string folder) =>
        Directory.EnumerateFiles(folder, "*.razor", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path));

    public static IEnumerable<string> CSharp(string folder) =>
        Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path));

    private static bool IsBuildOutput(string path)
    {
        var segments = RepoPaths.Relative(path).Split('/');
        return segments.Contains("bin") || segments.Contains("obj");
    }
}
