namespace FluentRoslyn.Abstractions;

/// <summary>
/// A reference whose type was given as text rather than as a type argument — a field
/// from <c>DefineField(name, typeName)</c>, or a parameter from
/// <c>WithParameter(name, typeName, out …)</c>.
/// </summary>
/// <remarks>
/// <para>
/// This exists so assignment between two such references can be spelled <c>Assign</c>
/// without endangering the checked one. The two sets are disjoint — a
/// <c>PropertyBuilder&lt;T&gt;</c> is an <see cref="IReference{T}"/> and never an
/// <see cref="IRawReference"/>, and a raw field is the reverse — so exactly one overload
/// is ever applicable, and the checked call cannot be silently routed through the
/// unchecked one.
/// </para>
/// <para>
/// What a raw assignment can still check is the declared type <em>text</em> of both
/// sides. That is weaker than the <c>&lt;T&gt;</c> contract and it happens when the
/// generator runs rather than when it compiles — but it is the same rule
/// <c>AsCallable</c> already validates handles by, and for a type known only as an
/// <c>ISymbol</c> it is the strongest rule available.
/// </para>
/// </remarks>
public interface IRawReference : IReference
{
}
