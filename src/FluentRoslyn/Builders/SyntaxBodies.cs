using System;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace FluentRoslyn.Builders;

/// <summary>
/// How a member's body is built: the raw-statement escape hatch, and the expression-body
/// form every body-bearing member shares.
/// </summary>
/// <remarks>
/// The expression-body routine used to be written out at each of five sites, with four
/// different refusal messages between them — one of which named no member at all, so a
/// generator author was told that "an accessor" somewhere had two bodies. The shape is the
/// same everywhere (arrow clause, then a semicolon instead of a block) and so is the rule,
/// which is why they belong here rather than beside each declaration.
/// </remarks>
internal static class SyntaxBodies
{
    /// <summary>
    /// Parses a single complete C# statement, e.g. <c>"return a + b;"</c> or an
    /// <c>if</c> block. The raw-text escape hatch until statements get a fluent model.
    /// </summary>
    internal static StatementSyntax Statement(string statement)
        => SyntaxParse.Statement(statement);

    /// <summary>
    /// Applies an expression body to a method, constructor or operator.
    /// </summary>
    /// <param name="declaration">The declaration to give a body.</param>
    /// <param name="expression">The expression the body evaluates.</param>
    /// <param name="statementCount">How many statements the member also declares.</param>
    /// <param name="context">How the member names itself, e.g. <c>Method 'Foo'</c>.</param>
    internal static TDeclaration ExpressionBodied<TDeclaration>(
        TDeclaration declaration,
        ExpressionSyntax expression,
        int statementCount,
        string context)
        where TDeclaration : BaseMethodDeclarationSyntax
    {
        RefuseSecondBody(statementCount > 0, context, "statements");

        // Every With… returns the concrete node it was called on; the base type is only
        // how Roslyn declares the signature.
        return (TDeclaration)declaration
            .WithExpressionBody(ArrowExpressionClause(expression))
            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
    }

    /// <summary>Applies a whole-property expression body: <c>public int Count =&gt; _count;</c>.</summary>
    internal static PropertyDeclarationSyntax ExpressionBodied(
        PropertyDeclarationSyntax property,
        ExpressionSyntax expression,
        bool hasAccessorBody,
        string context)
    {
        RefuseSecondBody(hasAccessorBody, context, "accessor bodies");

        return property
            .WithExpressionBody(ArrowExpressionClause(expression))
            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
    }

    /// <summary>Applies an expression body to one accessor: <c>get =&gt; _count;</c>.</summary>
    internal static AccessorDeclarationSyntax ExpressionBodied(
        AccessorDeclarationSyntax accessor,
        ExpressionSyntax expression,
        bool hasStatementBody,
        string context)
    {
        RefuseSecondBody(hasStatementBody, context, "a statement body");

        return accessor
            .WithExpressionBody(ArrowExpressionClause(expression))
            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
    }

    private static void RefuseSecondBody(bool hasSecondBody, string context, string second)
    {
        if (hasSecondBody)
            throw new InvalidOperationException(
                $"{context} cannot have both an expression body and {second}.");
    }
}
