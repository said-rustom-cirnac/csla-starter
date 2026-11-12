using System.Xml.Linq;

namespace CslaStarter.Architecture.Tests;

/// <summary>The references declared in one src project's csproj.</summary>
public sealed class ProjectFile
{
    private ProjectFile(IReadOnlyList<string> projectReferences, IReadOnlyList<string> packageReferences)
    {
        ProjectReferences = projectReferences;
        PackageReferences = packageReferences;
    }

    /// <summary>Referenced project names, without folder or extension.</summary>
    public IReadOnlyList<string> ProjectReferences { get; }

    /// <summary>The Include value of every PackageReference.</summary>
    public IReadOnlyList<string> PackageReferences { get; }

    public static ProjectFile Load(string projectName)
    {
        var document = XDocument.Load(Path.Combine(RepoPaths.Src, projectName, projectName + ".csproj"));

        return new ProjectFile(
            Includes(document, "ProjectReference")
                .Select(include => Path.GetFileNameWithoutExtension(include.Replace('\\', '/')))
                .ToList(),
            Includes(document, "PackageReference").ToList());
    }

    public static IReadOnlyList<string> AllSrcProjectNames() =>
        Directory.GetDirectories(RepoPaths.Src)
            .Select(Path.GetFileName)
            .OfType<string>()
            .Where(name => File.Exists(Path.Combine(RepoPaths.Src, name, name + ".csproj")))
            .ToList();

    private static IEnumerable<string> Includes(XDocument document, string itemName) =>
        document.Descendants(itemName)
            .Select(item => (string?)item.Attribute("Include"))
            .OfType<string>();
}
