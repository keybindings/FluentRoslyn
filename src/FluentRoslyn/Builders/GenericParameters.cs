using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FluentRoslyn.Builders;

/// <summary>
/// Holds a declaration's generic type parameters and their constraints, and applies
/// them to a syntax node. Shared by every builder that can be generic (types and
/// methods) so the storage, guards, and emission live in one place.
/// </summary>
internal sealed class GenericParameters
{
    private readonly List<string> _typeParameters = [];
    private readonly Dictionary<string, List<string>> _constraints = [];

    internal void AddTypeParameter(string name)
        => _typeParameters.Add(name ?? throw new ArgumentNullException(nameof(name)));

    internal void AddConstraint(string typeParameter, string constraint)
    {
        if (constraint is null) throw new ArgumentNullException(nameof(constraint));
        if (!_constraints.TryGetValue(typeParameter, out var list))
            _constraints[typeParameter] = list = [];
        list.Add(constraint);
    }

    /// <summary>Applies the type-parameter list and where-clauses to a type declaration.</summary>
    internal TDeclaration ApplyTo<TDeclaration>(TDeclaration declaration, string owner)
        where TDeclaration : TypeDeclarationSyntax
    {
        Validate(owner);

        if (SyntaxGenerics.TypeParameterList(_typeParameters) is { } list)
            declaration = (TDeclaration)declaration.WithTypeParameterList(list);

        var clauses = SyntaxGenerics.ConstraintClauses(_typeParameters, _constraints);
        return clauses.Count == 0 ? declaration : (TDeclaration)declaration.WithConstraintClauses(clauses);
    }

    /// <summary>Applies the type-parameter list and where-clauses to a method declaration.</summary>
    internal MethodDeclarationSyntax ApplyTo(MethodDeclarationSyntax method, string owner)
    {
        Validate(owner);

        if (SyntaxGenerics.TypeParameterList(_typeParameters) is { } list)
            method = method.WithTypeParameterList(list);

        var clauses = SyntaxGenerics.ConstraintClauses(_typeParameters, _constraints);
        return clauses.Count == 0 ? method : method.WithConstraintClauses(clauses);
    }

    /// <summary>Applies the type-parameter list and where-clauses to a delegate declaration.</summary>
    internal DelegateDeclarationSyntax ApplyTo(DelegateDeclarationSyntax @delegate, string owner)
    {
        Validate(owner);

        if (SyntaxGenerics.TypeParameterList(_typeParameters) is { } list)
            @delegate = @delegate.WithTypeParameterList(list);

        var clauses = SyntaxGenerics.ConstraintClauses(_typeParameters, _constraints);
        return clauses.Count == 0 ? @delegate : @delegate.WithConstraintClauses(clauses);
    }

    private void Validate(string owner)
        => SyntaxGenerics.Validate(owner, _typeParameters, _constraints);
}
