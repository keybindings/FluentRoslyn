using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Generatr.Builders;

internal static class SyntaxFormatting
{
    /// <summary>
    /// Builds a modifier token list in canonical order: access modifiers, static, readonly, partial.
    /// </summary>
    internal static SyntaxTokenList Modifiers(AccessModifier accessModifier, bool isStatic = false, bool isReadonly = false, bool isPartial = false)
    {
        var tokens = new List<SyntaxToken>(accessModifier.Tokens);
        if (isStatic) tokens.Add(SyntaxFactory.Token(SyntaxKind.StaticKeyword));
        if (isReadonly) tokens.Add(SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword));
        if (isPartial) tokens.Add(SyntaxFactory.Token(SyntaxKind.PartialKeyword));
        return SyntaxFactory.TokenList(tokens);
    }
}
