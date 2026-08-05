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
