using System;
using System.Collections.Generic;
using System.Linq;
using FluentRoslyn.Abstractions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace FluentRoslyn.Builders;

/// <summary>
/// Turns typed references into statements — assignments and method calls. Joins the
/// other <c>Syntax*</c> helpers as the shared emission surface, used by both method and
/// constructor bodies.
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
                Expression(target, parameters, inStaticContext, context),
                Expression(value, parameters, inStaticContext, context)));
    }

    /// <summary>
    /// Builds <c>target.Method(arguments);</c> from a receiver, a method handle, and
    /// argument references. The types were matched by the compiler at the
    /// <c>Call</c> overload; what remains here is emission.
    /// </summary>
    internal static StatementSyntax Invocation(
        IReference target,
        object method,
        IReference[] arguments,
        IReadOnlyCollection<IParameter> parameters,
        bool inStaticContext,
        string context)
    {
        if (target is null) throw new ArgumentNullException(nameof(target));
        if (arguments.Any(a => a is null)) throw new ArgumentNullException(nameof(arguments));

        var handle = MethodHandle.From(method, context);

        return ExpressionStatement(
            InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    Expression(target, parameters, inStaticContext, context),
                    IdentifierName(handle.MethodName)),
                ArgumentList(SeparatedList(arguments.Select(a =>
                    Argument(Expression(a, parameters, inStaticContext, context)))))));
    }

    // A parameter sharing a member's name shadows it, so a bare identifier would bind
    // the parameter -- source that compiles and is silently wrong, which is exactly what
    // this library exists to make impossible. `this.` is the only way to reach the
    // member, and it is unavailable for a static member or from a static context;
    // there, refuse rather than emit the wrong thing. Applied to every reference
    // position: assignment targets and values, call receivers and arguments.
    private static ExpressionSyntax Expression(
        IReference reference,
        IReadOnlyCollection<IParameter> parameters,
        bool inStaticContext,
        string context)
    {
        var identifier = IdentifierName(reference.Name);

        if (!IsMember(reference) || !IsShadowed(reference.Name, parameters))
            return identifier;

        if (inStaticContext || IsStaticMember(reference))
            throw new InvalidOperationException(
                $"{context} declares a parameter named '{reference.Name}', which shadows the member " +
                "being referenced. A static member cannot be reached with 'this.', so the reference " +
                "would silently bind the parameter. Rename the parameter.");

        return MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, ThisExpression(), identifier);
    }

    // An outside implementation of IReference carries no kind, so treat it as a member:
    // qualifying is the conservative choice, since a shadowed member is the case that
    // silently misbehaves.
    private static bool IsMember(IReference reference)
        => reference is not IReferenceInfo info || info.Kind == ReferenceKind.Member;

    private static bool IsStaticMember(IReference reference)
        => reference is IReferenceInfo { IsStaticMember: true };

    private static bool IsShadowed(string name, IReadOnlyCollection<IParameter> parameters)
        => parameters.Any(parameter => string.Equals(parameter.Name, name, StringComparison.Ordinal));
}
