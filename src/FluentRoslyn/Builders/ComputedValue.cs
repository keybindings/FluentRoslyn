using System;
using System.Collections.Generic;
using System.Linq;
using FluentRoslyn.Abstractions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace FluentRoslyn.Builders;

/// <summary>
/// A value that is computed rather than named — <c>new T(args)</c>, or a call's result.
/// </summary>
/// <remarks>
/// A producer cannot build its syntax when it is created: shadow qualification needs the
/// enclosing parameter list, which only the statement builder has. So a value is a
/// description, resolved at emission — the same deferral <see cref="IReferencePath"/>
/// uses for an index, through the same one dispatch point.
/// </remarks>
internal interface IComputedValue
{
    /// <summary>
    /// Builds the expression. <paramref name="qualify"/> builds any nested value —
    /// arguments — through the shared shadow-qualification rules.
    /// </summary>
    ExpressionSyntax Build(Func<IValue, ExpressionSyntax> qualify);
}

/// <summary>
/// <c>new T(arguments)</c>. The constructed type and the argument types were matched by
/// the compiler where the handle was used; what remains here is emission.
/// </summary>
/// <typeparam name="T">The constructed type.</typeparam>
internal sealed class ConstructionValue<T> : IValue<T>, IComputedValue
{
    private readonly ConstructorHandle _constructor;
    private readonly IValue[] _arguments;

    internal ConstructionValue(object constructor, IValue[] arguments, string context)
    {
        _constructor = ConstructorHandle.From(constructor, context);

        if (arguments.Any(a => a is null)) throw new ArgumentNullException(nameof(arguments));
        _arguments = arguments;
    }

    public ExpressionSyntax Build(Func<IValue, ExpressionSyntax> qualify)
        => ObjectCreationExpression(_constructor.DeclaringType.BuildTypeSyntax())
            .WithArgumentList(ArgumentList(SeparatedList(_arguments.Select(a => Argument(qualify(a))))));
}

/// <summary>
/// <c>new T(arguments)</c> where <c>T</c> is named by text and nothing about the
/// constructor is checked — for a type the generator did not build and cannot name as a
/// type argument, above all one discovered from the consumer's compilation.
/// </summary>
/// <remarks>
/// Produces an untyped <see cref="IValue"/>, so it reaches only the positions that
/// accept a bare value and cannot slip into a typed one. The gain over a raw statement
/// is real but bounded: the syntax is built rather than concatenated, and the arguments
/// are references whose names come from the builders that declared them, so a name
/// cannot drift between the declaration and the use.
/// </remarks>
internal sealed class RawConstructionValue : IValue, IComputedValue
{
    private readonly TypeNameBuilder _type;
    private readonly IValue[] _arguments;

    internal RawConstructionValue(string typeName, IValue[] arguments)
    {
        _type = TypeNameBuilder.ForRawName(typeName);

        if (arguments.Any(a => a is null)) throw new ArgumentNullException(nameof(arguments));
        _arguments = arguments;
    }

    public ExpressionSyntax Build(Func<IValue, ExpressionSyntax> qualify)
        => ObjectCreationExpression(_type.BuildTypeSyntax())
            .WithArgumentList(ArgumentList(SeparatedList(_arguments.Select(a => Argument(qualify(a))))));
}

/// <summary>
/// A constant used where a value is expected — a call argument, above all. Typed, so it
/// composes with the checked families: <c>Value.Literal("x")</c> is an
/// <c>IValue&lt;string&gt;</c> and fits a handle whose parameter is a string.
/// </summary>
/// <remarks>
/// Closes the last part of the "only named references can be values" limit that
/// <c>AssignLiteral</c> closed for assignment alone. Nothing needs qualifying inside a
/// literal, so the callback is ignored.
/// </remarks>
internal sealed class LiteralValue<T> : IValue<T>, IComputedValue
{
    private readonly T _value;

    internal LiteralValue(T value)
    {
        _value = value;
    }

    public ExpressionSyntax Build(Func<IValue, ExpressionSyntax> qualify)
        => SyntaxLiterals.Expression(_value);
}

