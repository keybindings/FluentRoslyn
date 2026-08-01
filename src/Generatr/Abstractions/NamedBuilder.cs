using System;
using Microsoft.CodeAnalysis;

namespace Generatr.Abstractions;

/// <summary>
/// The base of every builder in the library. Holds the validated name and turns the
/// built syntax node into formatted source via <see cref="ToString"/>.
/// </summary>
public abstract class NamedBuilder : INamedBuilder
{
    /// <summary>Creates a named builder, validating the name up front.</summary>
    /// <param name="name">The construct's name.</param>
    /// <param name="validNameCheck">
    /// Validation applied to <paramref name="name"/>; throws if it is not acceptable.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    protected NamedBuilder(string name, Action<string> validNameCheck)
    {
        validNameCheck.Invoke(name ?? throw new ArgumentNullException(nameof(name)));
        Name = name;
    }

    /// <summary>The construct's name as it will be emitted.</summary>
    public string Name { get; }

    internal abstract SyntaxNode BuildSyntax();

    /// <summary>
    /// Line ending used for all emitted source. Pinned rather than taken from
    /// <see cref="Environment.NewLine"/> so generator output is byte-identical
    /// across operating systems.
    /// </summary>
    internal const string Eol = "\n";

    /// <summary>
    /// The generated C# source: 4-space indentation and <c>\n</c> line endings,
    /// regardless of host operating system.
    /// </summary>
    public override string ToString()
        => BuildSyntax().NormalizeWhitespace(eol: Eol).ToFullString();
}
