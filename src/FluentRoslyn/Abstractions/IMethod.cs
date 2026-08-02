namespace FluentRoslyn.Abstractions;

/// <summary>
/// A handle to a generated method with no parameters, for emitting type-checked calls.
/// Obtained from <c>AsCallable</c> on a method builder, which validates the handle's
/// shape against the declared parameters — so a handle that exists is a handle that
/// matches.
/// </summary>
public interface IMethod
{
}

/// <summary>
/// A handle to a generated method with one parameter of type <typeparamref name="T1"/>.
/// A call through the handle only compiles when the argument reference's type matches.
/// </summary>
/// <typeparam name="T1">The parameter's type.</typeparam>
public interface IMethod<T1>
{
}

/// <summary>A handle to a generated method with two parameters.</summary>
/// <typeparam name="T1">The first parameter's type.</typeparam>
/// <typeparam name="T2">The second parameter's type.</typeparam>
public interface IMethod<T1, T2>
{
}

/// <summary>A handle to a generated method with three parameters.</summary>
/// <typeparam name="T1">The first parameter's type.</typeparam>
/// <typeparam name="T2">The second parameter's type.</typeparam>
/// <typeparam name="T3">The third parameter's type.</typeparam>
public interface IMethod<T1, T2, T3>
{
}
