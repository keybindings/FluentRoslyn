using System;
using Microsoft.CodeAnalysis.CSharp;

namespace FluentRoslyn.Builders;

internal static class Identifiers
{
    /// <summary>
    /// Validates that a user-supplied name is a legal C# identifier and not a reserved
    /// keyword; a leading <c>@</c> verbatim prefix is allowed, and is the way to emit a
    /// keyword deliberately. Reflection-derived names (TypeNameBuilder) bypass this,
    /// since they can carry array/generic artifacts.
    /// </summary>
    internal static void Validate(string name)
    {
        if (name is null) throw new ArgumentNullException(nameof(name));

        var verbatim = name.StartsWith("@", StringComparison.Ordinal);
        var identifier = verbatim ? name.Substring(1) : name;
        if (!SyntaxFacts.IsValidIdentifier(identifier))
            throw new ArgumentException($"'{name}' is not a valid C# identifier.", nameof(name));

        // IsValidIdentifier is purely lexical, so every reserved keyword passes it: the
        // check that exists to stop unparseable output accepted `class` and `int`. Only
        // the verbatim form can carry a keyword, so the message names the escape hatch --
        // a name taken from consumer data or an ISymbol (which strips the @) has to get
        // it back, and there is nothing else the caller can do.
        if (!verbatim && SyntaxFacts.GetKeywordKind(identifier) != SyntaxKind.None)
            throw new ArgumentException(
                $"'{name}' is a C# keyword and cannot be used as a name; write '@{name}' to emit it as an identifier.",
                nameof(name));
    }
}
