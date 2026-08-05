namespace FluentRoslyn.Abstractions;

/// <summary>
/// A handle to a generated parameterless method whose result can be used as a value.
/// Obtained from <c>AsFunction</c> on a value-returning method builder.
/// </summary>
/// <remarks>
/// Separate from <see cref="IMethod"/> because that handle asserts argument types only
/// and so cannot say what a call through it produces. <typeparamref name="TResult"/> is
/// not asserted by the caller: it comes from <c>MethodBuilder&lt;TReturn&gt;</c>, so the
/// compiler supplies it and it cannot disagree with the declared return type.
/// </remarks>
/// <typeparam name="TResult">The method's return type.</typeparam>
public interface IFunction<TResult>
{
}

/// <summary>A handle to a generated one-parameter method whose result can be used as a value.</summary>
/// <typeparam name="TResult">The method's return type.</typeparam>
/// <typeparam name="T1">The parameter's type.</typeparam>
public interface IFunction<TResult, T1>
{
}

/// <summary>A handle to a generated two-parameter method whose result can be used as a value.</summary>
/// <typeparam name="TResult">The method's return type.</typeparam>
/// <typeparam name="T1">The first parameter's type.</typeparam>
/// <typeparam name="T2">The second parameter's type.</typeparam>
public interface IFunction<TResult, T1, T2>
{
}

/// <summary>A handle to a generated three-parameter method whose result can be used as a value.</summary>
/// <typeparam name="TResult">The method's return type.</typeparam>
/// <typeparam name="T1">The first parameter's type.</typeparam>
/// <typeparam name="T2">The second parameter's type.</typeparam>
/// <typeparam name="T3">The third parameter's type.</typeparam>
public interface IFunction<TResult, T1, T2, T3>
{
}

/// <summary>
/// A handle to a generated parameterless method that carries its declaring type as well
/// as its result, so the receiver is checked too. Obtained from <c>AsFunctionOn</c>.
/// </summary>
/// <typeparam name="TDeclaring">The declaring type — its <c>[EmitsAs]</c> placeholder when it is being generated.</typeparam>
/// <typeparam name="TResult">The method's return type.</typeparam>
public interface IFunctionOn<TDeclaring, TResult>
{
}

/// <summary>A receiver-typed handle to a generated one-parameter method used as a value.</summary>
/// <typeparam name="TDeclaring">The declaring type.</typeparam>
/// <typeparam name="TResult">The method's return type.</typeparam>
/// <typeparam name="T1">The parameter's type.</typeparam>
public interface IFunctionOn<TDeclaring, TResult, T1>
{
}

/// <summary>A receiver-typed handle to a generated two-parameter method used as a value.</summary>
/// <typeparam name="TDeclaring">The declaring type.</typeparam>
/// <typeparam name="TResult">The method's return type.</typeparam>
/// <typeparam name="T1">The first parameter's type.</typeparam>
/// <typeparam name="T2">The second parameter's type.</typeparam>
public interface IFunctionOn<TDeclaring, TResult, T1, T2>
{
}

/// <summary>A receiver-typed handle to a generated three-parameter method used as a value.</summary>
/// <typeparam name="TDeclaring">The declaring type.</typeparam>
/// <typeparam name="TResult">The method's return type.</typeparam>
/// <typeparam name="T1">The first parameter's type.</typeparam>
/// <typeparam name="T2">The second parameter's type.</typeparam>
/// <typeparam name="T3">The third parameter's type.</typeparam>
public interface IFunctionOn<TDeclaring, TResult, T1, T2, T3>
{
}
