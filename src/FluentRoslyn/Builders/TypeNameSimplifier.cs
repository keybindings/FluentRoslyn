using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FluentRoslyn.Builders;

/// <summary>
/// Shortens namespace-qualified type references and reports the namespaces that must be
/// imported to keep the result compiling.
/// </summary>
/// <remarks>
/// This is a syntax-only pass: it can see what the file says and nothing else. Everything
/// it refuses is refused because the file itself shows the shortened name would bind
/// somewhere other than intended — never on a guess about what a namespace contains. The
/// one place that ceiling still bites is an explicit <c>WithUsing</c> of a namespace this
/// file never names a type from; see <see cref="NamesOfferedBy"/>.
/// </remarks>
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
    /// <param name="explicitImports">
    /// Namespaces the caller imported with <c>WithUsing</c>. They are in scope for the
    /// emitted file whether the simplifier asked for them or not, so they have to be part
    /// of the analysis rather than merged in afterwards.
    /// </param>
    internal static (CompilationUnitSyntax Unit, IReadOnlyCollection<string> Imports) Simplify(
        CompilationUnitSyntax unit,
        string? currentNamespace,
        IReadOnlyCollection<string> explicitImports)
    {
        var candidates = unit.GetAnnotatedNodes(AnnotationKind).OfType<QualifiedNameSyntax>().ToList();
        if (candidates.Count == 0)
            return (unit, []);

        var declared = DeclaredNames.Of(unit);
        var namespaceNames = VisibleNamespaceNames(candidates, explicitImports, currentNamespace);
        var offered = NamesOfferedBy(unit, explicitImports);

        var simplifiable = new List<QualifiedNameSyntax>();
        var imports = new SortedSet<string>(UsingOrder.Comparer);

        foreach (var group in candidates.GroupBy(SimpleName))
        {
            var namespaces = group.Select(NamespaceOf).Distinct().ToList();

            // Two namespaces offering the same simple name would be ambiguous once
            // imported.
            if (namespaces.Count != 1)
                continue;

            if (!CanShorten(group.Key, namespaces[0], currentNamespace, declared, namespaceNames, offered))
                continue;

            simplifiable.AddRange(group);

            if (namespaces[0] != currentNamespace)
                imports.Add(namespaces[0]);
        }

        if (simplifiable.Count == 0)
            return (unit, []);

        // The replacement is taken from the REWRITTEN node, not the original: a generic's
        // type arguments are descendants of its own qualified name, so reading the
        // original discards the simplification already applied to them -- leaving the
        // argument fully qualified while its import was still recorded, and an unused
        // import is how an unrelated name becomes ambiguous (CS0104).
        var rewritten = (CompilationUnitSyntax)unit.ReplaceNodes(
            simplifiable,
            (_, replaced) => ((QualifiedNameSyntax)replaced).Right);

        return (rewritten, imports);
    }

    private static bool CanShorten(
        string simpleName,
        string @namespace,
        string? currentNamespace,
        DeclaredNames declared,
        ISet<string> namespaceNames,
        IReadOnlyDictionary<string, string> offered)
    {
        // A namespace of the same name wins over the type, and the reference then names a
        // namespace where a type is required (CS0118).
        if (namespaceNames.Contains(simpleName))
            return false;

        // A type parameter shadows the name inside every member of its declaration, and a
        // syntax-only pass cannot show that the reference sits outside that scope. Same
        // for a nested type, which shadows throughout its declaring type.
        if (declared.TypeParameters.Contains(simpleName) || declared.Nested.Contains(simpleName))
            return false;

        // A top-level declaration in this file shadows the import -- unless the reference
        // IS that declaration, which is the case a same-file reference always is: it needs
        // no import at all, and shortening it can only bind to the type meant.
        if (declared.TopLevel.Contains(simpleName) && @namespace != currentNamespace)
            return false;

        // An explicitly imported namespace that this file shows holds the same simple name
        // makes the shortened form ambiguous (CS0104).
        return !offered.TryGetValue(simpleName, out var from) || from == @namespace;
    }

    private static string SimpleName(QualifiedNameSyntax qualified)
        => qualified.Right.Identifier.ValueText;

    private static string NamespaceOf(QualifiedNameSyntax qualified)
        => qualified.GetAnnotations(AnnotationKind).First().Data ?? string.Empty;

    /// <summary>
    /// The namespace names that resolve as bare identifiers in the emitted file, and so
    /// cannot be produced by shortening a type without turning it into CS0118.
    /// </summary>
    /// <remarks>
    /// A namespace is visible by its simple name from inside the namespaces that
    /// lexically contain it: the global one, so every root segment is always visible, and
    /// each namespace enclosing the one being emitted into — which is where the file's own
    /// name, its parents, and its siblings come from. Measured: an import is *not* such a
    /// scope. <c>using System;</c> does not make <c>Text</c> mean <c>System.Text</c>,
    /// because a using-directive imports a namespace's types and not its nested
    /// namespaces. What an import does contribute is its root segment, which is visible
    /// from the global namespace like any other.
    /// </remarks>
    private static ISet<string> VisibleNamespaceNames(
        IEnumerable<QualifiedNameSyntax> candidates,
        IEnumerable<string> explicitImports,
        string? currentNamespace)
    {
        var known = new HashSet<string>(candidates.Select(NamespaceOf), StringComparer.Ordinal);
        known.UnionWith(explicitImports);
        if (currentNamespace is not null)
            known.Add(currentNamespace);

        var scopes = new HashSet<string>(StringComparer.Ordinal) { string.Empty };
        if (currentNamespace is not null)
            foreach (var enclosing in Prefixes(currentNamespace))
                scopes.Add(enclosing);

        var visible = new HashSet<string>(StringComparer.Ordinal);
        foreach (var scope in scopes)
        foreach (var @namespace in known)
            if (SegmentUnder(@namespace, scope) is { } segment)
                visible.Add(segment);

        return visible;
    }

    /// <summary>Every enclosing namespace of a dotted name, itself included.</summary>
    private static IEnumerable<string> Prefixes(string namespaceName)
    {
        for (var dot = namespaceName.IndexOf('.'); dot >= 0; dot = namespaceName.IndexOf('.', dot + 1))
            yield return namespaceName.Substring(0, dot);

        yield return namespaceName;
    }

    /// <summary>
    /// The bare name <paramref name="namespaceName"/> goes by inside <paramref name="scope"/>,
    /// or null when it is not under it. The global scope is the empty string, and every
    /// namespace's first segment is under it.
    /// </summary>
    private static string? SegmentUnder(string namespaceName, string scope)
    {
        if (namespaceName.Length == 0)
            return null;

        if (scope.Length == 0)
            return FirstSegment(namespaceName);

        if (namespaceName.Length <= scope.Length + 1
            || !namespaceName.StartsWith(scope, StringComparison.Ordinal)
            || namespaceName[scope.Length] != '.')
            return null;

        return FirstSegment(namespaceName.Substring(scope.Length + 1));
    }

    private static string FirstSegment(string namespaceName)
    {
        var dot = namespaceName.IndexOf('.');
        return dot < 0 ? namespaceName : namespaceName.Substring(0, dot);
    }

    /// <summary>
    /// For each explicitly imported namespace, the simple names this file shows it to
    /// hold — read off every qualified name in the unit, annotated or not, so the raw-text
    /// escape hatches count too.
    /// </summary>
    /// <remarks>
    /// This is the honest limit of a syntax-only pass: what an imported namespace holds is
    /// knowable only where the file names it. A <c>WithUsing</c> of a namespace the file
    /// never qualifies anything by contributes nothing, and a collision with a type in it
    /// cannot be seen from here.
    /// </remarks>
    private static IReadOnlyDictionary<string, string> NamesOfferedBy(
        CompilationUnitSyntax unit,
        IReadOnlyCollection<string> explicitImports)
    {
        var offered = new Dictionary<string, string>(StringComparer.Ordinal);
        if (explicitImports.Count == 0)
            return offered;

        var imported = new HashSet<string>(explicitImports, StringComparer.Ordinal);

        foreach (var qualified in unit.DescendantNodes().OfType<QualifiedNameSyntax>())
            if (DottedText(qualified.Left) is { } left && imported.Contains(left))
                offered[qualified.Right.Identifier.ValueText] = left;

        return offered;
    }

    // Null for anything that is not a plain dotted path -- a generic or alias-qualified
    // segment is not a namespace, so it cannot be one of the imports.
    private static string? DottedText(NameSyntax name)
        => name switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            QualifiedNameSyntax qualified when qualified.Right is IdentifierNameSyntax right
                                               && DottedText(qualified.Left) is { } left
                => left + "." + right.Identifier.ValueText,
            _ => null,
        };

    /// <summary>
    /// The names this file declares, split by how far they shadow. Types are not the whole
    /// set: a delegate declaration and a type parameter occupy the same name space and
    /// shadow an import just as completely.
    /// </summary>
    private readonly struct DeclaredNames
    {
        private DeclaredNames(HashSet<string> topLevel, HashSet<string> nested, HashSet<string> typeParameters)
        {
            TopLevel = topLevel;
            Nested = nested;
            TypeParameters = typeParameters;
        }

        internal HashSet<string> TopLevel { get; }
        internal HashSet<string> Nested { get; }
        internal HashSet<string> TypeParameters { get; }

        internal static DeclaredNames Of(CompilationUnitSyntax unit)
        {
            var topLevel = new HashSet<string>(StringComparer.Ordinal);
            var nested = new HashSet<string>(StringComparer.Ordinal);

            foreach (var node in unit.DescendantNodes())
            {
                var name = node switch
                {
                    BaseTypeDeclarationSyntax type => type.Identifier.ValueText,
                    DelegateDeclarationSyntax @delegate => @delegate.Identifier.ValueText,
                    _ => null,
                };

                if (name is null)
                    continue;

                // A nested declaration shadows only inside its declaring type, but that is
                // still everywhere a member of this file can reference anything, so it is
                // the stricter of the two.
                (node.Parent is TypeDeclarationSyntax ? nested : topLevel).Add(name);
            }

            var typeParameters = new HashSet<string>(
                unit.DescendantNodes().OfType<TypeParameterSyntax>().Select(p => p.Identifier.ValueText),
                StringComparer.Ordinal);

            return new DeclaredNames(topLevel, nested, typeParameters);
        }
    }
}
