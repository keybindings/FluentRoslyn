using System;
using FluentRoslyn.Abstractions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace FluentRoslyn.Builders;

/// <summary>
/// A reference built from another reference — <c>a.b</c> or <c>a[i]</c>. Emission builds
/// the target's expression first (which is where shadow qualification happens) and then
/// composes one step onto it, so a chain of any depth falls out of the recursion.
/// </summary>
/// <remarks>
/// Kept internal because it is an implementation detail of the public
/// <see cref="Abstractions.IReference{T}"/> surface: callers see only another reference.
/// </remarks>
internal interface IReferencePath
{
    /// <summary>The reference this one is built from.</summary>
    IReference Target { get; }

    /// <summary>
    /// Composes this step onto the target's already-built expression.
    /// <paramref name="qualify"/> builds any further reference this step contains — an
    /// index — through the same shadow-qualification rules as every other position.
    /// </summary>
    ExpressionSyntax Compose(ExpressionSyntax target, Func<IReference, ExpressionSyntax> qualify);

    /// <summary>
    /// Whether the whole chain is legal inside <c>nameof</c>. Member access is; element
    /// access is not, at any position in the chain — measured as CS8081 for
    /// <c>nameof(items[0])</c> and CS8082 for <c>nameof(items[0].Length)</c>.
    /// </summary>
    bool CanNameOf { get; }
}

/// <summary>
/// A member access step: <c>target.Name</c>. The result is a reference like any other, so
/// it can be assigned to, assigned from, called on, passed as an argument, or returned.
/// </summary>
/// <remarks>
/// The typed and untyped forms differ only in what interface they carry, so everything
/// else is here. It was written out twice, character for character — including the
/// <see cref="CanNameOf"/> recursion, which is what decides whether <c>ThrowIfNull</c>
/// refuses to emit, and so is the least obvious thing in the file to keep in step by hand.
/// </remarks>
internal abstract class MemberPath : IReferencePath
{
    private readonly string _memberName;

    private protected MemberPath(IReference target, string memberName)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Identifiers.Validate(memberName);
        _memberName = memberName;
    }

    public IReference Target { get; }

    // Not a bare identifier — see IReference.Name, which documents the composed case.
    public string Name => $"{Target.Name}.{_memberName}";

    public bool CanNameOf => Target is not IReferencePath path || path.CanNameOf;

    public ExpressionSyntax Compose(ExpressionSyntax target, Func<IReference, ExpressionSyntax> qualify)
        => MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, target, IdentifierName(_memberName));
}

/// <summary>A member access whose type is asserted by the caller.</summary>
/// <typeparam name="T">The member's type.</typeparam>
internal sealed class MemberPath<T> : MemberPath, IReference<T>
{
    internal MemberPath(IReference target, string memberName) : base(target, memberName)
    {
    }
}

/// <summary>
/// A member access step whose type is unknown: <c>target.Name</c> where the member
/// belongs to a type the generator only discovered. Reads and writes a location, so it
/// is a reference rather than a value — <c>_inner.Count</c> can be returned, passed, or
/// assigned to.
/// </summary>
/// <remarks>
/// Deliberately carries no <see cref="IRawTypeInfo"/>: nothing here knows the member's
/// declared type, and inventing one would be worse than admitting it. <c>AssignRaw</c>
/// compares declared types only when both sides report one, so an assignment involving
/// this degrades to unchecked rather than to wrong.
/// </remarks>
internal sealed class RawMemberPath : MemberPath, IRawReference
{
    internal RawMemberPath(IReference target, string memberName) : base(target, memberName)
    {
    }
}

/// <summary>
/// An element access step: <c>target[index]</c>. The index is either a constant or
/// another reference; which one it is was settled by the compiler at the call site, so
/// nothing here re-checks it.
/// </summary>
/// <typeparam name="T">The element's type.</typeparam>
internal sealed class ElementPath<T> : IReference<T>, IReferencePath
{
    private readonly object? _literalIndex;
    private readonly IReference? _referenceIndex;

    private ElementPath(IReference target, object? literalIndex, IReference? referenceIndex)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        _literalIndex = literalIndex;
        _referenceIndex = referenceIndex;
    }

    /// <summary>Creates <c>target[literal]</c> from a constant index or key.</summary>
    internal static ElementPath<T> OfLiteral(IReference target, object? index)
        => new(target, index, referenceIndex: null);

    /// <summary>Creates <c>target[i]</c> from an index or key that is itself a reference.</summary>
    internal static ElementPath<T> OfReference(IReference target, IReference index)
        => new(target, literalIndex: null, index ?? throw new ArgumentNullException(nameof(index)));

    public IReference Target { get; }

    // Not a bare identifier — see IReference.Name, which documents the composed case.
    public string Name => $"{Target.Name}[{_referenceIndex?.Name ?? _literalIndex?.ToString() ?? "null"}]";

    // An element access is never legal inside nameof, whatever the rest of the chain does.
    public bool CanNameOf => false;

    public ExpressionSyntax Compose(ExpressionSyntax target, Func<IReference, ExpressionSyntax> qualify)
    {
        var index = _referenceIndex is null
            ? SyntaxLiterals.Expression(_literalIndex)
            : qualify(_referenceIndex);

        return ElementAccessExpression(target)
            .WithArgumentList(BracketedArgumentList(SingletonSeparatedList(Argument(index))));
    }
}
