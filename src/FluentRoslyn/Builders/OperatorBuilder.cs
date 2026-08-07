using System;
using System.Collections.Generic;
using FluentRoslyn.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace FluentRoslyn.Builders;

/// <summary>
/// Lets the type builder ask an operator which one it is, so it can enforce the pairs
/// C# requires. Null for a conversion, which has no partner.
/// </summary>
internal interface IOperatorMember
{
    OperatorKind? Kind { get; }
}

/// <summary>
/// The shared surface of an operator declaration — parameters, a body, and docs.
/// </summary>
/// <remarks>
/// <para>
/// An operator is a method in every way that matters to a body, so this derives from
/// <see cref="StatementBuilder{TSelf}"/> and inherits the whole statement API. It does
/// <em>not</em> derive from <see cref="MethodBuilderBase{TSelf}"/>, deliberately: that
/// carries <c>Partial</c>, <c>Async</c>, <c>Virtual</c>, <c>Override</c> and the
/// <c>AsCallable</c> families, none of which an operator can be. Inheriting a surface
/// that throws on half its members would be worse than declaring the small one twice.
/// </para>
/// <para>
/// Operators are always <c>public static</c>, so there is no modifier surface at all —
/// C# requires both, and offering to set them could only produce source that does not
/// compile.
/// </para>
/// </remarks>
/// <typeparam name="TSelf">The concrete operator builder type.</typeparam>
public abstract class OperatorBuilderBase<TSelf> : StatementBuilder<TSelf>, IMemberSyntaxBuilder
    where TSelf : OperatorBuilderBase<TSelf>
{
    private protected readonly TypeSyntax ResultType;
    private protected ExpressionSyntax? ExpressionBody;

    private protected OperatorBuilderBase(string name, TypeSyntax resultType) : base(name, _ => { })
    {
        ResultType = resultType;
    }

    // An operator is always static, so `this` does not exist inside one -- which is what
    // makes a member shadowed by a parameter an error here rather than a qualification.
    private protected override bool IsStaticContext => true;

    internal DocComment Docs { get; } = new();

    internal List<AttributeListSyntax> Attributes { get; } = [];

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

    /// <summary>
    /// Gives the operator an expression body: <c>=&gt; left.Equals(right);</c>. The
    /// escape hatch for the many operator bodies that need the expression grammar —
    /// <c>!(left == right)</c>, for one.
    /// </summary>
    public TSelf AsExpressionBody(string expression)
        => Self.With(() => ExpressionBody = SyntaxParse.Expression(expression));

    /// <summary>
    /// Appends <c>return value;</c> for a value carrying no type — the result of
    /// <c>InvokeRaw</c> or a <c>MemberRaw</c> access. Available on both operator kinds,
    /// since an operator whose result type is named by text has no type argument for a
    /// checked <c>Return</c> to match against.
    /// </summary>
    /// <param name="value">The value to return.</param>
    /// <returns>This builder, so the chain continues.</returns>
    public TSelf ReturnRaw(IValue value)
    {
        AddReturn(value ?? throw new ArgumentNullException(nameof(value)));
        return Self;
    }

    private protected BaseMethodDeclarationSyntax ApplyBody(BaseMethodDeclarationSyntax declaration)
    {
        if (ExpressionBody is not null)
        {
            if (Statements.Count > 0)
                throw new InvalidOperationException(
                    $"{StatementContext} has both an expression body and statements.");

            return declaration
                .WithExpressionBody(ArrowExpressionClause(ExpressionBody))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
        }

        if (Statements.Count == 0)
            throw new InvalidOperationException(
                $"{StatementContext} has no body. An operator must return a value.");

        return declaration.WithBody(Block(Statements));
    }

    private protected SyntaxTokenList PublicStatic
        => TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword));

    internal abstract MemberDeclarationSyntax BuildOperator();

    MemberDeclarationSyntax IMemberSyntaxBuilder.BuildMember()
    {
        var declaration = BuildOperator();
        return Docs.IsEmpty ? declaration : declaration.WithLeadingTrivia(Docs.Build());
    }

    internal override SyntaxNode BuildSyntax() => ((IMemberSyntaxBuilder)this).BuildMember();
}

