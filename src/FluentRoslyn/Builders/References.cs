using System;
using System.Collections.Generic;
using FluentRoslyn.Abstractions;

namespace FluentRoslyn.Builders;

/// <summary>
/// Builds a reference out of another reference: <c>Config.Name</c>, <c>_items[0]</c>,
/// <c>_map[key]</c>. The result is an <see cref="IReference{T}"/> like any other, so
/// everything that already takes one — <c>Assign</c>, <c>Return</c>, <c>Call</c>'s
/// receiver and arguments, <c>ThrowIfNull</c> — accepts a path without a new overload.
/// </summary>
/// <remarks>
/// <para>
/// This extends <em>references</em>, not expressions. A path names a location; it does
/// not compute anything, so there is still no operator, no comparison, and no evaluation
/// order to model. That keeps it on the near side of the expression-grammar line the
/// roadmap draws.
/// </para>
/// <para>
/// What it closes is the assignment <em>target</em> column. Before this, only a simple
/// name could be assigned to — a property, a field, a parameter, a setter's
/// <c>value</c> — so <c>this.a.b = x;</c> and <c>arr[i] = x;</c> had to be raw text,
/// which is exactly where a typo emits source that compiles into the wrong thing.
/// </para>
/// </remarks>
public static class References
{
    /// <summary>
    /// A member of what <paramref name="target"/> refers to, named by an existing typed
    /// reference to that member: <c>target.Member(labelProperty)</c>. Both the name and
    /// the type come from the member's own definition, so neither can drift.
    /// </summary>
    /// <remarks>
    /// What is <em>not</em> checked is that the member actually belongs to the target's
    /// type — the same seam as calling an <c>AsCallable</c> handle through the untyped
    /// <c>Call</c>. Nothing in the generator's type system relates a property builder to
    /// the type it was defined on.
    /// </remarks>
    /// <typeparam name="TMember">The member's type, taken from the member reference.</typeparam>
    /// <param name="target">The reference the member is reached through.</param>
    /// <param name="member">A reference to the member — typically a property or field builder.</param>
    /// <returns>A reference to <c>target.Member</c>.</returns>
    public static IReference<TMember> Member<TMember>(this IReference target, IReference<TMember> member)
    {
        if (member is null) throw new ArgumentNullException(nameof(member));

        return new MemberPath<TMember>(target, member.Name);
    }

    /// <summary>
    /// A member of what <paramref name="target"/> refers to, named by text:
    /// <c>target.MemberNamed&lt;string&gt;("Label")</c>. The type of the result is
    /// asserted rather than derived, which is why it is named apart from
    /// <see cref="Member{TMember}"/> — this is the raw-text seam, for a member of a type
    /// the generator has no handle to.
    /// </summary>
    /// <typeparam name="TMember">The member's type, asserted by the caller.</typeparam>
    /// <param name="target">The reference the member is reached through.</param>
    /// <param name="name">The member's name. Validated as a C# identifier.</param>
    /// <returns>A reference to <c>target.name</c>.</returns>
    public static IReference<TMember> MemberNamed<TMember>(this IReference target, string name)
        => new MemberPath<TMember>(target, name);

    /// <summary>
    /// A member of what <paramref name="target"/> refers to, with no type asserted:
    /// <c>target.MemberRaw("Count")</c>. For a member of a type the generator only
    /// discovered, where there is no <c>T</c> to assert and asserting one would be a
    /// guess.
    /// </summary>
    /// <remarks>
    /// Named apart from <see cref="MemberNamed{TMember}"/> rather than being an overload
    /// without the type argument, for the reason that keeps recurring: a caller who
    /// meant the typed form and forgot the type argument would silently get this one,
    /// which is the wrong-candidate-succeeds failure the codebase has been avoiding
    /// since <c>CallOn</c>. The result is a location, so it can be assigned to as well
    /// as read — but nothing knows its type, so <c>AssignRaw</c> involving it is
    /// unchecked rather than checked by type text.
    /// </remarks>
    /// <param name="target">The reference the member is reached through.</param>
    /// <param name="name">The member's name. Validated as a C# identifier.</param>
    /// <returns>A reference to <c>target.name</c>.</returns>
    public static IRawReference MemberRaw(this IReference target, string name)
        => new RawMemberPath(target, name);

