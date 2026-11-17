using System.Runtime.CompilerServices;

namespace CslaStarter.Architecture.Tests;

/// <summary>Locates the repository from the test output folder.</summary>
public static class RepoPaths
{
    public static string Root { get; } = FindRoot();

    public static string Src => Path.Combine(Root, "src");

    public static string Tests => Path.Combine(Root, "tests");

    public static string Relative(string path) => Path.GetRelativePath(Root, path).Replace('\\', '/');

    // The build output can sit outside the repository (ArtifactsPath), so fall back to this source file's folder.
    private static string FindRoot([CallerFilePath] string sourceFile = "") =>
        Search(AppContext.BaseDirectory)
            ?? Search(Path.GetDirectoryName(sourceFile))
            ?? throw new InvalidOperationException($"CslaStarter.slnx not found above {AppContext.BaseDirectory} or {sourceFile}.");

    private static string? Search(string? start)
    {
        var dir = start is null ? null : new DirectoryInfo(start);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CslaStarter.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName;
    }
}