/// <summary>
/// <c>Type.Method(arguments)</c> — a static call. The receiver is a type rather than a
/// reference, which is why this could not be folded into the handle families: there is
/// nothing to qualify on the left, and nothing that can be shadowed.
/// </summary>
/// <remarks>
/// The type goes through <see cref="TypeNameBuilder"/>, so a <c>&lt;T&gt;</c> or
/// builder-reference receiver is fully qualified by default and <em>shortens under
/// <c>SimplifyTypeNames</c></em> with the import added — which raw text cannot do. The
/// method itself is named by text and unchecked, in every form.
/// </remarks>
internal sealed class StaticInvocationValue : IValue, IComputedValue
{
    private readonly TypeNameBuilder _type;
    private readonly string _methodName;
    private readonly IValue[] _arguments;

    internal StaticInvocationValue(TypeNameBuilder type, string methodName, IValue[] arguments)
    {
        _type = type;
        Identifiers.Validate(methodName);
        _methodName = methodName;

        if (arguments is null) throw new ArgumentNullException(nameof(arguments));
        if (arguments.Any(a => a is null)) throw new ArgumentNullException(nameof(arguments));
        _arguments = arguments;
    }

    public ExpressionSyntax Build(Func<IValue, ExpressionSyntax> qualify)
        => InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                _type.BuildTypeSyntax(),
                IdentifierName(_methodName)),
            ArgumentList(SeparatedList(_arguments.Select(a => Argument(qualify(a))))));
}

/// <summary>
/// <c>target.Method(arguments)</c> where the method belongs to a type the generator only
/// discovered, so nothing about it is checked — not its existence, not its arity, not
/// its argument types.
/// </summary>
/// <remarks>
/// Takes the arguments as <c>params</c> rather than in fixed arities. The handle-based
/// families stop at three because each arity needs its own type parameters; with nothing
/// to check there is nothing to bound, and a generator forwarding a discovered method
/// needs whatever arity that method has.
/// </remarks>
internal sealed class RawInvocationValue : IValue, IComputedValue
{
    private readonly IReference _target;
    private readonly string _methodName;
    private readonly IValue[] _arguments;

    internal RawInvocationValue(IReference target, string methodName, IValue[] arguments)
    {
        _target = target ?? throw new ArgumentNullException(nameof(target));
        Identifiers.Validate(methodName);
        _methodName = methodName;

        if (arguments is null) throw new ArgumentNullException(nameof(arguments));
        if (arguments.Any(a => a is null)) throw new ArgumentNullException(nameof(arguments));
        _arguments = arguments;
    }

    public ExpressionSyntax Build(Func<IValue, ExpressionSyntax> qualify)
        => InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                qualify(_target),
                IdentifierName(_methodName)),
            ArgumentList(SeparatedList(_arguments.Select(a => Argument(qualify(a))))));
}

/// <summary>
/// <c>target.Method(arguments)</c> through a typed handle. Identical in syntax to the
/// statement form, which is why <see cref="SyntaxReferences.Invocation"/> wraps this
/// rather than building its own — the untyped base exists for exactly that, since a
/// statement has no result type to name.
/// </summary>
/// <remarks>
/// The two used to be built separately, while the doc comments on both sides said they
/// could not drift. The sibling raw and static forms did delegate, so this was the odd
/// one out in a family of three.
/// </remarks>
internal class InvocationValue : IComputedValue
{
    private readonly IReference _target;
    private readonly MethodHandle _method;
    private readonly IValue[] _arguments;

    internal InvocationValue(IReference target, object method, IValue[] arguments, string context)
    {
        _target = target ?? throw new ArgumentNullException(nameof(target));
        _method = MethodHandle.From(method, context);

        if (arguments is null) throw new ArgumentNullException(nameof(arguments));
        if (arguments.Any(a => a is null)) throw new ArgumentNullException(nameof(arguments));
        _arguments = arguments;
    }

    public ExpressionSyntax Build(Func<IValue, ExpressionSyntax> qualify)
        => InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    qualify(_target),
                    IdentifierName(_method.MethodName)))
            .WithArgumentList(ArgumentList(SeparatedList(_arguments.Select(a => Argument(qualify(a))))));
}

