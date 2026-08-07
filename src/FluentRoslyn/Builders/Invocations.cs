using System;
using FluentRoslyn.Abstractions;

namespace FluentRoslyn.Builders;

/// <summary>
/// Produces a value out of a call: <c>target.Method(arguments)</c>. The mirror of
/// <c>Call</c>/<c>CallOn</c> on the statement side, for methods that return something
/// worth using.
/// </summary>
/// <remarks>
/// <para>
/// Extension methods rather than a static factory, so a call reads left-to-right in the
/// order the emitted source reads — the same choice <see cref="References"/> makes for
/// <c>Member</c> and <c>Item</c>.
/// </para>
/// <para>
/// The receiver stays an <see cref="IReference{T}"/> rather than widening to
/// <see cref="IValue{T}"/>, so <c>Factory.Create().Configure()</c> is not expressible.
/// Shadow qualification is defined on a root name and a call has none; and a chain on a
/// temporary is exactly the shape that most wants a named local, which costs one
/// statement and keeps the emitted code flat.
/// </para>
/// </remarks>
public static class Invocations
{
    private const string Context = "Invoke";

    /// <summary>
    /// <c>T.Method(arguments)</c> as a value — a static call whose receiver is a type
    /// rather than a reference.
    /// </summary>
    /// <remarks>
    /// The type goes through <see cref="TypeNameBuilder"/>, so it is fully qualified by
    /// default and <em>shortens under <c>SimplifyTypeNames</c></em> with the import
    /// added — which the raw form cannot do, and which is the reason to prefer this one
    /// whenever the type can be named as <typeparamref name="TDeclaring"/>. An
    /// <c>[EmitsAs]</c> placeholder works here like anywhere else. The method itself is
    /// named by text and unchecked.
    /// </remarks>
    /// <typeparam name="TDeclaring">The type declaring the static method.</typeparam>
    /// <param name="methodName">The method's name. Validated as a C# identifier.</param>
    /// <param name="arguments">The arguments, in order.</param>
    /// <returns>The call, as an untyped value.</returns>
    public static IValue InvokeStatic<TDeclaring>(string methodName, params IValue[] arguments)
        => new StaticInvocationValue(TypeNameBuilder.New<TDeclaring>(), methodName, arguments);

    /// <summary>
    /// <c>T.Method(arguments)</c> as a value, with the declaring type given as a
    /// <see cref="Type"/> — the form that reaches a <c>static class</c>, since C#
    /// forbids one as a type argument (CS0718) and most static methods live in one.
    /// </summary>
    /// <param name="declaringType">The type declaring the static method, e.g. <c>typeof(Math)</c>.</param>
    /// <param name="methodName">The method's name. Validated as a C# identifier.</param>
    /// <param name="arguments">The arguments, in order.</param>
    /// <returns>The call, as an untyped value.</returns>
    public static IValue InvokeStatic(Type declaringType, string methodName, params IValue[] arguments)
        => new StaticInvocationValue(
            TypeNameBuilder.New(declaringType ?? throw new ArgumentNullException(nameof(declaringType))),
            methodName,
            arguments);

    /// <summary>
    /// <c>T.Method(arguments)</c> as a value, where the declaring type is one being
    /// generated alongside and is named by its builder.
    /// </summary>
    /// <param name="type">The builder of the type declaring the static method.</param>
    /// <param name="methodName">The method's name. Validated as a C# identifier.</param>
    /// <param name="arguments">The arguments, in order.</param>
    /// <returns>The call, as an untyped value.</returns>
    public static IValue InvokeStatic(TypeDeclarationBuilder type, string methodName, params IValue[] arguments)
        => new StaticInvocationValue(
            TypeNameBuilder.For(type ?? throw new ArgumentNullException(nameof(type))), methodName, arguments);

    /// <summary>
    /// <c>T.Method(arguments)</c> as a value, where the declaring type is named by text —
    /// for a type the generator only discovered.
    /// </summary>
    /// <remarks>
    /// The text is taken as written, so it neither shortens nor gains an import. Prefer
    /// <see cref="InvokeStatic{TDeclaring}"/> when the type can be named as a type
    /// argument.
    /// </remarks>
    /// <param name="typeName">The declaring type, as C# text. Parsed, so a malformed name is rejected.</param>
    /// <param name="methodName">The method's name. Validated as a C# identifier.</param>
    /// <param name="arguments">The arguments, in order.</param>
    /// <returns>The call, as an untyped value.</returns>
    public static IValue InvokeStaticRaw(string typeName, string methodName, params IValue[] arguments)
        => new StaticInvocationValue(TypeNameBuilder.ForRawName(typeName), methodName, arguments);

