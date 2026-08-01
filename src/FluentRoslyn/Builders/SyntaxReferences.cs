using System;
using System.Collections.Generic;
using System.Linq;
using FluentRoslyn.Abstractions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace FluentRoslyn.Builders;

/// <summary>
/// Turns typed references into assignment statements. Joins the other <c>Syntax*</c>
/// helpers as the shared emission surface, used by both method and constructor bodies.
/// </summary>
internal static class SyntaxReferences
{
    /// <summary>Builds <c>target = value;</c> from two references of the same type.</summary>
    internal static StatementSyntax Assignment<T>(
        IReference<T> target,
        IReference<T> value,
        IReadOnlyCollection<IParameter> parameters,
        bool inStaticContext,
        string context)
    {
        if (target is null) throw new ArgumentNullException(nameof(target));
        if (value is null) throw new ArgumentNullException(nameof(value));

        return ExpressionStatement(
            AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                TargetExpression(target, parameters, inStaticContext, context),
                IdentifierName(value.Name)));
    }

    // A parameter sharing the member's name shadows it, so a bare `name = name;` would
    // assign the parameter to itself -- source that compiles and is silently wrong, which
    // is exactly what this library exists to make impossible. `this.` is the only way to
    // reach the member, and it is unavailable for a static member or from a static
    // context; there, refuse rather than emit the wrong thing.
    private static ExpressionSyntax TargetExpression<T>(
        IReference<T> target,
        IReadOnlyCollection<IParameter> parameters,
        bool inStaticContext,
        string context)
    {
        var identifier = IdentifierName(target.Name);

        if (!IsMember(target) || !IsShadowed(target.Name, parameters))
            return identifier;

        if (inStaticContext || IsStaticMember(target))
            throw new InvalidOperationException(
                $"{context} declares a parameter named '{target.Name}', which shadows the member being " +
                "assigned. A static member cannot be reached with 'this.', so the assignment would " +
                "silently target the parameter. Rename the parameter.");

        return MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, ThisExpression(), identifier);
    }

    // An outside implementation of IReference<T> carries no kind, so treat it as a member:
    // qualifying is the conservative choice, since a shadowed member is the case that
    // silently misbehaves.
    private static bool IsMember<T>(IReference<T> reference)
        => reference is not IReferenceInfo info || info.Kind == ReferenceKind.Member;

    private static bool IsStaticMember<T>(IReference<T> reference)
        => reference is IReferenceInfo { IsStaticMember: true };

    private static bool IsShadowed(string name, IReadOnlyCollection<IParameter> parameters)
        => parameters.Any(parameter => string.Equals(parameter.Name, name, StringComparison.Ordinal));
}
