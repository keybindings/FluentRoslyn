using System;
using System.Globalization;
using Microsoft.CodeAnalysis;
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
            float f => SingleExpression(f),
            double d => DoubleExpression(d),
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

    // double.NaN / double.PositiveInfinity / double.NegativeInfinity (and the float
    // equivalents) are themselves declared `const` in the BCL, so spelling the special
    // values as member access on the predefined-type keyword — rather than as text that
    // merely prints the same way, e.g. "NaN" — binds with no using directive, stays legal
    // in a const initializer, and reads back as exactly the value that went in.
    private static ExpressionSyntax DoubleExpression(double d)
    {
        if (double.IsNaN(d)) return SpecialMember(SyntaxKind.DoubleKeyword, nameof(double.NaN));
        if (double.IsPositiveInfinity(d)) return SpecialMember(SyntaxKind.DoubleKeyword, nameof(double.PositiveInfinity));
        if (double.IsNegativeInfinity(d)) return SpecialMember(SyntaxKind.DoubleKeyword, nameof(double.NegativeInfinity));
        // netstandard2.0 has no double.IsNegative; dividing into it does the same job —
        // 1.0 / -0.0 is negative infinity, 1.0 / +0.0 is positive infinity.
        if (d == 0 && double.IsNegativeInfinity(1.0 / d)) return NegativeZero(Literal("0.0", 0.0));
        return LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(d));
    }

    private static ExpressionSyntax SingleExpression(float f)
    {
        if (float.IsNaN(f)) return SpecialMember(SyntaxKind.FloatKeyword, nameof(float.NaN));
        if (float.IsPositiveInfinity(f)) return SpecialMember(SyntaxKind.FloatKeyword, nameof(float.PositiveInfinity));
        if (float.IsNegativeInfinity(f)) return SpecialMember(SyntaxKind.FloatKeyword, nameof(float.NegativeInfinity));
        if (f == 0 && float.IsNegativeInfinity(1f / f)) return NegativeZero(Literal("0F", 0f));
        return LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(f));
    }

    private static ExpressionSyntax SpecialMember(SyntaxKind predefinedKeyword, string memberName)
        => MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            PredefinedType(Token(predefinedKeyword)),
            IdentifierName(memberName));

    // Roslyn's Literal(double)/Literal(float) render -0.0 as the text "-0", which a real
    // compiler reads back as the *integer* literal 0 negated — positive zero again once it
    // converts to the target type, because generated source is text, not syntax nodes: it
    // gets written to a .cs file and re-lexed from scratch. Negating a positive-zero token
    // whose text is unambiguously floating-point ("0.0"/"0F", passed in by the caller
    // rather than left to Literal's default formatting) keeps the sign through that
    // round trip: unary minus on a floating-point operand is well-defined for zero (it
    // flips the sign bit, per IEEE 754) and, unlike System.BitConverter.Int64BitsToDouble,
    // stays a compile-time constant expression, so it still works inside a const initializer.
    private static ExpressionSyntax NegativeZero(SyntaxToken positiveZero)
        => PrefixUnaryExpression(
            SyntaxKind.UnaryMinusExpression,
            LiteralExpression(SyntaxKind.NumericLiteralExpression, positiveZero));
}
