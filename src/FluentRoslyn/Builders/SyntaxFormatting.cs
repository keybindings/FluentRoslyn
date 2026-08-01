using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace FluentRoslyn.Builders;

internal static class SyntaxFormatting
{
    /// <summary>
    /// Builds a modifier token list in canonical C# order: access modifiers, const,
    /// required, static, sealed, the inheritance modifier (virtual / abstract /
    /// override), readonly, async, partial.
    /// </summary>
    internal static SyntaxTokenList Modifiers(
        AccessModifier accessModifier,
        bool isStatic = false,
        bool isReadonly = false,
        bool isPartial = false,
        bool isConst = false,
        Inheritance inheritance = Inheritance.None,
        bool isSealed = false,
        bool isAsync = false,
        bool isRequired = false)
    {
        var tokens = new List<SyntaxToken>(accessModifier.Tokens);
        if (isConst) tokens.Add(SyntaxFactory.Token(SyntaxKind.ConstKeyword));
        if (isRequired) tokens.Add(SyntaxFactory.Token(SyntaxKind.RequiredKeyword));
        if (isStatic) tokens.Add(SyntaxFactory.Token(SyntaxKind.StaticKeyword));
        // A sealed *type* stands alone; a sealed member only exists as `sealed override`,
        // which Inheritance covers.
        if (isSealed) tokens.Add(SyntaxFactory.Token(SyntaxKind.SealedKeyword));
        tokens.AddRange(InheritanceTokens(inheritance));
        if (isReadonly) tokens.Add(SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword));
        if (isAsync) tokens.Add(SyntaxFactory.Token(SyntaxKind.AsyncKeyword));
        if (isPartial) tokens.Add(SyntaxFactory.Token(SyntaxKind.PartialKeyword));
        return SyntaxFactory.TokenList(tokens);
    }

    private static IEnumerable<SyntaxToken> InheritanceTokens(Inheritance inheritance)
    {
        switch (inheritance)
        {
            case Inheritance.Virtual:
                yield return SyntaxFactory.Token(SyntaxKind.VirtualKeyword);
                break;
            case Inheritance.Abstract:
                yield return SyntaxFactory.Token(SyntaxKind.AbstractKeyword);
                break;
            case Inheritance.Override:
                yield return SyntaxFactory.Token(SyntaxKind.OverrideKeyword);
                break;
            case Inheritance.SealedOverride:
                // C# orders these `sealed override`, not the reverse.
                yield return SyntaxFactory.Token(SyntaxKind.SealedKeyword);
                yield return SyntaxFactory.Token(SyntaxKind.OverrideKeyword);
                break;
        }
    }
}
