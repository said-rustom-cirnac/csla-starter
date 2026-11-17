using System.Text.RegularExpressions;

namespace CslaStarter.Architecture.Tests;

/// <summary>
/// Source scan that keeps the boundaries between Platform and Features and between the projects.
/// Each rule is its own method; <see cref="Check"/> runs them over every file.
/// </summary>
public static partial class Boundaries
{
    private const string DataPlatformFolder = "src/CslaStarter.Data/Platform/";
    private static readonly string[] PageAndComponentFolders =
    [
        "src/CslaStarter.Web/Platform/Components/",
        "src/CslaStarter.Web/Platform/Pages/",
        "src/CslaStarter.Web/Features/",
    ];

    /// <summary>One entry per violation: "relative path: rule". Empty when the root has no src folder.</summary>
    public static IReadOnlyList<string> Check(string repoRoot)
    {
        var src = Path.Combine(repoRoot, "src");
        if (!Directory.Exists(src))
        {
            return [];
        }

        var violations = new List<string>();
        foreach (var file in SourceFiles.HandWritten(src).Order(StringComparer.Ordinal))
        {
            var path = Path.GetRelativePath(repoRoot, file).Replace('\\', '/');
            var text = File.ReadAllText(file);
            foreach (var rule in Rules(path, text))
            {
                violations.Add($"{path}: {rule}");
            }
        }

        return violations;
    }

    private static IEnumerable<string> Rules(string path, string text)
    {
        if (PlatformUsesFeatures(path, text))
        {
            yield return "Platform must not use Features";
        }

        if (FeatureUsesOtherFeatureData(path, text))
        {
            yield return "a feature must not use another feature's data";
        }

        if (PageOrComponentUsesData(path, text))
        {
            yield return "pages and components must not use Data";
        }

        if (!path.StartsWith(DataPlatformFolder, StringComparison.Ordinal))
        {
            if (SaveChangesCall().IsMatch(text))
            {
                yield return "only Platform may call SaveChanges";
            }

            if (IgnoreQueryFiltersCall().IsMatch(text))
            {
                yield return "only Platform may call IgnoreQueryFilters";
            }

            if (RawSqlCall().IsMatch(text))
            {
                yield return "only Platform may run raw SQL";
            }
        }
    }

    private static bool PlatformUsesFeatures(string path, string text) =>
        path.Contains("/Platform/", StringComparison.Ordinal) && FeaturesNamespace().IsMatch(text);

    private static bool FeatureUsesOtherFeatureData(string path, string text)
    {
        const string folder = "src/CslaStarter.Data/Features/";
        if (!path.StartsWith(folder, StringComparison.Ordinal))
        {
            return false;
        }

        var own = path[folder.Length..].Split('/')[0];
        return DataFeatureNamespace().Matches(text).Any(m => m.Groups[1].Value != own);
    }

    private static bool PageOrComponentUsesData(string path, string text) =>
        PageAndComponentFolders.Any(folder => path.StartsWith(folder, StringComparison.Ordinal))
        && DataNamespace().IsMatch(text);

    [GeneratedRegex(@"\bCslaStarter\.\w+\.Features\b")]
    private static partial Regex FeaturesNamespace();

    [GeneratedRegex(@"\bCslaStarter\.Data\.Features\.(\w+)")]
    private static partial Regex DataFeatureNamespace();

    [GeneratedRegex(@"\bCslaStarter\.Data\b")]
    private static partial Regex DataNamespace();

    [GeneratedRegex(@"\bSaveChanges(Async)?\s*\(")]
    private static partial Regex SaveChangesCall();

    [GeneratedRegex(@"\bIgnoreQueryFilters\s*\(")]
    private static partial Regex IgnoreQueryFiltersCall();

    [GeneratedRegex(@"\b(ExecuteSqlRaw|ExecuteSqlInterpolated|ExecuteSql|FromSqlRaw|FromSqlInterpolated|FromSql|SqlQueryRaw|SqlQuery)(Async)?\s*(<[^>]*>)?\s*\(")]
    private static partial Regex RawSqlCall();
}
