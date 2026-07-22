using System;
using System.Collections.Generic;
using System.Linq;
using Generatr.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Generatr.Builders;

public class ConstructorBuilder : NamedBuilder, IAccessModifier, IMemberSyntaxBuilder
{
    private readonly List<IParameter> _params;
    private readonly List<StatementSyntax> _statements = [];
    private readonly List<AttributeSyntax> _attributes = [];
    private ExpressionSyntax? _expressionBody;
    private ConstructorInitializerSyntax? _initializer;

    internal ConstructorBuilder(TypeBuilder declaringType, AccessModifier accessModifier, IEnumerable<IParameter> @params) : base(declaringType.Name, _ => { })
    {
        DeclaringType = declaringType;
        AccessModifier = accessModifier;
        _params = @params.ToList();
    }

    public TypeBuilder DeclaringType { get; }

    public bool IsStatic { get; set; }

    public AccessModifier AccessModifier { get; set; }

    #region FluentMethods

    /// <summary>
    /// Marks the constructor <c>static</c>. A static constructor takes no parameters,
    /// no access modifier, and no base/this initializer.
    /// </summary>
    public ConstructorBuilder Static() => With(() => IsStatic = true);

    public ConstructorBuilder WithAccessModifier(AccessModifier accessModifier) => With(() => AccessModifier = accessModifier);

    public ConstructorBuilder WithParameter<T>(string name) => With(() => _params.Add(Parameter<T>.New(name)));

    /// <summary>Adds an attribute, e.g. <c>WithAttribute("JsonConstructor")</c>.</summary>
    public ConstructorBuilder WithAttribute(string attribute) => With(() => _attributes.Add(SyntaxAttributes.Attribute(attribute)));

    /// <summary>Chains to a base constructor: <c>: base(arguments)</c>.</summary>
    public ConstructorBuilder CallingBase(params string[] arguments)
        => With(() => _initializer = BuildInitializer(SyntaxKind.BaseConstructorInitializer, arguments));

    /// <summary>Chains to another constructor on this type: <c>: this(arguments)</c>.</summary>
    public ConstructorBuilder CallingThis(params string[] arguments)
        => With(() => _initializer = BuildInitializer(SyntaxKind.ThisConstructorInitializer, arguments));

    /// <summary>Gives the constructor an expression body: <c>C(...) =&gt; expression;</c>.</summary>
    public ConstructorBuilder AsExpressionBody(string expression)
        => With(() => _expressionBody = SyntaxParse.Expression(expression));

    /// <summary>Appends a complete statement to the constructor body.</summary>
    public ConstructorBuilder AddStatement(string statement)
        => With(() => _statements.Add(SyntaxBodies.Statement(statement)));

    /// <summary>Replaces the constructor body with the given statements.</summary>
    public ConstructorBuilder WithBody(params string[] statements)
        => With(() =>
        {
            _statements.Clear();
            foreach (var statement in statements ?? throw new ArgumentNullException(nameof(statements)))
                _statements.Add(SyntaxBodies.Statement(statement));
        });

    #endregion

    internal ConstructorDeclarationSyntax BuildConstructor()
    {
        var ctor = ConstructorDeclaration(Identifier(Name))
            .WithAttributeLists(SyntaxAttributes.Lists(_attributes))
            .WithParameterList(SyntaxParameters.List(_params));

        if (IsStatic)
        {
            if (_params.Count > 0)
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
            if (_statements.Count > 0)
                throw new InvalidOperationException(
                    $"Constructor for '{Name}' cannot have both an expression body and statements.");

            return ctor
                .WithExpressionBody(ArrowExpressionClause(_expressionBody))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
        }

        return ctor.WithBody(Block(_statements));
    }

    MemberDeclarationSyntax IMemberSyntaxBuilder.BuildMember() => BuildConstructor();

    internal override SyntaxNode BuildSyntax() => BuildConstructor();

    private static ConstructorInitializerSyntax BuildInitializer(SyntaxKind kind, string[] arguments)
    {
        if (arguments is null) throw new ArgumentNullException(nameof(arguments));

        return ConstructorInitializer(kind, ArgumentList(SeparatedList(
            arguments.Select(a => Argument(SyntaxParse.Expression(a))))));
    }

    private ConstructorBuilder With(Action action)
    {
        action();
        return this;
    }
}