    /// <summary>
    /// <c>target.Method(arguments)</c> as a value, for a method on a type the generator
    /// only discovered — <c>return _inner.Greet(name);</c> in a decorator.
    /// </summary>
    /// <remarks>
    /// Nothing is checked: the generator holds the method as an <c>ISymbol</c>, which
    /// the library never sees, so there is no signature to check against. The result is
    /// an untyped <see cref="IValue"/>, so it reaches only positions that accept a bare
    /// value and cannot pass for a checked one. Arguments are <c>params</c> rather than
    /// fixed arities, because a forwarding generator needs whatever arity the discovered
    /// method has — the handle-based families stop at three only because each arity
    /// needs its own type parameters.
    /// </remarks>
    /// <param name="target">The receiver.</param>
    /// <param name="methodName">The method's name. Validated as a C# identifier.</param>
    /// <param name="arguments">The arguments, in order.</param>
    /// <returns>The call, as an untyped value.</returns>
    public static IValue InvokeRaw(IReference target, string methodName, params IValue[] arguments)
        => new RawInvocationValue(target, methodName, arguments);

    /// <summary>Produces <c>target.Method()</c> as a value.</summary>
    /// <typeparam name="TTarget">The receiver's type.</typeparam>
    /// <typeparam name="TResult">The method's return type, taken from the handle.</typeparam>
    /// <param name="target">The receiver.</param>
    /// <param name="function">A handle from <c>AsFunction</c>.</param>
    /// <returns>The call's result, as a value.</returns>
    public static IValue<TResult> Invoke<TTarget, TResult>(
        this IReference<TTarget> target, IFunction<TResult> function)
        => new InvocationValue<TResult>(target, function, [], Context);

    /// <summary>Produces <c>target.Method(argument1)</c> as a value.</summary>
    /// <typeparam name="TTarget">The receiver's type.</typeparam>
    /// <typeparam name="TResult">The method's return type.</typeparam>
    /// <typeparam name="T1">The parameter's type, taken from the handle.</typeparam>
    /// <param name="target">The receiver.</param>
    /// <param name="function">A handle from <c>AsFunction</c>.</param>
    /// <param name="argument1">The argument, whose type must match the handle's.</param>
    /// <returns>The call's result, as a value.</returns>
    public static IValue<TResult> Invoke<TTarget, TResult, T1>(
        this IReference<TTarget> target, IFunction<TResult, T1> function, IValue<T1> argument1)
        => new InvocationValue<TResult>(target, function, [argument1], Context);

    /// <summary>Produces <c>target.Method(argument1, argument2)</c> as a value.</summary>
    /// <typeparam name="TTarget">The receiver's type.</typeparam>
    /// <typeparam name="TResult">The method's return type.</typeparam>
    /// <typeparam name="T1">The first parameter's type.</typeparam>
    /// <typeparam name="T2">The second parameter's type.</typeparam>
    /// <param name="target">The receiver.</param>
    /// <param name="function">A handle from <c>AsFunction</c>.</param>
    /// <param name="argument1">The first argument.</param>
    /// <param name="argument2">The second argument.</param>
    /// <returns>The call's result, as a value.</returns>
    public static IValue<TResult> Invoke<TTarget, TResult, T1, T2>(
        this IReference<TTarget> target,
        IFunction<TResult, T1, T2> function,
        IValue<T1> argument1,
        IValue<T2> argument2)
        => new InvocationValue<TResult>(target, function, [argument1, argument2], Context);

    /// <summary>Produces <c>target.Method(argument1, argument2, argument3)</c> as a value.</summary>
    /// <typeparam name="TTarget">The receiver's type.</typeparam>
    /// <typeparam name="TResult">The method's return type.</typeparam>
    /// <typeparam name="T1">The first parameter's type.</typeparam>
    /// <typeparam name="T2">The second parameter's type.</typeparam>
    /// <typeparam name="T3">The third parameter's type.</typeparam>
    /// <param name="target">The receiver.</param>
    /// <param name="function">A handle from <c>AsFunction</c>.</param>
    /// <param name="argument1">The first argument.</param>
    /// <param name="argument2">The second argument.</param>
    /// <param name="argument3">The third argument.</param>
    /// <returns>The call's result, as a value.</returns>
    public static IValue<TResult> Invoke<TTarget, TResult, T1, T2, T3>(
        this IReference<TTarget> target,
        IFunction<TResult, T1, T2, T3> function,
        IValue<T1> argument1,
        IValue<T2> argument2,
        IValue<T3> argument3)
        => new InvocationValue<TResult>(target, function, [argument1, argument2, argument3], Context);

