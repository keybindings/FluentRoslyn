using FluentRoslyn.Abstractions;

namespace FluentRoslyn.Builders;

/// <summary>
/// Marks a reference to <c>this</c>. Emission needs to know, because <c>this</c> is a
/// keyword rather than an identifier — and because it is illegal in a static context,
/// which is a guard the shared emission path already has the information to apply.
/// </summary>
internal interface IThisReference
{
}

/// <summary>
/// <c>this</c>, without a type. For a type the generator discovers or builds without an
/// <c>[EmitsAs]</c> placeholder, there is no <c>T</c> to carry.
/// </summary>
internal sealed class ThisReference : IReference, IThisReference
{
    public string Name => "this";
}

/// <summary>
/// <c>this</c>, typed through the declaring type's placeholder, so it composes with the
/// typed surface — as a call receiver, an argument, or an assigned value.
/// </summary>
/// <typeparam name="T">The declaring type, named by its placeholder.</typeparam>
internal sealed class ThisReference<T> : IReference<T>, IThisReference
{
    public string Name => "this";
}