    /// <summary>
    /// An element of an array at a constant index: <c>target[0]</c>. The element type
    /// comes from the array's, so it cannot be asserted wrongly.
    /// </summary>
    /// <typeparam name="TItem">The array's element type.</typeparam>
    /// <param name="target">A reference to the array.</param>
    /// <param name="index">The index. Must not be negative.</param>
    /// <returns>A reference to <c>target[index]</c>.</returns>
    public static IReference<TItem> Item<TItem>(this IReference<TItem[]> target, int index)
        => ElementPath<TItem>.OfLiteral(target, NonNegative(index));

    /// <summary>An element of an array at an index held in another reference: <c>target[i]</c>.</summary>
    /// <typeparam name="TItem">The array's element type.</typeparam>
    /// <param name="target">A reference to the array.</param>
    /// <param name="index">A reference to the index.</param>
    /// <returns>A reference to <c>target[index]</c>.</returns>
    public static IReference<TItem> Item<TItem>(this IReference<TItem[]> target, IReference<int> index)
        => ElementPath<TItem>.OfReference(target, index);

    /// <summary>An element of a list at a constant index: <c>target[0]</c>.</summary>
    /// <typeparam name="TItem">The list's element type.</typeparam>
    /// <param name="target">A reference to the list.</param>
    /// <param name="index">The index. Must not be negative.</param>
    /// <returns>A reference to <c>target[index]</c>.</returns>
    public static IReference<TItem> Item<TItem>(this IReference<List<TItem>> target, int index)
        => ElementPath<TItem>.OfLiteral(target, NonNegative(index));

    /// <summary>An element of a list at an index held in another reference: <c>target[i]</c>.</summary>
    /// <typeparam name="TItem">The list's element type.</typeparam>
    /// <param name="target">A reference to the list.</param>
    /// <param name="index">A reference to the index.</param>
    /// <returns>A reference to <c>target[index]</c>.</returns>
    public static IReference<TItem> Item<TItem>(this IReference<List<TItem>> target, IReference<int> index)
        => ElementPath<TItem>.OfReference(target, index);

    /// <summary>
    /// A dictionary entry under a constant key: <c>target["name"]</c>. The key's type is
    /// the dictionary's, so a key of the wrong type is a compile error in the generator.
    /// </summary>
    /// <typeparam name="TKey">The dictionary's key type.</typeparam>
    /// <typeparam name="TValue">The dictionary's value type.</typeparam>
    /// <param name="target">A reference to the dictionary.</param>
    /// <param name="key">The key, as a constant with a C# literal form.</param>
    /// <returns>A reference to <c>target[key]</c>.</returns>
    public static IReference<TValue> Item<TKey, TValue>(this IReference<Dictionary<TKey, TValue>> target, TKey key)
        => ElementPath<TValue>.OfLiteral(target, key);

    /// <summary>A dictionary entry under a key held in another reference: <c>target[key]</c>.</summary>
    /// <typeparam name="TKey">The dictionary's key type.</typeparam>
    /// <typeparam name="TValue">The dictionary's value type.</typeparam>
    /// <param name="target">A reference to the dictionary.</param>
    /// <param name="key">A reference to the key.</param>
    /// <returns>A reference to <c>target[key]</c>.</returns>
    public static IReference<TValue> Item<TKey, TValue>(
        this IReference<Dictionary<TKey, TValue>> target, IReference<TKey> key)
        => ElementPath<TValue>.OfReference(target, key);

    // A negative constant index is always a bug, and emitting it would need a unary
    // minus rather than a literal token -- refuse rather than build a malformed one.
    private static int NonNegative(int index)
        => index >= 0
            ? index
            : throw new ArgumentOutOfRangeException(nameof(index), index, "An element index cannot be negative.");
}
