using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace FluentRoslyn.Builders;

/// <summary>
/// Parses raw C# fragments from user-supplied strings, rejecting anything Roslyn
/// flags with syntax diagnostics — so a malformed escape-hatch string throws instead
/// of silently emitting non-compiling source.
/// </summary>
internal static class SyntaxParse
{
    internal static ExpressionSyntax Expression(string text)
        => Checked(ParseExpression(Require(text)), text, "expression");

    internal static StatementSyntax Statement(string text)
        => Checked(ParseStatement(Require(text)), text, "statement");

    internal static TypeSyntax TypeName(string text)
        => Checked(ParseTypeName(Require(text)), text, "type name");

    private static T Checked<T>(T node, string text, string kind) where T : SyntaxNode
    {
        if (node.ContainsDiagnostics)
            throw new ArgumentException($"'{text}' is not a valid C# {kind}.", nameof(text));
        return node;
    }

    private static string Require(string text)
        => text ?? throw new ArgumentNullException(nameof(text));
}
