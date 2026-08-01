using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Generatr.Builders;

/// <summary>
/// A member or type accessibility level. Instances are a fixed set of singletons —
/// use the static fields rather than constructing one.
/// </summary>
public sealed class AccessModifier
{
    private readonly SyntaxKind[] _keywords;

    private AccessModifier(int accessabilityLevel, params SyntaxKind[] keywords)
    {
        AccessabilityLevel = accessabilityLevel;
        _keywords = keywords;
    }

    internal int AccessabilityLevel { get; }

    internal IEnumerable<SyntaxToken> Tokens => _keywords.Select(SyntaxFactory.Token);

    /// <summary>The C# keywords for this modifier, e.g. <c>"protected internal"</c>.</summary>
    public override string ToString()
        => string.Join(" ", _keywords.Select(k => SyntaxFactory.Token(k).Text));

    /// <summary><c>public</c></summary>
    public static readonly AccessModifier Public = new(0, SyntaxKind.PublicKeyword);

    /// <summary><c>internal</c></summary>
    public static readonly AccessModifier Internal = new(1, SyntaxKind.InternalKeyword);

    /// <summary><c>protected</c></summary>
    public static readonly AccessModifier Protected = new(2, SyntaxKind.ProtectedKeyword);

    /// <summary><c>protected internal</c></summary>
    public static readonly AccessModifier ProtectedInternal = new(3, SyntaxKind.ProtectedKeyword, SyntaxKind.InternalKeyword);

    /// <summary><c>private protected</c></summary>
    public static readonly AccessModifier PrivateProtected = new(4, SyntaxKind.PrivateKeyword, SyntaxKind.ProtectedKeyword);

    /// <summary><c>private</c></summary>
    public static readonly AccessModifier Private = new(5, SyntaxKind.PrivateKeyword);

    /// <summary>
    /// No explicit access modifier — the member's accessibility is left implicit
    /// (private for members, internal for types). Required for classic partial methods,
    /// which cannot carry an accessibility keyword. Sorts with private for member order.
    /// </summary>
    public static readonly AccessModifier None = new(5);
}
