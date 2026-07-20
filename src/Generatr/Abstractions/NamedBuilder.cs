using System;
using Microsoft.CodeAnalysis;

namespace Generatr.Abstractions;

public abstract class NamedBuilder : INamedBuilder
{
    protected NamedBuilder(string name, Action<string> validNameCheck)
    {
        validNameCheck.Invoke(name ?? throw new ArgumentNullException(nameof(name)));
        Name = name;
    }

    public string Name { get; }

    internal abstract SyntaxNode BuildSyntax();

    /// <summary>
    /// Line ending used for all emitted source. Pinned rather than taken from
    /// <see cref="Environment.NewLine"/> so generator output is byte-identical
    /// across operating systems.
    /// </summary>
    internal const string Eol = "\n";

    public override string ToString()
        => BuildSyntax().NormalizeWhitespace(eol: Eol).ToFullString();
}
