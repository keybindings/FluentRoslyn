using System;
using System.Globalization;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace FluentRoslyn.Builders;

internal static class SyntaxLiterals
{
    /// <summary>
    /// Converts a constant value to its literal expression. Covers the primitive
    /// types with a natural C# literal form; anything else (enums, object
    /// construction, member access) must go through a raw expression string.
    /// </summary>
    internal static ExpressionSyntax Expression(object? value)
        => value switch
        {
            null => LiteralExpression(SyntaxKind.NullLiteralExpression),
            bool b => LiteralExpression(b ? SyntaxKind.TrueLiteralExpression : SyntaxKind.FalseLiteralExpression),
            string s => LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(s)),
            char c => LiteralExpression(SyntaxKind.CharacterLiteralExpression, Literal(c)),
            int i => LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(i)),
            uint ui => LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(ui)),
            long l => LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(l)),
            ulong ul => LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(ul)),
            float f => LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(f)),
            double d => LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(d)),
            decimal m => LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(m)),
            // Widening integrals have no dedicated Literal overload; emit the value as
            // an int literal, which converts implicitly at the assignment site.
            byte or sbyte or short or ushort =>
                LiteralExpression(SyntaxKind.NumericLiteralExpression,
                    Literal(Convert.ToInt32(value, CultureInfo.InvariantCulture))),
            // Reachable from initializers and from statements, so the advice names both
            // escape hatches rather than assuming which path got here.
            _ => throw new NotSupportedException(
                $"No literal form for '{value.GetType()}'. Supply it as raw text instead — " +
                "WithInitializerExpression for an initializer, AddStatement for a statement.")
        };
}
