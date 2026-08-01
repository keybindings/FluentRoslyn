using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Generatr.Builders;

internal static class SyntaxAttributes
{
    // The targets C# allows before the colon in `[target: Attr]`.
    private static readonly HashSet<string> Targets = new(StringComparer.Ordinal)
    {
        "assembly", "module", "field", "event", "method", "param", "property", "return", "type",
    };

    /// <summary>
    /// Parses a single attribute from raw text, with or without brackets, and with an
    /// optional target specifier: <c>"Obsolete"</c>, <c>"[Serializable]"</c>,
    /// <c>"JsonProperty(\"name\")"</c>, or <c>"return: NotNull"</c>.
    /// </summary>
    internal static AttributeListSyntax AttributeList(string attribute)
    {
        if (attribute is null) throw new ArgumentNullException(nameof(attribute));

        var text = attribute.Trim();
        if (text.StartsWith("[", StringComparison.Ordinal) && text.EndsWith("]", StringComparison.Ordinal))
            text = text.Substring(1, text.Length - 2).Trim();

        // Split the target off before parsing, so the probe only ever sees a bare
        // attribute — an unrecognised target then fails there rather than being dropped.
        var target = SplitTarget(ref text);
        var list = SingletonList(text);

        return target is null
            ? list
            : list.WithTarget(AttributeTargetSpecifier(Identifier(target)));
    }

    /// <summary>
    /// Splits a leading <c>target:</c> off the text, returning it and trimming the input.
    /// Null when there is none.
    /// </summary>
    private static string? SplitTarget(ref string text)
    {
        var colon = text.IndexOf(':');
        if (colon <= 0)
            return null;

        var candidate = text.Substring(0, colon).Trim();

        // Checking against the known targets keeps named arguments — `Obsolete(message:
        // "x")` — from being mistaken for one.
        if (!Targets.Contains(candidate))
            return null;

        text = text.Substring(colon + 1).Trim();
        return candidate;
    }

    private static AttributeListSyntax SingletonList(string attributeText)
        => SyntaxFactory.AttributeList(SingletonSeparatedList(Parse(attributeText)));

    private static AttributeSyntax Parse(string text)
    {
        // Attach the attribute to a throwaway declaration and lift it back out; there is
        // no public ParseAttribute entry point. The wrapper is always well-formed, so any
        // diagnostics come from the attribute itself — reject them rather than emit a
        // broken `[...]` (Roslyn's error recovery keeps the counts at 1 regardless).
        if (ParseMemberDeclaration($"[{text}] class __AttrProbe {{ }}") is BaseTypeDeclarationSyntax decl
            && !decl.ContainsDiagnostics
            && decl.AttributeLists.Count == 1
            && decl.AttributeLists[0].Attributes.Count == 1
            // Any target has already been split off by the caller, so one appearing here
            // means the text carried an unrecognised one. Lifting the bare attribute out
            // would silently drop it.
            && decl.AttributeLists[0].Target is null)
        {
            return decl.AttributeLists[0].Attributes[0];
        }

        throw new ArgumentException($"'{text}' is not a valid C# attribute.", nameof(text));
    }

    /// <summary>
    /// Each attribute is its own <c>[...]</c> list, so they render one per line and can
    /// carry independent targets. Returns an empty list when there are none.
    /// </summary>
    internal static SyntaxList<AttributeListSyntax> Lists(IReadOnlyList<AttributeListSyntax> attributes)
        => attributes.Count == 0 ? default : List(attributes);
}
