namespace CslaStarter.Architecture.Tests;

/// <summary>Every data change must go through SaveChanges so the audit interceptor records it.</summary>
public class DataLayerTests
{
    private static readonly string[] AuditBypasses = ["ExecuteUpdate", "ExecuteDelete", "ExecuteSql", "FromSql", "SqlQuery"];

    [Fact]
    public void DataLayer_DoesNotBypassAuditing()
    {
        var offenders = Directory.EnumerateFiles(Path.Combine(RepoPaths.Src, "CslaStarter.Data"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !RepoPaths.Relative(f).Split('/').Intersect(["bin", "obj", "Migrations"]).Any())
            .SelectMany(f => AuditBypasses.Where(File.ReadAllText(f).Contains).Select(call => $"{RepoPaths.Relative(f)}: {call}"))
            .ToList();

        Assert.True(offenders.Count == 0, "These bypass the audit interceptor: " + string.Join(", ", offenders));
    }
}
