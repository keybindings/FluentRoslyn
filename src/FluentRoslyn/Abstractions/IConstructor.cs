namespace FluentRoslyn.Abstractions;

/// <summary>
/// A handle to a generated parameterless constructor, for emitting type-checked
/// <c>new T()</c>. Obtained from <c>AsConstructable</c> on a constructor builder, which
/// validates the handle's shape against the declared parameters and pairs
/// <typeparamref name="TDeclaring"/> with the declaring type — so a handle that exists is
/// a handle that matches.
/// </summary>
/// <remarks>
/// The declaring type is a type argument rather than a detail of the handle because it is
/// what the construction <em>produces</em>: <c>Value.New</c> returns an
/// <see cref="IValue{T}"/> of it, so the result can be checked wherever it is used.
/// </remarks>
/// <typeparam name="TDeclaring">The type being constructed — its <c>[EmitsAs]</c> placeholder when it is being generated.</typeparam>
public interface IConstructor<TDeclaring>
{
}

/// <summary>
/// A handle to a generated constructor with one parameter of type
/// <typeparamref name="T1"/>.
/// </summary>
/// <typeparam name="TDeclaring">The type being constructed.</typeparam>
/// <typeparam name="T1">The parameter's type.</typeparam>
public interface IConstructor<TDeclaring, T1>
{
}

/// <summary>A handle to a generated constructor with two parameters.</summary>
/// <typeparam name="TDeclaring">The type being constructed.</typeparam>
/// <typeparam name="T1">The first parameter's type.</typeparam>
/// <typeparam name="T2">The second parameter's type.</typeparam>
public interface IConstructor<TDeclaring, T1, T2>
{
}

/// <summary>A handle to a generated constructor with three parameters.</summary>
/// <typeparam name="TDeclaring">The type being constructed.</typeparam>
/// <typeparam name="T1">The first parameter's type.</typeparam>
/// <typeparam name="T2">The second parameter's type.</typeparam>
/// <typeparam name="T3">The third parameter's type.</typeparam>
public interface IConstructor<TDeclaring, T1, T2, T3>
{
}