/// <summary>
/// Builds an operator whose result type is named by text — for an operator returning a
/// type the generator is emitting or only discovered, which is the common case for
/// arithmetic operators on a generated type.
/// </summary>
public sealed class OperatorBuilder : OperatorBuilderBase<OperatorBuilder>, IOperatorMember
{
    private readonly OperatorKind? _kind;
    private readonly ConversionKind? _conversion;

    internal OperatorBuilder(OperatorKind kind, TypeSyntax resultType)
        : base($"operator {Operators.SymbolFor(kind)}", resultType)
    {
        _kind = kind;
    }

    internal OperatorBuilder(ConversionKind conversion, TypeSyntax targetType)
        : base($"{conversion.ToString().ToLowerInvariant()} operator {targetType}", targetType)
    {
        _conversion = conversion;
    }

    OperatorKind? IOperatorMember.Kind => _kind;

    private protected override string StatementContext => $"Operator '{Name}'";

    internal override MemberDeclarationSyntax BuildOperator()
        => OperatorSyntax.Build(_kind, _conversion, ResultType, PublicStatic, Attributes, Parameters, ApplyBody);
}

/// <summary>
/// Builds an operator returning <typeparamref name="TReturn"/>, so its
/// <see cref="Return"/> is checked by the compiler. The form comparison operators want,
/// since they all return <c>bool</c>.
/// </summary>
/// <typeparam name="TReturn">The operator's result type.</typeparam>
public sealed class OperatorBuilder<TReturn> : OperatorBuilderBase<OperatorBuilder<TReturn>>, IOperatorMember
{
    private readonly OperatorKind? _kind;
    private readonly ConversionKind? _conversion;

    internal OperatorBuilder(OperatorKind kind)
        : base($"operator {Operators.SymbolFor(kind)}", TypeNameBuilder.New<TReturn>().BuildTypeSyntax())
    {
        _kind = kind;
    }

    internal OperatorBuilder(ConversionKind conversion)
        : base(
            $"{conversion.ToString().ToLowerInvariant()} operator {TypeNameBuilder.New<TReturn>()}",
            TypeNameBuilder.New<TReturn>().BuildTypeSyntax())
    {
        _conversion = conversion;
    }

    OperatorKind? IOperatorMember.Kind => _kind;

    private protected override string StatementContext => $"Operator '{Name}'";

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

    internal override MemberDeclarationSyntax BuildOperator()
        => OperatorSyntax.Build(_kind, _conversion, ResultType, PublicStatic, Attributes, Parameters, ApplyBody);
}

internal static class OperatorSyntax
{
    /// <summary>
    /// Builds the declaration. An operator and a conversion differ only in how the
    /// "name" position is spelled — a token for one, a target type for the other — so
    /// they share everything else rather than duplicating the parameter and body work.
    /// </summary>
    internal static MemberDeclarationSyntax Build(
        OperatorKind? kind,
        ConversionKind? conversion,
        TypeSyntax resultType,
        SyntaxTokenList modifiers,
        List<AttributeListSyntax> attributes,
        IReadOnlyCollection<IParameter> parameters,
        Func<BaseMethodDeclarationSyntax, BaseMethodDeclarationSyntax> applyBody)
    {
        BaseMethodDeclarationSyntax declaration = kind is not null
            ? OperatorDeclaration(resultType, Token(Operators.TokenFor(kind.Value)))
            : ConversionOperatorDeclaration(
                Token(conversion == ConversionKind.Implicit
                    ? SyntaxKind.ImplicitKeyword
                    : SyntaxKind.ExplicitKeyword),
                resultType);

        declaration = declaration
            .WithAttributeLists(SyntaxAttributes.Lists(attributes))
            .WithModifiers(modifiers)
            .WithParameterList(SyntaxParameters.List(parameters));

        return (MemberDeclarationSyntax)applyBody(declaration);
    }
}
