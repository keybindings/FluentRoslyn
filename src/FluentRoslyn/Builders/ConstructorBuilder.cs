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
/// Builds a constructor declaration. Obtained from <c>DefineConstructor</c> on a type
/// builder; its name always matches the declaring type. Parameters and statements come
/// from <see cref="StatementBuilder{TSelf}"/>.
/// </summary>
public class ConstructorBuilder : StatementBuilder<ConstructorBuilder>, IAccessModifier, IMemberSyntaxBuilder
{
    private readonly List<AttributeListSyntax> _attributes = [];
    private readonly DocComment _docs = new();
    private ExpressionSyntax? _expressionBody;
    private ConstructorInitializerSyntax? _initializer;

    internal ConstructorBuilder(TypeBuilder declaringType, AccessModifier accessModifier) : base(declaringType.Name, _ => { })
    {
        AccessModifier = accessModifier;
    }

    /// <summary>Whether this is a static constructor.</summary>
    public bool IsStatic { get; set; }

    /// <summary>The constructor's accessibility. Ignored for a static constructor.</summary>
    public AccessModifier AccessModifier { get; set; }

    private protected override string StatementContext => $"Constructor for '{Name}'";

    private protected override bool IsStaticContext => IsStatic;

    #region FluentMethods

    /// <summary>
    /// Marks the constructor <c>static</c>. A static constructor takes no parameters,
    /// no access modifier, and no base/this initializer.
    /// </summary>
    public ConstructorBuilder Static() => this.With(() => IsStatic = true);

    /// <summary>Sets the constructor's accessibility.</summary>
    public ConstructorBuilder WithAccessModifier(AccessModifier accessModifier) => this.With(() => AccessModifier = accessModifier);

    /// <summary>Documents the constructor with an XML <c>&lt;summary&gt;</c>.</summary>
    public ConstructorBuilder WithSummary(string text) => this.With(() => _docs.SetSummary(text));

    /// <summary>Documents a parameter: <c>&lt;param name="..."&gt;</c>.</summary>
    public ConstructorBuilder WithParameterDoc(string parameterName, string text)
        => this.With(() => _docs.AddParameter(parameterName, text));

    /// <summary>Adds an attribute, e.g. <c>WithAttribute("JsonConstructor")</c>.</summary>
    public ConstructorBuilder WithAttribute(string attribute) => this.With(() => _attributes.Add(SyntaxAttributes.AttributeList(attribute)));

    /// <summary>Chains to a base constructor: <c>: base(arguments)</c>.</summary>
    public ConstructorBuilder CallingBase(params string[] arguments)
        => this.With(() => _initializer = BuildInitializer(SyntaxKind.BaseConstructorInitializer, arguments));

    /// <summary>Chains to another constructor on this type: <c>: this(arguments)</c>.</summary>
    public ConstructorBuilder CallingThis(params string[] arguments)
        => this.With(() => _initializer = BuildInitializer(SyntaxKind.ThisConstructorInitializer, arguments));

    /// <summary>Gives the constructor an expression body: <c>C(...) =&gt; expression;</c>.</summary>
    public ConstructorBuilder AsExpressionBody(string expression)
        => this.With(() => _expressionBody = SyntaxParse.Expression(expression));

    #endregion

    internal ConstructorDeclarationSyntax BuildConstructor()
    {
        var ctor = BuildConstructorCore();
        return _docs.IsEmpty ? ctor : ctor.WithLeadingTrivia(_docs.Build());
    }

    private ConstructorDeclarationSyntax BuildConstructorCore()
    {
        var ctor = ConstructorDeclaration(Identifier(Name))
            .WithAttributeLists(SyntaxAttributes.Lists(_attributes))
            .WithParameterList(SyntaxParameters.List(Parameters));

        if (IsStatic)
        {
            if (Parameters.Count > 0)
                throw new InvalidOperationException($"Static constructor for '{Name}' cannot have parameters.");
            if (_initializer is not null)
                throw new InvalidOperationException($"Static constructor for '{Name}' cannot chain to base or this.");

            ctor = ctor.WithModifiers(TokenList(Token(SyntaxKind.StaticKeyword)));
        }
        else
        {
            ctor = ctor.WithModifiers(SyntaxFormatting.Modifiers(AccessModifier));
            if (_initializer is not null)
                ctor = ctor.WithInitializer(_initializer);
        }

        if (_expressionBody is not null)
        {
            if (Statements.Count > 0)
                throw new InvalidOperationException(
                    $"Constructor for '{Name}' cannot have both an expression body and statements.");

            return ctor
                .WithExpressionBody(ArrowExpressionClause(_expressionBody))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
        }

        return ctor.WithBody(Block(Statements));
    }

    MemberDeclarationSyntax IMemberSyntaxBuilder.BuildMember() => BuildConstructor();

    internal override SyntaxNode BuildSyntax() => BuildConstructor();

    private static ConstructorInitializerSyntax BuildInitializer(SyntaxKind kind, string[] arguments)
    {
        if (arguments is null) throw new ArgumentNullException(nameof(arguments));

        return ConstructorInitializer(kind, ArgumentList(SeparatedList(
            arguments.Select(a => Argument(SyntaxParse.Expression(a))))));
    }
}
