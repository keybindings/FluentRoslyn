using System;
using System.Collections.Generic;
using System.Linq;
using FluentRoslyn.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace FluentRoslyn.Builders;

/// <summary>
/// Lets an <see cref="OperatorSet"/> interrogate an operator so it can enforce the
/// rules C# imposes across a whole type: the <c>==</c>/<c>!=</c> pairings, the
/// checked/unchecked twinning, and the no-duplicate-signature rules.
/// </summary>
internal interface IOperatorMember : IMemberSyntaxBuilder
{
    /// <summary>Which operator this is, or null for a conversion.</summary>
    OperatorKind? Kind { get; }

    /// <summary>Which conversion this is, or null for an operator.</summary>
    ConversionKind? Conversion { get; }

    /// <summary>Whether this is the <c>checked</c> form.</summary>
    bool IsChecked { get; }

    /// <summary>Whether the partner and twin requirements are waived for this member.</summary>
    bool PartnerElsewhere { get; }

    /// <summary>How this operator names itself in an error message.</summary>
    string Display { get; }

    /// <summary>
    /// The canonical parameter type list, e.g. <c>(MyApp.OrderId, int)</c>. Two
    /// operators are the same overload exactly when their kind and this agree, so it is
    /// the unit the pairing, twinning, and duplicate rules all compare — C# matches
    /// partners by signature, not by symbol (CS0216 fires on <c>==(A, A)</c> paired
    /// with <c>!=(A, int)</c>).
    /// </summary>
    string ParameterSignature { get; }

    /// <summary>The canonical result or conversion-target type text.</summary>
    string ResultTypeText { get; }

