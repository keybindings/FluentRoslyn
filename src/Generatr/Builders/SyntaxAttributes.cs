using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Generatr.Builders;

internal static class SyntaxAttributes
{
    /// <summary>
    /// Parses a single attribute from raw text, with or without brackets, e.g.
    /// <c>"Obsolete"</c>, <c>"[Serializable]"</c>, or <c>"JsonProperty(\"name\")"</c>.
    /// </summary>
    internal static AttributeSyntax Attribute(string attribute)
    {
        if (attribute is null) throw new ArgumentNullException(nameof(attribute));

        var text = attribute.Trim();
        if (text.StartsWith("[", StringComparison.Ordinal) && text.EndsWith("]", StringComparison.Ordinal))
            text = text.Substring(1, text.Length - 2).Trim();

        // Attach the attribute to a throwaway declaration and lift it back out; there is
        // no public ParseAttribute entry point.
        if (ParseMemberDeclaration($"[{text}] class __AttrProbe {{ }}") is BaseTypeDeclarationSyntax decl
            && decl.AttributeLists.Count == 1
            && decl.AttributeLists[0].Attributes.Count == 1)
        {
            return decl.AttributeLists[0].Attributes[0];
        }

        throw new ArgumentException($"Could not parse a single attribute from '{attribute}'.", nameof(attribute));
    }

    /// <summary>
    /// Wraps each attribute in its own <c>[...]</c> list so they render one per line.
    /// Returns an empty list when there are no attributes.
    /// </summary>
    internal static SyntaxList<AttributeListSyntax> Lists(IReadOnlyList<AttributeSyntax> attributes)
        => attributes.Count == 0
            ? default
            : List(attributes.Select(a => AttributeList(SingletonSeparatedList(a))));
}
