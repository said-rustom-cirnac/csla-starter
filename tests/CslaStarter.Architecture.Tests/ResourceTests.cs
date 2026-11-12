using System.Xml.Linq;

namespace CslaStarter.Architecture.Tests;

/// <summary>Every text resource exists, non-empty, in English and French (Official Languages Act).</summary>
public class ResourceTests
{
    [Fact]
    public void Resources_HaveEveryKeyInBothLanguages()
    {
        var problems = new List<string>();
        foreach (var neutral in NeutralResourceFiles())
        {
            var french = neutral[..^".resx".Length] + ".fr.resx";
            if (!File.Exists(french))
            {
                problems.Add($"{RepoPaths.Relative(neutral)}: missing .fr.resx");
                continue;
            }

            var english = Entries(neutral);
            var frenchEntries = Entries(french);
            problems.AddRange(english.Keys.Except(frenchEntries.Keys).Select(k => $"{RepoPaths.Relative(french)}: missing {k}"));
            problems.AddRange(frenchEntries.Keys.Except(english.Keys).Select(k => $"{RepoPaths.Relative(neutral)}: missing {k}"));
            problems.AddRange(english.Concat(frenchEntries)
                .Where(e => string.IsNullOrWhiteSpace(e.Value))
                .Select(e => $"empty value: {e.Key}"));
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void Resources_AreNotEmpty()
    {
        var labels = Path.Combine(RepoPaths.Src, "CslaStarter.Core", "Resources", "Labels.resx");
        Assert.True(File.Exists(labels), "Labels.resx is missing");
        Assert.NotEmpty(Entries(labels));
    }

    private static IEnumerable<string> NeutralResourceFiles() =>
        Directory.EnumerateFiles(RepoPaths.Src, "*.resx", SearchOption.AllDirectories)
            .Where(f => !RepoPaths.Relative(f).Split('/').Intersect(["bin", "obj"]).Any())
            .Where(f => Path.GetFileNameWithoutExtension(f).IndexOf('.', StringComparison.Ordinal) < 0);

    private static Dictionary<string, string> Entries(string path) =>
        XDocument.Load(path).Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? string.Empty, StringComparer.Ordinal);
}
