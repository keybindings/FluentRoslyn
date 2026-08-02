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
/// builder; its name always matches the declaring type.
/// </summary>
public class ConstructorBuilder : NamedBuilder, IAccessModifier, IMemberSyntaxBuilder
{
    private readonly List<IParameter> _params = [];
    private readonly List<StatementSyntax> _statements = [];
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

    #region FluentMethods

    /// <summary>
    /// Marks the constructor <c>static</c>. A static constructor takes no parameters,
    /// no access modifier, and no base/this initializer.
    /// </summary>
    public ConstructorBuilder Static() => this.With(() => IsStatic = true);

    /// <summary>Sets the constructor's accessibility.</summary>
    public ConstructorBuilder WithAccessModifier(AccessModifier accessModifier) => this.With(() => AccessModifier = accessModifier);

    /// <summary>Appends a parameter of type <typeparamref name="T"/>.</summary>
    public ConstructorBuilder WithParameter<T>(string name) => this.With(() => _params.Add(Parameter<T>.New(name)));

    /// <summary>
    /// Appends a parameter of type <typeparamref name="T"/> and hands back a typed
    /// reference to it, for use with <see cref="Assign{TValue}"/>. Returns the builder,
    /// so the fluent chain is unbroken:
    /// <c>.WithParameter&lt;int&gt;("id", out var id)</c>.
    /// </summary>
    public ConstructorBuilder WithParameter<T>(string name, out IReference<T> reference)
    {
        var parameter = Parameter<T>.New(name);
        _params.Add(parameter);
        reference = new ParameterReference<T>(parameter.Name);
        return this;
    }

    /// <summary>
    /// Appends an assignment statement, e.g. <c>Name = name;</c>. Both sides are
    /// references of the same type, so assigning the wrong one is a compile error in the
    /// generator rather than broken generated source.
    /// </summary>
    public ConstructorBuilder Assign<TValue>(IReference<TValue> target, IReference<TValue> value)
        => this.With(() => _statements.Add(
            SyntaxReferences.Assignment(target, value, _params, IsStatic, $"Constructor for '{Name}'")));

    /// <summary>Appends a call statement: <c>target.Method();</c>.</summary>
    public ConstructorBuilder Call<TTarget>(IReference<TTarget> target, IMethod method)
        => AddCall(target, method);

    /// <summary>
    /// Appends a call statement: <c>target.Method(argument1);</c>. The argument
    /// reference's type must match the handle's — a mismatch is a compile error in the
    /// generator rather than broken generated source.
    /// </summary>
    public ConstructorBuilder Call<TTarget, T1>(IReference<TTarget> target, IMethod<T1> method, IReference<T1> argument1)
        => AddCall(target, method, argument1);

    /// <summary>Appends a two-argument call statement.</summary>
    public ConstructorBuilder Call<TTarget, T1, T2>(
        IReference<TTarget> target, IMethod<T1, T2> method, IReference<T1> argument1, IReference<T2> argument2)
        => AddCall(target, method, argument1, argument2);

    /// <summary>Appends a three-argument call statement.</summary>
    public ConstructorBuilder Call<TTarget, T1, T2, T3>(
        IReference<TTarget> target, IMethod<T1, T2, T3> method,
        IReference<T1> argument1, IReference<T2> argument2, IReference<T3> argument3)
        => AddCall(target, method, argument1, argument2, argument3);

    private ConstructorBuilder AddCall(IReference target, object method, params IReference[] arguments)
        => this.With(() => _statements.Add(
            SyntaxReferences.Invocation(target, method, arguments, _params, IsStatic, $"Constructor for '{Name}'")));

    /// <summary>
    /// Appends a parameter whose type is being generated alongside — the type's builder
    /// is the reference, so the name is spelled once. For a typed handle to the
    /// parameter, give the generated type an <c>[EmitsAs]</c> placeholder and use
    /// <see cref="WithParameter{T}(string, out IReference{T})"/> instead.
    /// </summary>
    public ConstructorBuilder WithParameter(TypeDeclarationBuilder type, string name)
        => this.With(() => _params.Add(Parameter.Of(type, name)));

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

    /// <summary>Appends a complete statement to the constructor body.</summary>
    public ConstructorBuilder AddStatement(string statement)
        => this.With(() => _statements.Add(SyntaxBodies.Statement(statement)));

    /// <summary>Replaces the constructor body with the given statements.</summary>
    public ConstructorBuilder WithBody(params string[] statements)
        => this.With(() =>
        {
            _statements.Clear();
            foreach (var statement in statements ?? throw new ArgumentNullException(nameof(statements)))
                _statements.Add(SyntaxBodies.Statement(statement));
        });

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
}
