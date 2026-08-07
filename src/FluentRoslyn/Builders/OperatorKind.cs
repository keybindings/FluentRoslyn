using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FluentRoslyn.Builders;

/// <summary>
/// An overloadable C# operator. Whether a symbol is unary or binary follows from how
/// many parameters the declaration has — <c>+</c> and <c>-</c> are both.
/// </summary>
public enum OperatorKind
{
    /// <summary><c>==</c>. Must be declared together with <see cref="Inequality"/>.</summary>
    Equality,

    /// <summary><c>!=</c>. Must be declared together with <see cref="Equality"/>.</summary>
    Inequality,

    /// <summary><c>&lt;</c>. Must be declared together with <see cref="GreaterThan"/>.</summary>
    LessThan,

    /// <summary><c>&gt;</c>. Must be declared together with <see cref="LessThan"/>.</summary>
    GreaterThan,

    /// <summary><c>&lt;=</c>. Must be declared together with <see cref="GreaterThanOrEqual"/>.</summary>
    LessThanOrEqual,

    /// <summary><c>&gt;=</c>. Must be declared together with <see cref="LessThanOrEqual"/>.</summary>
    GreaterThanOrEqual,

    /// <summary><c>true</c>. Must be declared together with <see cref="False"/>, and must return <c>bool</c>.</summary>
    True,

    /// <summary><c>false</c>. Must be declared together with <see cref="True"/>, and must return <c>bool</c>.</summary>
    False,

    /// <summary><c>+</c>, unary or binary.</summary>
    Plus,

    /// <summary><c>-</c>, unary or binary.</summary>
    Minus,

    /// <summary><c>*</c>.</summary>
    Multiply,

    /// <summary><c>/</c>.</summary>
    Divide,

    /// <summary><c>%</c>.</summary>
    Modulo,

    /// <summary><c>&amp;</c>.</summary>
    BitwiseAnd,

    /// <summary><c>|</c>.</summary>
    BitwiseOr,

    /// <summary><c>^</c>.</summary>
    ExclusiveOr,

    /// <summary><c>&lt;&lt;</c>.</summary>
    LeftShift,

    /// <summary><c>&gt;&gt;</c>.</summary>
    RightShift,

    /// <summary><c>&gt;&gt;&gt;</c>, the unsigned right shift (C# 11).</summary>
    UnsignedRightShift,

    /// <summary><c>!</c>.</summary>
    LogicalNot,

    /// <summary><c>~</c>.</summary>
    OnesComplement,

    /// <summary><c>++</c>.</summary>
    Increment,

    /// <summary><c>--</c>.</summary>
    Decrement,
}

/// <summary>Whether a conversion operator is applied implicitly or requires a cast.</summary>
/// <remarks>
/// Zero is deliberately not a defined value: <c>default(ConversionKind)</c> is invalid
/// and rejected, so a generator that computes the argument and accidentally passes a
/// default cannot silently declare an implicit conversion — the most dangerous kind to
/// declare by accident.
/// </remarks>
public enum ConversionKind
{
    /// <summary><c>implicit operator T(…)</c> — applied without a cast.</summary>
    Implicit = 1,

    /// <summary><c>explicit operator T(…)</c> — requires a cast.</summary>
    Explicit = 2,
}

