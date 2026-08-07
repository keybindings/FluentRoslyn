using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp;

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

    /// <summary><c>true</c>. Must be declared together with <see cref="False"/>.</summary>
    True,

    /// <summary><c>false</c>. Must be declared together with <see cref="True"/>.</summary>
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
public enum ConversionKind
{
    /// <summary><c>implicit operator T(…)</c> — applied without a cast.</summary>
    Implicit,

    /// <summary><c>explicit operator T(…)</c> — requires a cast.</summary>
    Explicit,
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

    internal static SyntaxKind TokenFor(OperatorKind kind) => Tokens[kind];

    internal static string SymbolFor(OperatorKind kind) => SyntaxFacts.GetText(Tokens[kind]);

    /// <summary>The operator that must accompany this one, or null when it stands alone.</summary>
    internal static OperatorKind? PartnerOf(OperatorKind kind)
        => Partners.TryGetValue(kind, out var partner) ? partner : null;
}
