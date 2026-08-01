using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace FluentRoslyn.Builders;

/// <summary>
/// The using directives for a generated file: those added explicitly, plus any collected
/// by simplifying qualified type names.
/// </summary>
internal sealed class TypeImports
{
    private readonly SortedSet<string> _explicit = new(UsingOrder.Comparer);
    private bool _simplify;

    internal void Add(string namespaceName)
    {
        if (namespaceName is null) throw new ArgumentNullException(nameof(namespaceName));

        foreach (var level in namespaceName.Split('.'))
            Identifiers.Validate(level);

        _explicit.Add(namespaceName);
    }

    internal void EnableSimplification() => _simplify = true;

    /// <summary>
    /// Applies simplification (if enabled) and prepends the resulting using directives.
    /// </summary>
    internal CompilationUnitSyntax ApplyTo(CompilationUnitSyntax unit, string? currentNamespace)
    {
        var imports = new SortedSet<string>(_explicit, UsingOrder.Comparer);

        if (_simplify)
        {
            var (rewritten, collected) = TypeNameSimplifier.Simplify(unit, currentNamespace);
            unit = rewritten;
            foreach (var name in collected)
                imports.Add(name);
        }

        // An import of the namespace being emitted into is redundant.
        if (currentNamespace is { } current)
            imports.Remove(current);

        return imports.Count == 0
            ? unit
            : unit.WithUsings(List(imports.Select(name => UsingDirective(ParseName(name)))));
    }
}
