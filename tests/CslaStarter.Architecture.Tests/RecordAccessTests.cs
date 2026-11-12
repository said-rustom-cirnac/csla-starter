namespace CslaStarter.Architecture.Tests;

/// <summary>Turning off the record-level access filter is allowed only in code explicitly marked for it.</summary>
public class RecordAccessTests
{
    [Fact]
    public void RecordAccess_IgnoreQueryFilters_OnlyInBypassCode()
    {
        var offenders = Directory.EnumerateFiles(RepoPaths.Src, "*.cs", SearchOption.AllDirectories)
            .Where(f => !RepoPaths.Relative(f).Split('/').Intersect(["bin", "obj"]).Any())
            .Where(f => CodeLines(f).Any(line => line.Contains("IgnoreQueryFilters", StringComparison.Ordinal)))
            .Where(f => !File.ReadAllText(f).Contains("[BypassRecordAccess", StringComparison.Ordinal))
            .Select(RepoPaths.Relative)
            .ToList();

        Assert.True(offenders.Count == 0, "IgnoreQueryFilters outside [BypassRecordAccess] code: " + string.Join(", ", offenders));
    }

    private static IEnumerable<string> CodeLines(string file) =>
        File.ReadLines(file).Select(line => line.Trim()).Where(line => !line.StartsWith("//", StringComparison.Ordinal));
}
