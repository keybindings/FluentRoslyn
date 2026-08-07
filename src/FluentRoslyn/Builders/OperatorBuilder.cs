using System;
using System.Collections.Generic;
using FluentRoslyn.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace FluentRoslyn.Builders;

/// <summary>
/// Lets the type builder interrogate an operator so it can enforce the pairings C#
/// requires — <c>==</c> with <c>!=</c>, and a <c>checked</c> form with its unchecked
/// counterpart.
/// </summary>
internal interface IOperatorMember
{
    /// <summary>Which operator this is, or null for a conversion.</summary>
    OperatorKind? Kind { get; }

    /// <summary>Whether this is the <c>checked</c> form.</summary>
    bool IsChecked { get; }

    /// <summary>
    /// Identifies the overload a <c>checked</c> form must have an unchecked twin of:
    /// the operator plus its arity, or the conversion's target type. Unary and binary
    /// <c>-</c> are different operators, so arity is part of it.
    /// </summary>
    string SignatureKey { get; }

    /// <summary>How this operator names itself in an error message.</summary>
    string Display { get; }

    /// <summary>Rejects a <c>checked</c> form the language does not allow.</summary>
    void ValidateChecked(string typeName);
}

/// <summary>
/// The shared surface of an operator declaration — parameters, a body, docs, and the
/// two modifiers an operator may carry.
/// </summary>
/// <remarks>
/// <para>
/// An operator is a method in every way that matters to a body, so this derives from
/// <see cref="StatementBuilder{TSelf}"/> and inherits the whole statement API. It does
/// <em>not</em> derive from <see cref="MethodBuilderBase{TSelf}"/>, deliberately: that
/// carries <c>Partial</c>, <c>Async</c>, <c>Virtual</c>, <c>Override</c> and the
/// <c>AsCallable</c> families, none of which an operator can be.
/// </para>
/// <para>
/// Accessibility and staticness genuinely are fixed — C# requires <c>public static</c>
/// and rejects anything else (CS0558), so there is nothing to offer there. What an
/// operator <em>can</em> carry is <see cref="Unsafe"/> and, since C# 11,
/// <see cref="Checked"/>; both are here.
/// </para>
/// </remarks>
/// <typeparam name="TSelf">The concrete operator builder type.</typeparam>
public abstract class OperatorBuilderBase<TSelf> : StatementBuilder<TSelf>, IMemberSyntaxBuilder, IOperatorMember
    where TSelf : OperatorBuilderBase<TSelf>
{
    private readonly OperatorKind? _kind;
    private readonly ConversionKind? _conversion;
    private readonly TypeSyntax _resultType;

    private ExpressionSyntax? _expressionBody;
    private bool _isChecked;
    private bool _isUnsafe;

    private protected OperatorBuilderBase(
        string name,
        TypeSyntax resultType,
        OperatorKind? kind,
        ConversionKind? conversion)
        : base(name, _ => { })
    {
        _resultType = resultType;
        _kind = kind;
        _conversion = conversion;
    }

    // An operator is always static, so `this` does not exist inside one -- which is what
    // makes a member shadowed by a parameter an error here rather than a qualification.
    private protected override bool IsStaticContext => true;

    private protected override string StatementContext => $"Operator '{Name}'";

    internal DocComment Docs { get; } = new();

    internal List<AttributeListSyntax> Attributes { get; } = [];

    OperatorKind? IOperatorMember.Kind => _kind;

    bool IOperatorMember.IsChecked => _isChecked;

    string IOperatorMember.Display => Name;

    string IOperatorMember.SignatureKey
        => _kind is not null ? $"operator {_kind}/{Parameters.Count}" : $"conversion {_resultType}";

    /// <summary>Documents the operator with an XML <c>&lt;summary&gt;</c>.</summary>
    public TSelf WithSummary(string text) => Self.With(() => Docs.SetSummary(text));

    /// <summary>Documents a parameter.</summary>
    public TSelf WithParameterDoc(string parameterName, string text)
        => Self.With(() => Docs.AddParameter(parameterName, text));

    /// <summary>Documents the result.</summary>
    public TSelf WithReturnsDoc(string text) => Self.With(() => Docs.SetReturns(text));

    /// <summary>Adds an attribute to the operator.</summary>
    public TSelf WithAttribute(string attribute)
        => Self.With(() => Attributes.Add(SyntaxAttributes.AttributeList(attribute)));

    /// <summary>Marks the operator <c>unsafe</c>.</summary>
    /// <remarks>
    /// The consuming project needs <c>AllowUnsafeBlocks</c>; the library cannot know
    /// whether it does, so this is opt-in and unvalidated.
    /// </remarks>
    public TSelf Unsafe() => Self.With(() => _isUnsafe = true);

    /// <summary>
    /// Marks this the <c>checked</c> form of the operator (C# 11):
    /// <c>public static A operator checked +(A l, A r)</c>, for an arithmetic overload
    /// that should throw on overflow inside a <c>checked</c> context.
    /// </summary>
    /// <remarks>
    /// Three language rules apply, all enforced when the type is built rather than left
    /// to the consumer's compiler. A checked form is only allowed on <c>+ - * /</c>,
    /// <c>++</c> and <c>--</c> (CS9023) — not on unary <c>+</c>, remainder, bitwise,
    /// shift or comparison operators. A checked <em>conversion</em> must be
    /// <c>explicit</c> (CS9024). And a checked form requires its unchecked counterpart
    /// to exist alongside it (CS9025).
    /// </remarks>
    public TSelf Checked() => Self.With(() => _isChecked = true);

    /// <summary>
    /// Gives the operator an expression body: <c>=&gt; left.Equals(right);</c>. The
    /// escape hatch for the many operator bodies that need the expression grammar —
    /// <c>!(left == right)</c>, for one.
    /// </summary>
    public TSelf AsExpressionBody(string expression)
        => Self.With(() => _expressionBody = SyntaxParse.Expression(expression));

    /// <summary>
    /// Appends <c>return value;</c> for a value carrying no type — the result of
    /// <c>InvokeRaw</c> or a <c>MemberRaw</c> access. Available on both operator kinds,
    /// since one whose result type is named by text has no type argument for a checked
    /// <c>Return</c> to match against.
    /// </summary>
    /// <param name="value">The value to return.</param>
    /// <returns>This builder, so the chain continues.</returns>
    public TSelf ReturnRaw(IValue value)
    {
        AddReturn(value ?? throw new ArgumentNullException(nameof(value)));
        return Self;
    }

    void IOperatorMember.ValidateChecked(string typeName)
    {
        if (!_isChecked)
            return;

        if (_conversion == ConversionKind.Implicit)
            throw new InvalidOperationException(
                $"Type '{typeName}': an implicit conversion cannot be declared checked. Use an explicit one.");

        if (_kind is null)
            return;

        // Unary + is excluded even though binary + is allowed, which is why arity is
        // part of the test rather than the operator alone.
        var checkable = _kind is OperatorKind.Minus or OperatorKind.Multiply or OperatorKind.Divide
            or OperatorKind.Increment or OperatorKind.Decrement
            || (_kind is OperatorKind.Plus && Parameters.Count == 2);

        if (!checkable)
            throw new InvalidOperationException(
                $"Type '{typeName}': operator '{Operators.SymbolFor(_kind.Value)}' cannot be declared " +
                "checked. Only + - * / ++ -- and explicit conversions have checked forms.");
    }

    MemberDeclarationSyntax IMemberSyntaxBuilder.BuildMember()
    {
        var declaration = BuildOperator();
        return Docs.IsEmpty ? declaration : declaration.WithLeadingTrivia(Docs.Build());
    }

    internal override SyntaxNode BuildSyntax() => ((IMemberSyntaxBuilder)this).BuildMember();

    // An operator and a conversion differ only in how the "name" position is spelled --
    // a token for one, a target type for the other -- so they share everything else.
    private MemberDeclarationSyntax BuildOperator()
    {
        var check = _isChecked ? Token(SyntaxKind.CheckedKeyword) : default;

        BaseMethodDeclarationSyntax declaration = _kind is not null
            ? OperatorDeclaration(_resultType, Token(Operators.TokenFor(_kind.Value)))
                .WithCheckedKeyword(check)
            : ConversionOperatorDeclaration(
                    Token(_conversion == ConversionKind.Implicit
                        ? SyntaxKind.ImplicitKeyword
                        : SyntaxKind.ExplicitKeyword),
                    _resultType)
                .WithCheckedKeyword(check);

        declaration = declaration
            .WithAttributeLists(SyntaxAttributes.Lists(Attributes))
            .WithModifiers(Modifiers())
            .WithParameterList(SyntaxParameters.List(Parameters));

        return (MemberDeclarationSyntax)ApplyBody(declaration);
    }

    private SyntaxTokenList Modifiers()
    {
        var tokens = new List<SyntaxToken>
        {
            Token(SyntaxKind.PublicKeyword),
            Token(SyntaxKind.StaticKeyword),
        };

        if (_isUnsafe)
            tokens.Add(Token(SyntaxKind.UnsafeKeyword));

        return TokenList(tokens);
    }

    private BaseMethodDeclarationSyntax ApplyBody(BaseMethodDeclarationSyntax declaration)
    {
        if (_expressionBody is not null)
        {
            if (Statements.Count > 0)
                throw new InvalidOperationException(
                    $"{StatementContext} has both an expression body and statements.");

            return declaration
                .WithExpressionBody(ArrowExpressionClause(_expressionBody))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
        }

        if (Statements.Count == 0)
            throw new InvalidOperationException(
                $"{StatementContext} has no body. An operator must return a value.");

        return declaration.WithBody(Block(Statements));
    }
}

