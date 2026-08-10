using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FluentRoslyn.UnitTests;

/// <summary>
/// Compiles generated source, so a test can assert what it <em>binds to</em> rather than
/// what it looks like.
/// </summary>
/// <remarks>
/// The failure this library exists to prevent is source that is syntactically fine and
/// still wrong, and a <c>ToString()</c> assertion cannot see it: a reference that shortens
/// into the wrong type compiles clean and reads correctly. Supporting sources stand in for
/// the consumer's own types, which a generator's output is normally compiled against.
/// </remarks>
internal static class Compiled
{
    private static readonly Lazy<MetadataReference[]> References = new(() =>
        (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? string.Empty)
        .Split(Path.PathSeparator)
        .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
        .ToArray());

    /// <summary>The compilation errors in <paramref name="generated"/>, if any.</summary>
    internal static IReadOnlyList<string> Errors(string generated, params string[] supporting)
        => Create(generated, supporting)
            .GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => $"{d.Id} {d.GetMessage()}")
            .ToArray();

    /// <summary>
    /// The fully qualified type of a member, as the compiler resolved it. This is the
    /// assertion a string cannot make: two spellings that read the same can bind to
    /// different types.
    /// </summary>
    internal static string MemberType(
        string generated, string typeName, string memberName, params string[] supporting)
    {
        var compilation = Create(generated, supporting);

        var type = compilation.GetSymbolsWithName(typeName, SymbolFilter.Type)
            .OfType<INamedTypeSymbol>()
            .Single();

        return type.GetMembers(memberName).Single() switch
        {
            IPropertySymbol property => property.Type.ToDisplayString(),
            IFieldSymbol field => field.Type.ToDisplayString(),
            var other => throw new InvalidOperationException($"'{memberName}' is a {other.Kind}."),
        };
    }

    /// <summary>
    /// The compile-time constant value of the expression <paramref name="select"/> locates in
    /// <paramref name="generated"/>, as the compiler folded it — not the source text. This is
    /// the assertion a string cannot make: source that reads as the right value can still
    /// evaluate to a different one (a dropped sign on a negative-zero literal, a member that
    /// resolves to the wrong overload), and a substring check on the source text cannot see it.
    /// </summary>
    internal static object ConstantValueOf(
        string generated, Func<SyntaxNode, ExpressionSyntax> select, params string[] supporting)
    {
        var compilation = Create(generated, supporting);
        var tree = compilation.SyntaxTrees.First();
        var model = compilation.GetSemanticModel(tree);

        var node = tree.GetRoot().DescendantNodes()
            .Select(select)
            .First(e => e is not null);

        var constant = model.GetConstantValue(node);
        return constant.HasValue
            ? constant.Value
            : throw new InvalidOperationException($"'{node}' is not a compile-time constant.");
    }

    private static CSharpCompilation Create(string generated, string[] supporting)
        => CSharpCompilation.Create(
            "Generated",
            supporting.Prepend(generated).Select(source => CSharpSyntaxTree.ParseText(source)),
            References.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
}
