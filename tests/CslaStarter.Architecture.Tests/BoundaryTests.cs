using CslaStarter.Data;
using CslaStarter.Domain;

namespace CslaStarter.Architecture.Tests;

public sealed class BoundaryTests
{
    [Theory]
    [InlineData("src/CslaStarter.Domain/Platform/A.cs", "using CslaStarter.Domain.Features.Projects;", "Platform must not use Features")]
    [InlineData("src/CslaStarter.Web/Platform/Pages/A.razor.cs", "using CslaStarter.Web.Features.Projects;", "Platform must not use Features")]
    [InlineData("src/CslaStarter.Data/Features/Projects/A.cs", "await db.SaveChangesAsync();", "only Platform may call SaveChanges")]
    [InlineData("src/CslaStarter.Data/Features/Projects/A.cs", "query.IgnoreQueryFilters()", "only Platform may call IgnoreQueryFilters")]
    [InlineData("src/CslaStarter.Data/Features/Projects/A.cs", "db.Database.ExecuteSqlRaw(\"x\")", "only Platform may run raw SQL")]
    [InlineData("src/CslaStarter.Data/Features/Projects/A.cs", "using CslaStarter.Data.Features.Contracts;", "a feature must not use another feature's data")]
    [InlineData("src/CslaStarter.Web/Features/Projects/P.razor.cs", "using CslaStarter.Data;", "pages and components must not use Data")]
    [InlineData("src/CslaStarter.Web/Platform/Components/C.razor.cs", "using CslaStarter.Data.Platform;", "pages and components must not use Data")]
    [InlineData("src/CslaStarter.Web/Platform/Pages/P.razor", "@using CslaStarter.Data", "pages and components must not use Data")]
    public void Check_ReportsTheViolation(string path, string line, string rule) =>
        Assert.Contains($"{path}: {rule}", Violation(path, line));

    [Fact]
    public void Check_AllowsHostCodeToUseData() =>
        Assert.Empty(Violation("src/CslaStarter.Web/Platform/Startup/A.cs", "using CslaStarter.Data;"));

    [Fact]
    public void Check_AllowsLegalCode()
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        try
        {
            Write(root, "src/CslaStarter.Data/Platform/A.cs", "await db.SaveChangesAsync();");
            Write(root, "src/CslaStarter.Data/Features/Projects/A.cs",
                "using CslaStarter.Data.Features.Projects;\nusing CslaStarter.Domain.Features.Contracts;");
            Write(root, "src/CslaStarter.Web/Features/Projects/P.razor.cs", "using CslaStarter.Domain.Features.Projects;");
            Assert.Empty(Boundaries.Check(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Repository_HasNoBoundaryViolations() => Assert.Empty(Boundaries.Check(RepoPaths.Root));

    [Fact]
    public void Domain_ReferencesNoDataOrWebAssembly() => Assert.DoesNotContain(
        typeof(DomainAssembly).Assembly.GetReferencedAssemblies(),
        a => a.Name!.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) || a.Name.StartsWith("Npgsql", StringComparison.Ordinal) || a.Name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));

    // "Csla" and "Csla.*" only: Data references CslaStarter.Domain, whose name also starts with "Csla".
    [Fact]
    public void Data_DoesNotReferenceCsla() => Assert.DoesNotContain(
        typeof(DataAssembly).Assembly.GetReferencedAssemblies(),
        a => a.Name is "Csla" || a.Name!.StartsWith("Csla.", StringComparison.Ordinal));

    private static IReadOnlyList<string> Violation(string path, string line)
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        try
        {
            Write(root, path, line);
            return Boundaries.Check(root);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void Write(string root, string path, string content)
    {
        var full = Path.Combine(root, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }
}
