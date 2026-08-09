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
    /// <summary>Builds <c>target = value;</c> from a reference and a value of its type.</summary>
    internal static StatementSyntax Assignment<T>(
        IReference<T> target,
        IValue<T> value,
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
    /// Builds <c>target = value;</c> between two references whose types are text. The
    /// declared types were compared at the call site; what remains here is emission,
    /// which is the same as any other assignment.
    /// </summary>
    internal static StatementSyntax RawAssignment(
        IReference target,
        IReference value,
        IReadOnlyCollection<IParameter> parameters,
        bool inStaticContext,
        string context)
        => ExpressionStatement(
            AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                Expression(target, parameters, inStaticContext, context),
                Expression(value, parameters, inStaticContext, context)));

    /// <summary>
    /// Builds <c>T.Method(arguments);</c> — a static call as a statement. The expression
    /// is the one <c>InvokeStatic</c> produces as a value, so the two cannot drift.
    /// </summary>
    internal static StatementSyntax StaticInvocation(
        TypeNameBuilder type,
        string methodName,
        IValue[] arguments,
        IReadOnlyCollection<IParameter> parameters,
        bool inStaticContext,
        string context)
        => ExpressionStatement(
            new StaticInvocationValue(type, methodName, arguments)
                .Build(value => Expression(value, parameters, inStaticContext, context)));

    /// <summary>
    /// Builds <c>target.Method(arguments);</c> for a method on a type the generator only
    /// discovered. The expression is the same one <c>InvokeRaw</c> produces as a value;
    /// this wraps it as a statement, so the two forms cannot drift.
    /// </summary>
    internal static StatementSyntax RawInvocation(
        IReference target,
        string methodName,
        IValue[] arguments,
        IReadOnlyCollection<IParameter> parameters,
        bool inStaticContext,
        string context)
        => ExpressionStatement(
            new RawInvocationValue(target, methodName, arguments)
                .Build(value => Expression(value, parameters, inStaticContext, context)));

    /// <summary>
    /// Builds <c>target.Method(arguments);</c> from a receiver, a method handle, and
    /// argument references. The types were matched by the compiler at the <c>Call</c>
    /// overload; what remains here is emission — the same expression <c>Invoke</c>
    /// produces as a value, so the two forms cannot drift. They used to, quietly: this
    /// built its own while its two siblings delegated.
    /// </summary>
    internal static StatementSyntax Invocation(
        IReference target,
        object method,
        IValue[] arguments,
        IReadOnlyCollection<IParameter> parameters,
        bool inStaticContext,
        string context)
        => ExpressionStatement(
            new InvocationValue(target, method, arguments, context)
                .Build(value => Expression(value, parameters, inStaticContext, context)));

    /// <summary>Builds <c>target op= value;</c> from a reference and a value of its type.</summary>
    internal static StatementSyntax CompoundAssignment<T>(
        IReference<T> target,
        SyntaxKind kind,
        IValue<T> value,
        IReadOnlyCollection<IParameter> parameters,
        bool inStaticContext,
        string context)
    {
        if (target is null) throw new ArgumentNullException(nameof(target));
        if (value is null) throw new ArgumentNullException(nameof(value));

        return ExpressionStatement(
            AssignmentExpression(
                kind,
                Expression(target, parameters, inStaticContext, context),
                Expression(value, parameters, inStaticContext, context)));
    }

    /// <summary>Builds <c>target op= literal;</c>.</summary>
    internal static StatementSyntax CompoundAssignmentOfLiteral(
        IReference target,
        SyntaxKind kind,
        object? literal,
        IReadOnlyCollection<IParameter> parameters,
        bool inStaticContext,
        string context)
    {
        if (target is null) throw new ArgumentNullException(nameof(target));

        return ExpressionStatement(
            AssignmentExpression(
                kind,
                Expression(target, parameters, inStaticContext, context),
                SyntaxLiterals.Expression(literal)));
    }

    /// <summary>Maps an operator to its assignment syntax kind.</summary>
    internal static SyntaxKind KindOf(AssignmentOperator op)
        => op switch
        {
            AssignmentOperator.Add => SyntaxKind.AddAssignmentExpression,
            AssignmentOperator.Subtract => SyntaxKind.SubtractAssignmentExpression,
            AssignmentOperator.Multiply => SyntaxKind.MultiplyAssignmentExpression,
            AssignmentOperator.Divide => SyntaxKind.DivideAssignmentExpression,
            AssignmentOperator.Modulo => SyntaxKind.ModuloAssignmentExpression,
            AssignmentOperator.And => SyntaxKind.AndAssignmentExpression,
            AssignmentOperator.Or => SyntaxKind.OrAssignmentExpression,
            AssignmentOperator.ExclusiveOr => SyntaxKind.ExclusiveOrAssignmentExpression,
            AssignmentOperator.LeftShift => SyntaxKind.LeftShiftAssignmentExpression,
            AssignmentOperator.RightShift => SyntaxKind.RightShiftAssignmentExpression,
            _ => throw new ArgumentOutOfRangeException(
                nameof(op), op, "Not a compound assignment operator.")
        };

    /// <summary>
    /// Builds <c>target = literal;</c>. The literal's type was matched to the target's by
    /// the compiler at the call site; what remains here is converting it to syntax.
    /// </summary>
    internal static StatementSyntax AssignmentOfLiteral(
        IReference target,
        object? literal,
        IReadOnlyCollection<IParameter> parameters,
        bool inStaticContext,
        string context)
    {
        if (target is null) throw new ArgumentNullException(nameof(target));

        return ExpressionStatement(
            AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                Expression(target, parameters, inStaticContext, context),
                SyntaxLiterals.Expression(literal)));
    }

    /// <summary>Builds <c>return literal;</c>.</summary>
    internal static StatementSyntax ReturnLiteral(object? literal)
        => ReturnStatement(SyntaxLiterals.Expression(literal));

    /// <summary>
    /// Builds <c>if (x is null) throw new ArgumentNullException(nameof(x));</c>.
    /// </summary>
    /// <remarks>
    /// The classic form on purpose, not <c>ArgumentNullException.ThrowIfNull</c>: the
    /// generated code compiles in the consumer's compilation, whose target framework the
    /// generator cannot know, and the helper is .NET 6+. This form compiles everywhere,
    /// including the netstandard2.0 consumers this library exists to serve. The
    /// exception type goes through <see cref="TypeNameBuilder"/> so it is fully
    /// qualified by default and shortens under <c>SimplifyTypeNames</c> like any other
    /// reference.
    /// </remarks>
    internal static StatementSyntax ThrowIfNull(
        IReference value,
        IReadOnlyCollection<IParameter> parameters,
        bool inStaticContext,
        string context)
    {
        if (value is null) throw new ArgumentNullException(nameof(value));

        // `nameof` rejects an element access anywhere in the chain -- measured: CS8081
        // for `nameof(items[0])`, CS8082 for `nameof(items[0].Length)`. Emitting it
        // would break the consumer's build, so refuse to emit rather than produce
        // source that cannot compile.
        // `nameof(this)` is not legal C#, and a null guard on `this` is meaningless anyway.
        if (value is IThisReference)
            throw new InvalidOperationException(
                $"{context} cannot guard 'this': it is never null, and nameof(this) is not legal C#.");

        if (value is IReferencePath { CanNameOf: false })
            throw new InvalidOperationException(
                $"{context} cannot guard '{value.Name}': the guard needs nameof, and C# rejects " +
                "an element access inside nameof. Guard the collection itself, or use AddStatement.");

        var expression = Expression(value, parameters, inStaticContext, context);

        var nameOf = InvocationExpression(
            IdentifierName("nameof"),
            ArgumentList(SingletonSeparatedList(Argument(expression))));

        var throwStatement = ThrowStatement(
            ObjectCreationExpression(TypeNameBuilder.New<ArgumentNullException>().BuildTypeSyntax())
                .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(nameOf)))));

        return IfStatement(
            IsPatternExpression(expression, ConstantPattern(LiteralExpression(SyntaxKind.NullLiteralExpression))),
            throwStatement);
    }

    /// <summary>
    /// Builds <c>return value;</c>, or <c>return;</c> when <paramref name="value"/> is
    /// null. The value goes through the same shadow qualification as every other
    /// reference position.
    /// </summary>
    internal static StatementSyntax Return(
        IValue? value,
        IReadOnlyCollection<IParameter> parameters,
        bool inStaticContext,
        string context)
        => value is null
            ? ReturnStatement()
            : ReturnStatement(Expression(value, parameters, inStaticContext, context));

    // A parameter sharing a member's name shadows it, so a bare identifier would bind
    // the parameter -- source that compiles and is silently wrong, which is exactly what
    // this library exists to make impossible. `this.` is the only way to reach the
    // member, and it is unavailable for a static member or from a static context;
    // there, refuse rather than emit the wrong thing. Applied to every reference
    // position: assignment targets and values, call receivers and arguments.
    private static ExpressionSyntax Expression(
        IValue value,
        IReadOnlyCollection<IParameter> parameters,
        bool inStaticContext,
        string context)
    {
        // A path -- `a.b`, `a[i]` -- qualifies at its root and nowhere else: only the
        // leading name can be shadowed by a parameter, since everything after the first
        // dot binds in the target's type. Recursion covers a chain of any depth.
        if (value is IReferencePath path)
            return path.Compose(
                Expression(path.Target, parameters, inStaticContext, context),
                index => Expression(index, parameters, inStaticContext, context));

        // `this` is a keyword, not an identifier, and nothing can shadow it -- but it
        // does not exist at all in a static context, where emitting it would produce
        // source the consumer cannot compile.
        if (value is IThisReference)
        {
            if (inStaticContext)
                throw new InvalidOperationException(
                    $"{context} is static, so it has no 'this'. Reference the member directly, " +
                    "or make the member non-static.");

            return ThisExpression();
        }

        // A computed value -- `new T(args)`, a call's result -- has no name to shadow, but
        // the values nested inside it do, so it resolves them through this same helper.
        if (value is IComputedValue computed)
            return computed.Build(nested => Expression(nested, parameters, inStaticContext, context));

        if (value is not IReference reference)
            throw new ArgumentException(
                $"{context} was given an IValue that this library did not create. Values come from " +
                "references and from the producers in the fluent API; an outside implementation " +
                "carries no expression to emit.", nameof(value));

        var identifier = IdentifierName(reference.Name);

        if (!IsMember(reference))
            return identifier;

        // An instance member referenced where no instance exists -- a static method, an
        // operator -- would emit bare and fail the consumer's build with CS0120. This
        // fires whether or not a parameter shadows the name, because renaming the
        // parameter cannot fix it: there is no instance to reach the member through.
        if (inStaticContext && !IsStaticMember(reference))
            throw new InvalidOperationException(
                $"{context} is static, so it cannot reference '{reference.Name}', an instance member " +
                "of the type. Reach it through a parameter, or make the member static.");

        if (!IsShadowed(reference.Name, parameters))
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
