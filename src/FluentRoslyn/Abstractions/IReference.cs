namespace FluentRoslyn.Abstractions;

/// <summary>
/// A reference to a named construct that can appear in generated code, without its
/// type. The base of <see cref="IReference{T}"/>; used where references of different
/// types are handled together, e.g. a call's argument list.
/// </summary>
public interface IReference
{
    /// <summary>
    /// The identifier as it will be emitted. A reference composed from another — a
    /// member path or an element access, built by <see cref="Builders.References"/> —
    /// has no single identifier, and reports the whole access instead:
    /// <c>Config.Label</c>, <c>_items[0]</c>.
    /// </summary>
    string Name { get; }
}

/// <summary>
/// A typed reference to a named construct that can appear in generated code — a
/// property, a field, or a parameter.
/// </summary>
/// <remarks>
/// <typeparamref name="T"/> is a phantom type parameter: no value of that type is ever
/// stored. It exists so the compiler can reject a reference used where a different type
/// is expected, which turns a whole class of generated-code bugs into build errors in
/// the generator itself. The parameter is deliberately invariant — see
/// <c>docs/ROADMAP.md</c> for why exact matching is the enforceable contract.
/// </remarks>
/// <typeparam name="T">The type of the referenced construct.</typeparam>
public interface IReference<T> : IReference
{
}
