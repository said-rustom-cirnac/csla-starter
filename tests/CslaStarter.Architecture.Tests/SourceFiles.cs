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

    /// <summary>Files matching <paramref name="pattern"/> under a folder of the repository, skipping build output.</summary>
    /// <param name="repoFolder">The folder relative to the repository root, for example "src".</param>
    /// <param name="pattern">A file name pattern, for example "*.razor".</param>
    public static IEnumerable<string> Under(string repoFolder, string pattern) =>
        Directory.EnumerateFiles(Path.Combine(RepoPaths.Root, repoFolder), pattern, SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path));

    /// <summary>
    /// Every .cs and .razor file under <paramref name="folder"/>, skipping build output and EF migrations.
    /// Folders are judged relative to <paramref name="folder"/>, so any root works, not only the repository.
    /// </summary>
    public static IEnumerable<string> HandWritten(string folder) =>
        Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.Ordinal) || path.EndsWith(".razor", StringComparison.Ordinal))
            .Where(path => !Path.GetRelativePath(folder, path).Split(Path.DirectorySeparatorChar)
                .Intersect(["bin", "obj", "Migrations"]).Any());

    private static bool IsBuildOutput(string path)
    {
        var segments = RepoPaths.Relative(path).Split('/');
        return segments.Contains("bin") || segments.Contains("obj");
    }
}