/// <summary>
/// The same call, carrying the result type so it can be assigned or returned where a
/// <typeparamref name="T"/> is expected.
/// </summary>
/// <typeparam name="T">The call's result type.</typeparam>
internal sealed class InvocationValue<T> : InvocationValue, IValue<T>
{
    internal InvocationValue(IReference target, object method, IValue[] arguments, string context)
        : base(target, method, arguments, context)
    {
    }
}

/// <summary>
/// The implementation behind every <c>IConstructor</c> handle. Carries the type to
/// construct — the shape was validated when <c>AsConstructable</c> created the handle,
/// and the arity lives in the interface's type arguments where the compiler enforces it.
/// </summary>
internal class ConstructorHandle
{
    internal ConstructorHandle(TypeNameBuilder declaringType)
    {
        DeclaringType = declaringType;
    }

    internal TypeNameBuilder DeclaringType { get; }

    /// <summary>
    /// Recovers the implementation from a handle interface. The interfaces are public but
    /// only <c>AsConstructable</c> mints handles; anything else cannot carry a type.
    /// </summary>
    internal static ConstructorHandle From(object constructor, string context)
        => constructor as ConstructorHandle
           ?? throw new ArgumentException(
               $"{context} was given an IConstructor that was not created by AsConstructable.",
               nameof(constructor));
}

internal sealed class ConstructorHandle0<TDeclaring> : ConstructorHandle, IConstructor<TDeclaring>
{
    internal ConstructorHandle0(TypeNameBuilder declaringType) : base(declaringType)
    {
    }
}

internal sealed class ConstructorHandle1<TDeclaring, T1> : ConstructorHandle, IConstructor<TDeclaring, T1>
{
    internal ConstructorHandle1(TypeNameBuilder declaringType) : base(declaringType)
    {
    }
}

internal sealed class ConstructorHandle2<TDeclaring, T1, T2> : ConstructorHandle, IConstructor<TDeclaring, T1, T2>
{
    internal ConstructorHandle2(TypeNameBuilder declaringType) : base(declaringType)
    {
    }
}

internal sealed class ConstructorHandle3<TDeclaring, T1, T2, T3>
    : ConstructorHandle, IConstructor<TDeclaring, T1, T2, T3>
{
    internal ConstructorHandle3(TypeNameBuilder declaringType) : base(declaringType)
    {
    }
}

internal sealed class FunctionHandle0<TResult> : MethodHandle, IFunction<TResult>
{
    internal FunctionHandle0(string methodName) : base(methodName)
    {
    }
}

internal sealed class FunctionHandle1<TResult, T1> : MethodHandle, IFunction<TResult, T1>
{
    internal FunctionHandle1(string methodName) : base(methodName)
    {
    }
}

internal sealed class FunctionHandle2<TResult, T1, T2> : MethodHandle, IFunction<TResult, T1, T2>
{
    internal FunctionHandle2(string methodName) : base(methodName)
    {
    }
}

internal sealed class FunctionHandle3<TResult, T1, T2, T3> : MethodHandle, IFunction<TResult, T1, T2, T3>
{
    internal FunctionHandle3(string methodName) : base(methodName)
    {
    }
}

internal sealed class FunctionHandleOn0<TDeclaring, TResult> : MethodHandle, IFunctionOn<TDeclaring, TResult>
{
    internal FunctionHandleOn0(string methodName) : base(methodName)
    {
    }
}

internal sealed class FunctionHandleOn1<TDeclaring, TResult, T1> : MethodHandle, IFunctionOn<TDeclaring, TResult, T1>
{
    internal FunctionHandleOn1(string methodName) : base(methodName)
    {
    }
}

internal sealed class FunctionHandleOn2<TDeclaring, TResult, T1, T2>
    : MethodHandle, IFunctionOn<TDeclaring, TResult, T1, T2>
{
    internal FunctionHandleOn2(string methodName) : base(methodName)
    {
    }
}

internal sealed class FunctionHandleOn3<TDeclaring, TResult, T1, T2, T3>
    : MethodHandle, IFunctionOn<TDeclaring, TResult, T1, T2, T3>
{
    internal FunctionHandleOn3(string methodName) : base(methodName)
    {
    }
}
