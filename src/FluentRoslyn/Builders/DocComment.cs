using System;
using System.Collections.Generic;

using Microsoft.CodeAnalysis;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace FluentRoslyn.Builders;

/// <summary>
/// Accumulates an XML documentation comment and renders it as leading trivia. Shared by
/// every builder that can carry docs, so the escaping and layout live in one place.
/// </summary>
internal sealed class DocComment
{
    private readonly List<(string Name, string Text)> _parameters = [];
    private string? _summary;
    private string? _returns;

    internal bool IsEmpty => _summary is null && _returns is null && _parameters.Count == 0;

    internal void SetSummary(string text)
        => _summary = text ?? throw new ArgumentNullException(nameof(text));

    internal void SetReturns(string text)
        => _returns = text ?? throw new ArgumentNullException(nameof(text));

    internal void AddParameter(string name, string text)
    {
        Identifiers.Validate(name);
        _parameters.Add((name, text ?? throw new ArgumentNullException(nameof(text))));
    }

    /// <summary>
    /// Renders the comment as leading trivia. Must be attached before
    /// <c>NormalizeWhitespace</c> runs, which is what indents it to match the member.
    /// </summary>
    internal SyntaxTriviaList Build()
    {
        if (IsEmpty) return default;

        var trivia = new List<SyntaxTrivia>();

        if (_summary is { } summary)
        {
            Line(trivia, "/// <summary>");
            foreach (var line in Lines(summary))
                Line(trivia, "/// " + Escape(line));
            Line(trivia, "/// </summary>");
        }

        foreach (var (name, description) in _parameters)
            Line(trivia, $"/// <param name=\"{name}\">{Escape(Flatten(description))}</param>");

        if (_returns is { } returns)
            Line(trivia, $"/// <returns>{Escape(Flatten(returns))}</returns>");

        return TriviaList(trivia);
    }

    // Emitted as plain comment trivia rather than parsed documentation trivia:
    // NormalizeWhitespace reformats the XML inside structured doc trivia, turning
    // `name="x"` into `name = "x"`. The output text is identical to a hand-written
    // doc comment either way, so the compiler reads it the same.
    private static void Line(List<SyntaxTrivia> trivia, string text)
    {
        trivia.Add(Comment(text));
        trivia.Add(LineFeed);
    }

    private static IEnumerable<string> Lines(string text)
        => text.Replace("\r\n", "\n").Split('\n');

    private static string Flatten(string text)
        => string.Join(" ", Lines(text));

    // Doc text becomes XML, so the markup characters have to be escaped or a summary
    // mentioning e.g. List<T> would emit a malformed comment.
    private static string Escape(string text)
        => text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
