using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CslaStarter.Architecture.Tests;

/// <summary>
/// Every CSLA class reads the same way: const codes, property registrations, properties, AddBusinessRules, object
/// authorization rules, data portal operations. Property bodies only call Get/Set/Load/ReadProperty.
/// </summary>
public class BusinessLayoutTests
{
    private static readonly string[] DataPortalAttributes =
        ["Create", "Fetch", "Insert", "Update", "DeleteSelf", "Delete", "Execute", "CreateChild", "FetchChild", "InsertChild", "UpdateChild", "DeleteSelfChild"];

    private static readonly string[] PropertyMethods = ["GetProperty", "SetProperty", "LoadProperty", "ReadProperty"];

    [Fact]
    public void BusinessClasses_FollowTheLayoutOrder()
    {
        var problems = new List<string>();
        foreach (var (file, type) in BusinessClasses())
        {
            var last = (Group: -1, Member: string.Empty);
            foreach (var member in type.Members)
            {
                var group = GroupOf(member);
                if (group is null)
                {
                    continue;
                }

                if (group < last.Group)
                {
                    problems.Add($"{file}: {type.Identifier} — {Describe(member)} comes after {last.Member}");
                }

                last = (Math.Max(last.Group, group.Value), Describe(member));
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void RegisteredProperties_OnlyUsePropertyMethods()
    {
        var problems = new List<string>();
        foreach (var (file, type) in BusinessClasses())
        {
            foreach (var property in type.Members.OfType<PropertyDeclarationSyntax>().Where(p => p.ToString().Contains("Property)", StringComparison.Ordinal)))
            {
                var bodies = property.ExpressionBody is { } arrow
                    ? [arrow.Expression]
                    : (property.AccessorList?.Accessors ?? []).Select(a => a.ExpressionBody?.Expression ?? (a.Body?.Statements.SingleOrDefault() as ReturnStatementSyntax)?.Expression);
                if (bodies.Any(body => !IsPropertyMethodCall(body)))
                {
                    problems.Add($"{file}: {type.Identifier}.{property.Identifier} does more than call Get/Set/Load/ReadProperty");
                }
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    private static bool IsPropertyMethodCall(ExpressionSyntax? body)
    {
        while (body is PostfixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.SuppressNullableWarningExpression } bang)
        {
            body = bang.Operand;
        }

        return body is InvocationExpressionSyntax { Expression: IdentifierNameSyntax name } && PropertyMethods.Contains(name.Identifier.Text);
    }

    // 0 const codes · 1 registrations · 2 properties · 3 AddBusinessRules · 4 object authorization · 5 data portal operations
    private static int? GroupOf(MemberDeclarationSyntax member) => member switch
    {
        FieldDeclarationSyntax f when f.Modifiers.Any(SyntaxKind.ConstKeyword) => 0,
        FieldDeclarationSyntax f when f.ToString().Contains("RegisterProperty", StringComparison.Ordinal) => 1,
        PropertyDeclarationSyntax p when p.ToString().Contains("Property)", StringComparison.Ordinal) => 2,
        MethodDeclarationSyntax { Identifier.Text: "AddBusinessRules" } => 3,
        MethodDeclarationSyntax m when HasAttribute(m, "ObjectAuthorizationRules") => 4,
        MethodDeclarationSyntax m when DataPortalAttributes.Any(a => HasAttribute(m, a)) => 5,
        _ => null,
    };

    private static bool HasAttribute(MemberDeclarationSyntax member, string name) =>
        member.AttributeLists.SelectMany(l => l.Attributes).Any(a => a.Name.ToString() == name);

    private static string Describe(MemberDeclarationSyntax member) => member switch
    {
        FieldDeclarationSyntax f => f.Declaration.Variables.First().Identifier.Text,
        PropertyDeclarationSyntax p => p.Identifier.Text,
        MethodDeclarationSyntax m => m.Identifier.Text,
        _ => member.Kind().ToString(),
    };

    private static IEnumerable<(string File, ClassDeclarationSyntax Type)> BusinessClasses() =>
        Directory.EnumerateFiles(Path.Combine(RepoPaths.Src, "CslaStarter.Business"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !RepoPaths.Relative(f).Split('/').Intersect(["bin", "obj"]).Any())
            .SelectMany(f => CSharpSyntaxTree.ParseText(File.ReadAllText(f)).GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
                .Select(c => (RepoPaths.Relative(f), c)));
}