internal static class Operators
{
    private static readonly Dictionary<OperatorKind, SyntaxKind> Tokens = new()
    {
        [OperatorKind.Equality] = SyntaxKind.EqualsEqualsToken,
        [OperatorKind.Inequality] = SyntaxKind.ExclamationEqualsToken,
        [OperatorKind.LessThan] = SyntaxKind.LessThanToken,
        [OperatorKind.GreaterThan] = SyntaxKind.GreaterThanToken,
        [OperatorKind.LessThanOrEqual] = SyntaxKind.LessThanEqualsToken,
        [OperatorKind.GreaterThanOrEqual] = SyntaxKind.GreaterThanEqualsToken,
        [OperatorKind.True] = SyntaxKind.TrueKeyword,
        [OperatorKind.False] = SyntaxKind.FalseKeyword,
        [OperatorKind.Plus] = SyntaxKind.PlusToken,
        [OperatorKind.Minus] = SyntaxKind.MinusToken,
        [OperatorKind.Multiply] = SyntaxKind.AsteriskToken,
        [OperatorKind.Divide] = SyntaxKind.SlashToken,
        [OperatorKind.Modulo] = SyntaxKind.PercentToken,
        [OperatorKind.BitwiseAnd] = SyntaxKind.AmpersandToken,
        [OperatorKind.BitwiseOr] = SyntaxKind.BarToken,
        [OperatorKind.ExclusiveOr] = SyntaxKind.CaretToken,
        [OperatorKind.LeftShift] = SyntaxKind.LessThanLessThanToken,
        [OperatorKind.RightShift] = SyntaxKind.GreaterThanGreaterThanToken,
        [OperatorKind.UnsignedRightShift] = SyntaxKind.GreaterThanGreaterThanGreaterThanToken,
        [OperatorKind.LogicalNot] = SyntaxKind.ExclamationToken,
        [OperatorKind.OnesComplement] = SyntaxKind.TildeToken,
        [OperatorKind.Increment] = SyntaxKind.PlusPlusToken,
        [OperatorKind.Decrement] = SyntaxKind.MinusMinusToken,
    };

    /// <summary>
    /// The operators C# refuses to accept alone (CS0216 and friends). Declaring one
    /// without its partner is source the *consumer's* build rejects, so the type builder
    /// refuses to emit it — the same stance the enum builder takes on an out-of-range
    /// member value.
    /// </summary>
    private static readonly Dictionary<OperatorKind, OperatorKind> Partners = new()
    {
        [OperatorKind.Equality] = OperatorKind.Inequality,
        [OperatorKind.Inequality] = OperatorKind.Equality,
        [OperatorKind.LessThan] = OperatorKind.GreaterThan,
        [OperatorKind.GreaterThan] = OperatorKind.LessThan,
        [OperatorKind.LessThanOrEqual] = OperatorKind.GreaterThanOrEqual,
        [OperatorKind.GreaterThanOrEqual] = OperatorKind.LessThanOrEqual,
        [OperatorKind.True] = OperatorKind.False,
        [OperatorKind.False] = OperatorKind.True,
    };

    // How many parameters each operator accepts. Plus and Minus are genuinely both
    // unary and binary; everything else is exactly one or the other.
    private static readonly HashSet<OperatorKind> UnaryOnly =
    [
        OperatorKind.LogicalNot, OperatorKind.OnesComplement,
        OperatorKind.Increment, OperatorKind.Decrement,
        OperatorKind.True, OperatorKind.False,
    ];

    private static readonly HashSet<OperatorKind> UnaryOrBinary = [OperatorKind.Plus, OperatorKind.Minus];

    internal static SyntaxKind TokenFor(OperatorKind kind) => Tokens[kind];

    internal static string SymbolFor(OperatorKind kind) => SyntaxFacts.GetText(Tokens[kind]);

    /// <summary>
    /// Rejects an <see cref="OperatorKind"/> that is not one of the defined values —
    /// realistic when a generator maps discovered symbol names to kinds and misses a
    /// case — with an error naming the kind, rather than a bare
    /// <c>KeyNotFoundException</c> from a private dictionary.
    /// </summary>
    internal static OperatorKind Defined(OperatorKind kind)
        => Tokens.ContainsKey(kind)
            ? kind
            : throw new ArgumentException($"'{(int)kind}' is not a defined OperatorKind.", nameof(kind));

    /// <summary>Rejects a <see cref="ConversionKind"/> outside the defined values, including <c>default</c>.</summary>
    internal static ConversionKind Defined(ConversionKind kind)
        => kind is ConversionKind.Implicit or ConversionKind.Explicit
            ? kind
            : throw new ArgumentException(
                $"'{(int)kind}' is not a defined ConversionKind. Note that default(ConversionKind) is " +
                "deliberately invalid, so a computed argument cannot silently declare an implicit conversion.",
                nameof(kind));

