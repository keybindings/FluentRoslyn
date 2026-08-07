using System;
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

    /// <summary>
    /// A constant where a value is expected — a call argument above all:
    /// <c>CallStatic&lt;Console&gt;("WriteLine", Value.Literal("hello"))</c>.
    /// </summary>
    /// <remarks>
    /// Typed, so it composes with the checked families: <c>Value.Literal("x")</c> is an
    /// <c>IValue&lt;string&gt;</c> and fits a handle whose parameter is a string, while
    /// <c>Value.Literal(1)</c> does not. Covers the primitives with a natural C# literal
    /// form; anything else needs a raw expression. This is the argument-position
    /// counterpart of <c>AssignLiteral</c> and <c>ReturnLiteral</c>, which closed the
    /// same limit for assignment and return only.
    /// </remarks>
    /// <typeparam name="T">The constant's type, inferred from the argument.</typeparam>
    /// <param name="value">The constant.</param>
    /// <returns>The constant, as a typed value.</returns>
    public static IValue<T> Literal<T>(T value) => new LiteralValue<T>(value);

    /// <summary>
    /// Produces <c>new T(arguments)</c> for a type named by text — for a type the
    /// generator did not build, above all one discovered from the consumer's compilation
    /// as an <c>ISymbol</c>, which has no <c>ConstructorBuilder</c> to take a handle from.
    /// </summary>
    /// <remarks>
    /// Nothing about the constructor is checked: the generator has no signature to check
    /// against. The result is an untyped <see cref="IValue"/>, so it reaches only the
    /// positions that accept a bare value and cannot pass for a checked one. What it does
    /// buy over a raw statement is that the syntax is built rather than concatenated, and
    /// the argument names come from the builders that declared them rather than from a
    /// second piece of string formatting that can drift.
    /// </remarks>
    /// <param name="typeName">The type to construct, as C# text. Parsed, so a malformed name is rejected.</param>
    /// <param name="arguments">The constructor arguments, in order.</param>
    /// <returns>The construction, as an untyped value.</returns>
    public static IValue NewOfType(string typeName, params IValue[] arguments)
        => new RawConstructionValue(typeName, arguments ?? throw new ArgumentNullException(nameof(arguments)));

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
