using System;
using System.Linq;

namespace FluentRoslyn.Builders;

/// <summary>
/// How generated source is laid out. The defaults — four spaces and <c>\n</c> — are
/// deliberate: pinning them rather than reading <see cref="Environment.NewLine"/> keeps
/// output byte-identical across operating systems, so generated files hash the same
/// everywhere. Override them only when a consuming codebase demands it.
/// </summary>
public sealed class SourceFormatting
{
    /// <summary>Four-space indentation with <c>\n</c> line endings.</summary>
    public static readonly SourceFormatting Default = new("    ", "\n");

    private SourceFormatting(string indentation, string lineEndings)
    {
        Indentation = indentation;
        LineEndings = lineEndings;
    }

    /// <summary>The string used for one level of indentation.</summary>
    public string Indentation { get; }

    /// <summary>The line ending sequence.</summary>
    public string LineEndings { get; }

    /// <summary>Returns a copy using the given indentation, e.g. <c>"\t"</c> or two spaces.</summary>
    /// <exception cref="ArgumentException">The value is not whitespace.</exception>
    public SourceFormatting WithIndentation(string indentation)
    {
        if (indentation is null) throw new ArgumentNullException(nameof(indentation));

        // Anything else would corrupt the emitted source rather than merely restyle it.
        if (!indentation.All(char.IsWhiteSpace))
            throw new ArgumentException("Indentation must be whitespace.", nameof(indentation));

        return new SourceFormatting(indentation, LineEndings);
    }

    /// <summary>Returns a copy using the given line endings — <c>"\n"</c> or <c>"\r\n"</c>.</summary>
    /// <exception cref="ArgumentException">The value is not a line ending sequence.</exception>
    public SourceFormatting WithLineEndings(string lineEndings)
    {
        if (lineEndings is null) throw new ArgumentNullException(nameof(lineEndings));

        if (lineEndings != "\n" && lineEndings != "\r\n" && lineEndings != "\r")
            throw new ArgumentException(@"Line endings must be ""\n"", ""\r\n"", or ""\r"".", nameof(lineEndings));

        return new SourceFormatting(Indentation, lineEndings);
    }
}