    /// <summary>The operator that must accompany this one, or null when it stands alone.</summary>
    internal static OperatorKind? PartnerOf(OperatorKind kind)
        => Partners.TryGetValue(kind, out var partner) ? partner : null;

    /// <summary>Whether <paramref name="count"/> parameters is legal for this operator.</summary>
    internal static bool ArityIsLegal(OperatorKind kind, int count)
        => UnaryOrBinary.Contains(kind) ? count is 1 or 2
            : UnaryOnly.Contains(kind) ? count == 1
            : count == 2;

    /// <summary>Describes the legal arity in an error message.</summary>
    internal static string ArityDescription(OperatorKind kind)
        => UnaryOrBinary.Contains(kind) ? "one parameter (unary) or two (binary)"
            : UnaryOnly.Contains(kind) ? "exactly one parameter"
            : "exactly two parameters";

    /// <summary>
    /// The type text used to compare two operator signatures. Parsed type syntax keeps
    /// the spelling it was written with, so without this <c>Wrapper&lt;string,int&gt;</c>
    /// and <c>Wrapper&lt;string, int&gt;</c> — or <c>int</c> and <c>System.Int32</c> —
    /// would count as different types and legal code would be refused. Whitespace is
    /// normalized and the built-in aliases are rewritten to their keywords; distinct
    /// spellings beyond that (an alias a using directive introduces, say) remain
    /// distinct, which errs toward accepting code the consumer's compiler then judges.
    /// </summary>
    internal static string CanonicalTypeText(TypeSyntax type)
        => ((TypeSyntax)new BuiltInAliasRewriter().Visit(type)).NormalizeWhitespace().ToString();

    // Rewrites [global::]System.X to the predefined keyword for the 15 built-in types,
    // token-wise rather than by string replacement so `MySystem.Int32X` is untouched.
    private sealed class BuiltInAliasRewriter : CSharpSyntaxRewriter
    {
        private static readonly Dictionary<string, SyntaxKind> Keywords = new(StringComparer.Ordinal)
        {
            ["Boolean"] = SyntaxKind.BoolKeyword,
            ["Byte"] = SyntaxKind.ByteKeyword,
            ["SByte"] = SyntaxKind.SByteKeyword,
            ["Int16"] = SyntaxKind.ShortKeyword,
            ["UInt16"] = SyntaxKind.UShortKeyword,
            ["Int32"] = SyntaxKind.IntKeyword,
            ["UInt32"] = SyntaxKind.UIntKeyword,
            ["Int64"] = SyntaxKind.LongKeyword,
            ["UInt64"] = SyntaxKind.ULongKeyword,
            ["Char"] = SyntaxKind.CharKeyword,
            ["Single"] = SyntaxKind.FloatKeyword,
            ["Double"] = SyntaxKind.DoubleKeyword,
            ["Decimal"] = SyntaxKind.DecimalKeyword,
            ["String"] = SyntaxKind.StringKeyword,
            ["Object"] = SyntaxKind.ObjectKeyword,
        };

        public override SyntaxNode? VisitQualifiedName(QualifiedNameSyntax node)
        {
            if (IsSystem(node.Left) && node.Right is IdentifierNameSyntax { Identifier.ValueText: var name } &&
                Keywords.TryGetValue(name, out var keyword))
                return SyntaxFactory.PredefinedType(SyntaxFactory.Token(keyword));

            return base.VisitQualifiedName(node);
        }

        private static bool IsSystem(NameSyntax left) => left switch
        {
            IdentifierNameSyntax { Identifier.ValueText: "System" } => true,
            AliasQualifiedNameSyntax { Alias.Identifier.ValueText: "global", Name.Identifier.ValueText: "System" } => true,
            _ => false,
        };
    }
}
