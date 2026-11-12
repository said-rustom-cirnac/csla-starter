namespace CslaStarter.Architecture.Tests;

/// <summary>Locates the repository from the test output folder.</summary>
public static class RepoPaths
{
    public static string Root { get; } = FindRoot();

    public static string Src => Path.Combine(Root, "src");

    public static string Tests => Path.Combine(Root, "tests");

    public static string Relative(string path) => Path.GetRelativePath(Root, path).Replace('\\', '/');

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CslaStarter.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName
            ?? throw new InvalidOperationException($"CslaStarter.slnx not found above {AppContext.BaseDirectory}.");
    }
}