    /// <summary>
    /// Rejects a declaration that is wrong on its own, before any cross-member rule:
    /// bad arity, an ineligible <c>checked</c> form, a non-<c>bool</c>
    /// <c>operator true</c>. Runs from the member's own build path, so
    /// <c>ToString()</c> on a lone builder validates the same way a whole type does.
    /// </summary>
    void ValidateMember(string context);
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
public abstract class OperatorBuilderBase<TSelf> : StatementBuilder<TSelf>, IOperatorMember
    where TSelf : OperatorBuilderBase<TSelf>
{
    private readonly OperatorKind? _kind;
    private readonly ConversionKind? _conversion;
    private readonly TypeSyntax _resultType;

    private ExpressionSyntax? _expressionBody;
    private bool _isChecked;
    private bool _isUnsafe;
    private bool _partnerElsewhere;

    private protected OperatorBuilderBase(
        string name,
        TypeSyntax resultType,
        OperatorKind? kind,
        ConversionKind? conversion)
        : base(name, _ => { })
    {
        _resultType = resultType;
        // An undefined kind is rejected here, with the kind named, rather than
        // surfacing later as a KeyNotFoundException out of a private dictionary.
        _kind = kind is null ? null : Operators.Defined(kind.Value);
        _conversion = conversion is null ? null : Operators.Defined(conversion.Value);
    }

    // An operator is always static, so `this` does not exist inside one -- which is what
    // makes an instance-member reference an error here rather than a qualification.
    private protected override bool IsStaticContext => true;

    private protected override string StatementContext => $"Operator '{Name}'";

    internal DocComment Docs { get; } = new();

    internal List<AttributeListSyntax> Attributes { get; } = [];

    OperatorKind? IOperatorMember.Kind => _kind;

    ConversionKind? IOperatorMember.Conversion => _conversion;

    bool IOperatorMember.IsChecked => _isChecked;

    bool IOperatorMember.PartnerElsewhere => _partnerElsewhere;

    string IOperatorMember.Display => Name;

    string IOperatorMember.ParameterSignature => ParameterSignature;

    string IOperatorMember.ResultTypeText => Operators.CanonicalTypeText(_resultType);

    private string ParameterSignature
        => $"({string.Join(", ", Parameters.Select(p => Operators.CanonicalTypeText(p.TypeName.BuildTypeSyntax())))})";

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
    /// Three language rules are enforced rather than left to the consumer's compiler. A
    /// checked form is only allowed on <c>+ - * /</c>, <c>++</c> and <c>--</c> (CS9023)
    /// — not on unary <c>+</c>, remainder, bitwise, shift or comparison operators. A
    /// checked <em>conversion</em> must be <c>explicit</c> (CS9024). And a checked form
    /// requires an unchecked counterpart <em>with the same signature</em> alongside it
    /// (CS9025) — see <see cref="PartnerDeclaredElsewhere"/> when that counterpart
    /// lives in another part of a partial type.
    /// </remarks>
    public TSelf Checked() => Self.With(() => _isChecked = true);

    /// <summary>
    /// Waives, for this operator, the requirement that its partner (<c>!=</c> for
    /// <c>==</c>, and so on) or its unchecked twin be declared through this same
    /// builder.
    /// </summary>
    /// <remarks>
    /// C# imposes those rules per <em>type</em>, and a partial type may legally split a
    /// pair across its parts — one half generated, one half written by hand. The
    /// builder can only see its own part, so without this it would refuse a legal
    /// split. The consumer's compiler still enforces the real rule; this only moves the
    /// check there for the declarations that genuinely span parts.
    /// </remarks>
    public TSelf PartnerDeclaredElsewhere() => Self.With(() => _partnerElsewhere = true);

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

    void IOperatorMember.ValidateMember(string context) => ValidateMember(context);

    MemberDeclarationSyntax IMemberSyntaxBuilder.BuildMember()
    {
        var declaration = BuildOperator();
        return Docs.IsEmpty ? declaration : declaration.WithLeadingTrivia(Docs.Build());
    }

    internal override SyntaxNode BuildSyntax() => ((IMemberSyntaxBuilder)this).BuildMember();

    // Everything wrong with a declaration in isolation, checked on the member's own
    // build path so ToString() on a lone builder validates like every sibling builder.
    // The cross-member rules (pairing, twinning, duplicates) need the whole type and
    // live in OperatorSet.
    private void ValidateMember(string context)
    {
        if (_conversion is not null && Parameters.Count != 1)
            throw new InvalidOperationException(
                $"{context}: a conversion takes exactly one parameter, the value being converted; " +
                $"this one has {Parameters.Count}.");

        if (_kind is not null && !Operators.ArityIsLegal(_kind.Value, Parameters.Count))
            throw new InvalidOperationException(
                $"{context}: operator '{Operators.SymbolFor(_kind.Value)}' takes " +
                $"{Operators.ArityDescription(_kind.Value)}; this one has {Parameters.Count}.");

        // CS0215: operator true/false must return bool. The result type is this
        // library's own emission, so the check is by canonical text.
        if (_kind is OperatorKind.True or OperatorKind.False &&
            Operators.CanonicalTypeText(_resultType) != "bool")
            throw new InvalidOperationException(
                $"{context}: operator '{Operators.SymbolFor(_kind.Value)}' must return bool, " +
                $"not '{Operators.CanonicalTypeText(_resultType)}'.");

        ValidateChecked(context);
    }

    private void ValidateChecked(string context)
    {
        if (!_isChecked)
            return;

        if (_conversion == ConversionKind.Implicit)
            throw new InvalidOperationException(
                $"{context}: an implicit conversion cannot be declared checked. Use an explicit one.");

        if (_kind is null)
            return;

        // Unary + is excluded even though binary + is allowed, which is why arity is
        // part of the test rather than the operator alone.
        var checkable = _kind is OperatorKind.Minus or OperatorKind.Multiply or OperatorKind.Divide
            or OperatorKind.Increment or OperatorKind.Decrement
            || (_kind is OperatorKind.Plus && Parameters.Count == 2);

        if (!checkable)
            throw new InvalidOperationException(
                $"{context}: operator '{Operators.SymbolFor(_kind.Value)}' cannot be declared " +
                "checked. Only + - * / ++ -- and explicit conversions have checked forms.");
    }

    // An operator and a conversion differ only in how the "name" position is spelled --
    // a token for one, a target type for the other -- so they share everything else.
    private MemberDeclarationSyntax BuildOperator()
    {
        ValidateMember(StatementContext);

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
            .WithModifiers(SyntaxFormatting.Modifiers(AccessModifier.Public, isStatic: true, isUnsafe: _isUnsafe))
            .WithParameterList(SyntaxParameters.List(Parameters));

        return (MemberDeclarationSyntax)ApplyBody(declaration);
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
        : base($"operator {Operators.SymbolFor(Operators.Defined(kind))}", resultType, kind, conversion: null)
    {
    }

    internal OperatorBuilder(ConversionKind conversion, TypeSyntax targetType)
        : base(
            $"{Operators.Defined(conversion).ToString().ToLowerInvariant()} operator {targetType}",
            targetType,
            kind: null,
            conversion)
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
        : this(kind, TypeNameBuilder.New<TReturn>())
    {
    }

    internal OperatorBuilder(ConversionKind conversion)
        : this(conversion, TypeNameBuilder.New<TReturn>())
    {
    }

    // The chained constructors exist so the TypeNameBuilder is built once and feeds
    // both the display name and the result type, rather than being derived twice.
    private OperatorBuilder(OperatorKind kind, TypeNameBuilder result)
        : base($"operator {Operators.SymbolFor(Operators.Defined(kind))}", result.BuildTypeSyntax(), kind, conversion: null)
    {
    }

    private OperatorBuilder(ConversionKind conversion, TypeNameBuilder target)
        : base(
            $"{Operators.Defined(conversion).ToString().ToLowerInvariant()} operator {target}",
            target.BuildTypeSyntax(),
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
