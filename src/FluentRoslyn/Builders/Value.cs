using FluentRoslyn.Abstractions;

namespace FluentRoslyn.Builders;

/// <summary>
/// Produces a value out of a constructor handle: <c>new T(arguments)</c>.
/// </summary>
/// <remarks>
/// A static factory rather than an extension method, because construction has no
/// receiver for one to hang off — unlike a call, which reads left-to-right through
/// <see cref="Invocations"/>. The two producers have different shapes in C# itself.
/// </remarks>
public static class Value
{
    private const string Context = "Value.New";

    /// <summary>Produces <c>new T()</c>.</summary>
    /// <typeparam name="TDeclaring">The type being constructed, taken from the handle.</typeparam>
    /// <param name="constructor">A handle from <c>AsConstructable</c>.</param>
    /// <returns>A value of the constructed type.</returns>
    public static IValue<TDeclaring> New<TDeclaring>(IConstructor<TDeclaring> constructor)
        => new ConstructionValue<TDeclaring>(constructor, [], Context);

    /// <summary>Produces <c>new T(argument1)</c>.</summary>
    /// <typeparam name="TDeclaring">The type being constructed.</typeparam>
    /// <typeparam name="T1">The parameter's type, taken from the handle.</typeparam>
    /// <param name="constructor">A handle from <c>AsConstructable</c>.</param>
    /// <param name="argument1">The argument, whose type must match the handle's.</param>
    /// <returns>A value of the constructed type.</returns>
    public static IValue<TDeclaring> New<TDeclaring, T1>(
        IConstructor<TDeclaring, T1> constructor, IValue<T1> argument1)
        => new ConstructionValue<TDeclaring>(constructor, [argument1], Context);

    /// <summary>Produces <c>new T(argument1, argument2)</c>.</summary>
    /// <typeparam name="TDeclaring">The type being constructed.</typeparam>
    /// <typeparam name="T1">The first parameter's type.</typeparam>
    /// <typeparam name="T2">The second parameter's type.</typeparam>
    /// <param name="constructor">A handle from <c>AsConstructable</c>.</param>
    /// <param name="argument1">The first argument.</param>
    /// <param name="argument2">The second argument.</param>
    /// <returns>A value of the constructed type.</returns>
    public static IValue<TDeclaring> New<TDeclaring, T1, T2>(
        IConstructor<TDeclaring, T1, T2> constructor, IValue<T1> argument1, IValue<T2> argument2)
        => new ConstructionValue<TDeclaring>(constructor, [argument1, argument2], Context);

    /// <summary>Produces <c>new T(argument1, argument2, argument3)</c>.</summary>
    /// <typeparam name="TDeclaring">The type being constructed.</typeparam>
    /// <typeparam name="T1">The first parameter's type.</typeparam>
    /// <typeparam name="T2">The second parameter's type.</typeparam>
    /// <typeparam name="T3">The third parameter's type.</typeparam>
    /// <param name="constructor">A handle from <c>AsConstructable</c>.</param>
    /// <param name="argument1">The first argument.</param>
    /// <param name="argument2">The second argument.</param>
    /// <param name="argument3">The third argument.</param>
    /// <returns>A value of the constructed type.</returns>
    public static IValue<TDeclaring> New<TDeclaring, T1, T2, T3>(
        IConstructor<TDeclaring, T1, T2, T3> constructor,
        IValue<T1> argument1,
        IValue<T2> argument2,
        IValue<T3> argument3)
        => new ConstructionValue<TDeclaring>(constructor, [argument1, argument2, argument3], Context);
}
