using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CslaStarter.Architecture.Tests;

/// <summary>
/// CSLA keeps one authorization rule per (action, property), so a second HasRole for the same pair doesn't add roles:
/// it is ignored or rejected. Put every allowed role in one rule.
/// </summary>
public class AuthorizationRuleTests
{
    [Fact]
    public void AuthorizationRules_OnePerActionAndProperty()
    {
        var duplicates = SourceFiles.CSharp(Path.Combine(RepoPaths.Src, "CslaStarter.Business"))
            .SelectMany(file => CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
            .SelectMany(type => type.DescendantNodes().OfType<ObjectCreationExpressionSyntax>()
                .Where(rule => rule.ArgumentList?.Arguments.FirstOrDefault()?.Expression.ToString().StartsWith("AuthorizationActions.", StringComparison.Ordinal) == true)
                .Select(rule => (Type: type.Identifier.Text, Action: rule.ArgumentList!.Arguments[0].ToString(), Property: Element(rule))))
            .GroupBy(rule => rule)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key.Type}: {group.Key.Action} {group.Key.Property}".TrimEnd())
            .ToList();

        Assert.True(duplicates.Count == 0, "More than one rule for the same action and property (merge their roles): " + string.Join(", ", duplicates));
    }

    private static string Element(ObjectCreationExpressionSyntax rule) =>
        rule.ArgumentList!.Arguments.Skip(1).FirstOrDefault()?.ToString() is { } second && second.EndsWith("Property", StringComparison.Ordinal) ? second : string.Empty;
}