/// <summary>
/// Builds an operator whose result type is named by text — for an operator returning a
/// type the generator is emitting or only discovered, which is the common case for
/// arithmetic operators on a generated type.
/// </summary>
public sealed class OperatorBuilder : OperatorBuilderBase<OperatorBuilder>
{
    internal OperatorBuilder(OperatorKind kind, TypeSyntax resultType)
        : base($"operator {Operators.SymbolFor(kind)}", resultType, kind, conversion: null)
    {
    }

    internal OperatorBuilder(ConversionKind conversion, TypeSyntax targetType)
        : base($"{conversion.ToString().ToLowerInvariant()} operator {targetType}", targetType, kind: null, conversion)
    {
    }
}

/// <summary>
/// Builds an operator returning <typeparamref name="TReturn"/>, so its
/// <see cref="Return"/> is checked by the compiler. The form comparison operators want,
/// since they all return <c>bool</c>.
/// </summary>
/// <typeparam name="TReturn">The operator's result type.</typeparam>
public sealed class OperatorBuilder<TReturn> : OperatorBuilderBase<OperatorBuilder<TReturn>>
{
    internal OperatorBuilder(OperatorKind kind)
        : base(
            $"operator {Operators.SymbolFor(kind)}",
            TypeNameBuilder.New<TReturn>().BuildTypeSyntax(),
            kind,
            conversion: null)
    {
    }

    internal OperatorBuilder(ConversionKind conversion)
        : base(
            $"{conversion.ToString().ToLowerInvariant()} operator {TypeNameBuilder.New<TReturn>()}",
            TypeNameBuilder.New<TReturn>().BuildTypeSyntax(),
            kind: null,
            conversion)
    {
    }

    /// <summary>Appends <c>return value;</c>, checked against <typeparamref name="TReturn"/>.</summary>
    /// <param name="value">The value to return.</param>
    /// <returns>This builder, so the chain continues.</returns>
    public OperatorBuilder<TReturn> Return(IValue<TReturn> value)
    {
        AddReturn(value ?? throw new ArgumentNullException(nameof(value)));
        return this;
    }

    /// <summary>Appends <c>return literal;</c> for a constant of the result type.</summary>
    /// <param name="literal">The constant.</param>
    /// <returns>This builder, so the chain continues.</returns>
    public OperatorBuilder<TReturn> ReturnLiteral(TReturn literal)
    {
        AddLiteralReturn(literal);
        return this;
    }
}
