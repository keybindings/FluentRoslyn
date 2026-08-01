using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace FluentRoslyn.Builders;

internal static class SyntaxGenerics
{
    /// <summary>The <c>&lt;T, U&gt;</c> type-parameter list, or null when there are none.</summary>
    internal static TypeParameterListSyntax? TypeParameterList(IReadOnlyList<string> typeParameters)
        => typeParameters.Count == 0
            ? null
            : SyntaxFactory.TypeParameterList(SeparatedList(typeParameters.Select(t => TypeParameter(Identifier(t)))));

    /// <summary>
    /// One <c>where</c> clause per constrained type parameter, in type-parameter
    /// declaration order.
    /// </summary>
    internal static SyntaxList<TypeParameterConstraintClauseSyntax> ConstraintClauses(
        IReadOnlyList<string> typeParameters,
        IReadOnlyDictionary<string, List<string>> constraints)
        => List(typeParameters
            .Where(constraints.ContainsKey)
            .Select(t => TypeParameterConstraintClause(IdentifierName(t))
                .WithConstraints(SeparatedList(Ordered(constraints[t]).Select(Constraint)))));

    // C# requires a specific constraint order: the primary constraint (class/struct/
    // notnull/unmanaged) first, then interface/type constraints, then new() last.
    // Callers add constraints in any order, so normalize here rather than emit invalid
    // clauses like `where T : new(), class`.
    private static IEnumerable<string> Ordered(IEnumerable<string> constraints)
        => constraints.OrderBy(Rank);

    private static int Rank(string constraint)
        => constraint.Trim() switch
        {
            "class" or "struct" or "notnull" or "unmanaged" => 0,
            "new()" => 2,
            _ => 1,
        };

    /// <summary>Validates that every constraint names a declared type parameter.</summary>
    internal static void Validate(
        string owner,
        IReadOnlyList<string> typeParameters,
        IReadOnlyDictionary<string, List<string>> constraints)
    {
        if (typeParameters.Count == 0 && constraints.Count > 0)
            throw new InvalidOperationException($"{owner} has constraints but no type parameters.");

        var undeclared = constraints.Keys.FirstOrDefault(k => !typeParameters.Contains(k));
        if (undeclared is not null)
            throw new InvalidOperationException($"{owner} constrains undeclared type parameter '{undeclared}'.");
    }

    private static TypeParameterConstraintSyntax Constraint(string constraint)
        => constraint.Trim() switch
        {
            "class" => ClassOrStructConstraint(SyntaxKind.ClassConstraint),
            "struct" => ClassOrStructConstraint(SyntaxKind.StructConstraint),
            "new()" => ConstructorConstraint(),
            var other => TypeConstraint(SyntaxParse.TypeName(other)),
        };
}
