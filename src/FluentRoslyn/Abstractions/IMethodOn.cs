namespace FluentRoslyn.Abstractions;

/// <summary>
/// A handle to a parameterless method declared on <typeparamref name="TDeclaring"/>.
/// Obtained from <c>AsCallableOn</c>, which checks that the declaring type builder
/// really emits under <typeparamref name="TDeclaring"/>'s name — so a call through the
/// handle only compiles with a receiver of the right type.
/// </summary>
/// <typeparam name="TDeclaring">
/// The type declaring the method, named by its <c>[EmitsAs]</c> placeholder when the
/// type is being generated.
/// </typeparam>
public interface IMethodOn<TDeclaring>
{
}

/// <summary>
/// A handle to a one-parameter method declared on <typeparamref name="TDeclaring"/>.
/// Both the receiver and the argument are checked at the call.
/// </summary>
/// <typeparam name="TDeclaring">The type declaring the method.</typeparam>
/// <typeparam name="T1">The parameter's type.</typeparam>
public interface IMethodOn<TDeclaring, T1>
{
}

/// <summary>A handle to a two-parameter method declared on <typeparamref name="TDeclaring"/>.</summary>
/// <typeparam name="TDeclaring">The type declaring the method.</typeparam>
/// <typeparam name="T1">The first parameter's type.</typeparam>
/// <typeparam name="T2">The second parameter's type.</typeparam>
public interface IMethodOn<TDeclaring, T1, T2>
{
}

/// <summary>A handle to a three-parameter method declared on <typeparamref name="TDeclaring"/>.</summary>
/// <typeparam name="TDeclaring">The type declaring the method.</typeparam>
/// <typeparam name="T1">The first parameter's type.</typeparam>
/// <typeparam name="T2">The second parameter's type.</typeparam>
/// <typeparam name="T3">The third parameter's type.</typeparam>
public interface IMethodOn<TDeclaring, T1, T2, T3>
{
}
