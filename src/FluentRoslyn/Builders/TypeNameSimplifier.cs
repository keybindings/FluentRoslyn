using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FluentRoslyn.Builders;

/// <summary>
/// Shortens namespace-qualified type references and reports the namespaces that must be
/// imported to keep the result compiling.
/// </summary>
internal static class TypeNameSimplifier
{
    /// <summary>
    /// Marks a <see cref="QualifiedNameSyntax"/> as "namespace-qualified type", carrying
    /// the namespace as its data. Without this, a syntax-only pass cannot tell
    /// <c>System.Collections.Generic.List</c> (namespace + type) from
    /// <c>Ns.Outer.Inner</c> (namespace + type + nested type) — that needs semantics we
    /// do not have. The builders know the split, so they record it here.
    /// </summary>
    internal const string AnnotationKind = "FluentRoslyn.Namespace";

    internal static SyntaxAnnotation Annotation(string namespaceName)
        => new(AnnotationKind, namespaceName);

    /// <summary>
    /// Rewrites unambiguous qualified type names to their simple form.
    /// </summary>
    /// <param name="unit">The compilation unit to rewrite.</param>
    /// <param name="currentNamespace">
    /// The namespace being emitted into; references to it are shortened without needing
    /// an import. Null for the global namespace.
    /// </param>
    internal static (CompilationUnitSyntax Unit, IReadOnlyCollection<string> Imports) Simplify(
        CompilationUnitSyntax unit,
        string? currentNamespace)
    {
        var candidates = unit.GetAnnotatedNodes(AnnotationKind).OfType<QualifiedNameSyntax>().ToList();
        if (candidates.Count == 0)
            return (unit, []);

        // A simple name that this file itself declares would be shadowed by the import,
        // so those stay fully qualified too.
        var declared = DeclaredTypeNames(unit);

        var simplifiable = new List<QualifiedNameSyntax>();
        var imports = new SortedSet<string>(UsingOrder.Comparer);

        foreach (var group in candidates.GroupBy(SimpleName))
        {
            var namespaces = group.Select(NamespaceOf).Distinct().ToList();

            // Two namespaces offering the same simple name would be ambiguous once
            // imported, and a name the file declares itself would be shadowed.
            if (namespaces.Count != 1 || declared.Contains(group.Key))
                continue;

            simplifiable.AddRange(group);

            if (namespaces[0] != currentNamespace)
                imports.Add(namespaces[0]);
        }

        if (simplifiable.Count == 0)
            return (unit, []);

        var rewritten = (CompilationUnitSyntax)unit.ReplaceNodes(
            simplifiable,
            (original, _) => ((QualifiedNameSyntax)original).Right);

        return (rewritten, imports);
    }

    private static string SimpleName(QualifiedNameSyntax qualified)
        => qualified.Right.Identifier.ValueText;

    private static string NamespaceOf(QualifiedNameSyntax qualified)
        => qualified.GetAnnotations(AnnotationKind).First().Data ?? string.Empty;

    private static HashSet<string> DeclaredTypeNames(CompilationUnitSyntax unit)
        => new(
            unit.DescendantNodes().OfType<BaseTypeDeclarationSyntax>().Select(t => t.Identifier.ValueText),
            System.StringComparer.Ordinal);
}
