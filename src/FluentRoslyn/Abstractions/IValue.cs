namespace FluentRoslyn.Abstractions;

/// <summary>
/// Something that produces a value in generated code, without its type. The base of
/// <see cref="IValue{T}"/>; used where values of different types are handled together,
/// e.g. a call's argument list.
/// </summary>
public interface IValue
{
}

/// <summary>
/// Something that produces a value of type <typeparamref name="T"/> — a reference, or
/// (once those land) a constructor call or a method call's result.
/// </summary>
/// <remarks>
/// <para>
/// Every <see cref="IReference{T}"/> is an <see cref="IValue{T}"/>, and the converse is
/// deliberately false. A value has no name and no location, so it cannot be assigned
/// <em>to</em> and <c>nameof</c> cannot see it — which is precisely why <c>Assign</c>'s
/// target and <c>ThrowIfNull</c> keep asking for a reference while everything on the
/// value side asks for this.
/// </para>
/// <para>
/// <typeparamref name="T"/> is a phantom type parameter, invariant for the same reason
/// <see cref="IReference{T}"/> is: C# constraints cannot express "implicitly convertible
/// to", so exact matching is the only enforceable contract, and covariance would let a
/// mismatch compile by inferring a common base.
/// </para>
/// <para>
/// The set of things that can produce a value is closed on purpose — see
/// <c>docs/DESIGN-computed-values.md</c>. Values are produced, never combined: there is
/// no operator, no comparison and no conditional, so nothing here needs precedence or
/// evaluation order.
/// </para>
/// </remarks>
/// <typeparam name="T">The type of the value produced.</typeparam>
public interface IValue<T> : IValue
{
}
