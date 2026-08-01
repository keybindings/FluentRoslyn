using System;
using FluentRoslyn.Builders;
using Microsoft.CodeAnalysis;

namespace FluentRoslyn.Abstractions;

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
    /// Layout for this builder's output. Fixed at the library default here; the
    /// top-level type builders let it be overridden. Pinned rather than taken from
    /// <see cref="Environment.NewLine"/> so output is byte-identical across operating
    /// systems.
    /// </summary>
    private protected virtual SourceFormatting Formatting => SourceFormatting.Default;

    /// <summary>
    /// The generated C# source. Four-space indentation and <c>\n</c> line endings unless
    /// the builder's formatting has been overridden.
    /// </summary>
    public override string ToString()
        => BuildSyntax()
            .NormalizeWhitespace(Formatting.Indentation, Formatting.LineEndings)
            .ToFullString();
}
