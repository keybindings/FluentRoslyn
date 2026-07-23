using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Generatr.Builders;

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

    public override string ToString()
        => string.Join(" ", _keywords.Select(k => SyntaxFactory.Token(k).Text));

    public static readonly AccessModifier Public = new(0, SyntaxKind.PublicKeyword);
    public static readonly AccessModifier Internal = new(1, SyntaxKind.InternalKeyword);
    public static readonly AccessModifier Protected = new(2, SyntaxKind.ProtectedKeyword);
    public static readonly AccessModifier ProtectedInternal = new(3, SyntaxKind.ProtectedKeyword, SyntaxKind.InternalKeyword);
    public static readonly AccessModifier PrivateProtected = new(4, SyntaxKind.PrivateKeyword, SyntaxKind.ProtectedKeyword);
    public static readonly AccessModifier Private = new(5, SyntaxKind.PrivateKeyword);

    /// <summary>
    /// No explicit access modifier — the member's accessibility is left implicit
    /// (private for members, internal for types). Required for classic partial methods,
    /// which cannot carry an accessibility keyword. Sorts with private for member order.
    /// </summary>
    public static readonly AccessModifier None = new(5);
}