    /// <summary>
    /// Produces <c>target.Method()</c> as a value, with the receiver checked: the target
    /// must reference <typeparamref name="TDeclaring"/>, the type declaring the method.
    /// </summary>
    /// <remarks>
    /// Named apart from <see cref="Invoke{TTarget, TResult}"/> for the reason
    /// <c>CallOn</c> is named apart from <c>Call</c>: C# drops a candidate whose inference
    /// fails before it can produce a diagnostic, so sharing one name would let the untyped
    /// overload survive and blame the handle rather than the receiver disagreement.
    /// </remarks>
    /// <typeparam name="TDeclaring">The declaring type, which the receiver must match.</typeparam>
    /// <typeparam name="TResult">The method's return type.</typeparam>
    /// <param name="target">The receiver.</param>
    /// <param name="function">A handle from <c>AsFunctionOn</c>.</param>
    /// <returns>The call's result, as a value.</returns>
    public static IValue<TResult> InvokeOn<TDeclaring, TResult>(
        this IReference<TDeclaring> target, IFunctionOn<TDeclaring, TResult> function)
        => new InvocationValue<TResult>(target, function, [], Context);

    /// <summary>Produces a receiver-checked <c>target.Method(argument1)</c> as a value.</summary>
    /// <typeparam name="TDeclaring">The declaring type.</typeparam>
    /// <typeparam name="TResult">The method's return type.</typeparam>
    /// <typeparam name="T1">The parameter's type.</typeparam>
    /// <param name="target">The receiver.</param>
    /// <param name="function">A handle from <c>AsFunctionOn</c>.</param>
    /// <param name="argument1">The argument.</param>
    /// <returns>The call's result, as a value.</returns>
    public static IValue<TResult> InvokeOn<TDeclaring, TResult, T1>(
        this IReference<TDeclaring> target, IFunctionOn<TDeclaring, TResult, T1> function, IValue<T1> argument1)
        => new InvocationValue<TResult>(target, function, [argument1], Context);

    /// <summary>Produces a receiver-checked two-argument call as a value.</summary>
    /// <typeparam name="TDeclaring">The declaring type.</typeparam>
    /// <typeparam name="TResult">The method's return type.</typeparam>
    /// <typeparam name="T1">The first parameter's type.</typeparam>
    /// <typeparam name="T2">The second parameter's type.</typeparam>
    /// <param name="target">The receiver.</param>
    /// <param name="function">A handle from <c>AsFunctionOn</c>.</param>
    /// <param name="argument1">The first argument.</param>
    /// <param name="argument2">The second argument.</param>
    /// <returns>The call's result, as a value.</returns>
    public static IValue<TResult> InvokeOn<TDeclaring, TResult, T1, T2>(
        this IReference<TDeclaring> target,
        IFunctionOn<TDeclaring, TResult, T1, T2> function,
        IValue<T1> argument1,
        IValue<T2> argument2)
        => new InvocationValue<TResult>(target, function, [argument1, argument2], Context);

    /// <summary>Produces a receiver-checked three-argument call as a value.</summary>
    /// <typeparam name="TDeclaring">The declaring type.</typeparam>
    /// <typeparam name="TResult">The method's return type.</typeparam>
    /// <typeparam name="T1">The first parameter's type.</typeparam>
    /// <typeparam name="T2">The second parameter's type.</typeparam>
    /// <typeparam name="T3">The third parameter's type.</typeparam>
    /// <param name="target">The receiver.</param>
    /// <param name="function">A handle from <c>AsFunctionOn</c>.</param>
    /// <param name="argument1">The first argument.</param>
    /// <param name="argument2">The second argument.</param>
    /// <param name="argument3">The third argument.</param>
    /// <returns>The call's result, as a value.</returns>
    public static IValue<TResult> InvokeOn<TDeclaring, TResult, T1, T2, T3>(
        this IReference<TDeclaring> target,
        IFunctionOn<TDeclaring, TResult, T1, T2, T3> function,
        IValue<T1> argument1,
        IValue<T2> argument2,
        IValue<T3> argument3)
        => new InvocationValue<TResult>(target, function, [argument1, argument2, argument3], Context);
}
