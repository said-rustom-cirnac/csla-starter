using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CslaStarter.Architecture.Tests;

/// <summary>Turning off the record-level access filter is allowed only in code explicitly marked for it.</summary>
public class RecordAccessTests
{
    [Fact]
    public void RecordAccess_IgnoreQueryFilters_OnlyInBypassCode()
    {
        var offenders = Calls(SourceFiles.CSharp(RepoPaths.Src), "IgnoreQueryFilters").Where(c => !InBypassCode(c.Call)).Select(c => c.Where).ToList();

        Assert.True(offenders.Count == 0, "IgnoreQueryFilters outside a [BypassRecordAccess] method or class: " + string.Join(", ", offenders));
    }

    // Set<T>() can reach tables that are only filtered through their root (project_member), so DALs use the context's
    // named sets instead.
    [Fact]
    public void RecordAccess_DalsUseTheContextsNamedSets()
    {
        var offenders = Calls(SourceFiles.CSharp(Path.Combine(RepoPaths.Src, "CslaStarter.Data", "Dal")), "Set")
            .Where(c => !InBypassCode(c.Call)).Select(c => c.Where).ToList();

        Assert.True(offenders.Count == 0, "Set<T>() in a DAL outside [BypassRecordAccess] code: " + string.Join(", ", offenders));
    }

    private static IEnumerable<(InvocationExpressionSyntax Call, string Where)> Calls(IEnumerable<string> files, string method) =>
        files.SelectMany(file => CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot().DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(call => MethodName(call.Expression) == method)
            .Select(call => (call, $"{RepoPaths.Relative(file)}:{call.GetLocation().GetLineSpan().StartLinePosition.Line + 1}")));

    private static string? MethodName(ExpressionSyntax expression) => expression switch
    {
        MemberAccessExpressionSyntax member => member.Name.Identifier.Text,
        SimpleNameSyntax name => name.Identifier.Text,
        _ => null,
    };

    private static bool InBypassCode(SyntaxNode node) =>
        node.Ancestors().OfType<MemberDeclarationSyntax>()
            .Any(member => member.AttributeLists.SelectMany(list => list.Attributes)
                .Any(attribute => attribute.Name.ToString() is "BypassRecordAccess" or "BypassRecordAccessAttribute"));
}
