using System;
using Microsoft.CodeAnalysis.CSharp;

namespace FluentRoslyn.Builders;

internal static class Identifiers
{
    /// <summary>
    /// Validates that a user-supplied name is a legal C# identifier; a leading
    /// <c>@</c> verbatim prefix is allowed. Reflection-derived names (TypeNameBuilder)
    /// bypass this, since they can carry array/generic artifacts.
    /// </summary>
    internal static void Validate(string name)
    {
        if (name is null) throw new ArgumentNullException(nameof(name));

        var identifier = name.StartsWith("@", StringComparison.Ordinal) ? name.Substring(1) : name;
        if (!SyntaxFacts.IsValidIdentifier(identifier))
            throw new ArgumentException($"'{name}' is not a valid C# identifier.", nameof(name));
    }
}
