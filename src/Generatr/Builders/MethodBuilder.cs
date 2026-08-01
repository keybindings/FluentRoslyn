using System;
using System.Collections.Generic;
using System.Linq;
using Generatr.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Generatr.Builders;

/// <summary>
/// Builds a method declaration. Obtained from <c>DefineMethod</c> on a type builder.
/// </summary>
public class MethodBuilder : NamedBuilder, IAccessModifier, IMemberSyntaxBuilder
{
    private TypeSyntax _returnType;
    private bool _returnsVoid;
    private readonly List<IParameter> _params = [];
    private readonly List<StatementSyntax> _statements = [];
    private readonly List<AttributeSyntax> _attributes = [];
    private readonly GenericParameters _generics = new();
    private readonly DocComment _docs = new();
    private ExpressionSyntax? _expressionBody;

    private MethodBuilder(
        string name,
        AccessModifier accessModifier,
        TypeSyntax returnType,
        bool returnsVoid) : base(name, Identifiers.Validate)
    {
        AccessModifier = accessModifier;
        _returnType = returnType;
        _returnsVoid = returnsVoid;
    }

    /// <summary>Whether the method is <c>static</c>.</summary>
    public bool IsStatic { get; set; }

    /// <summary>Whether the method is <c>partial</c>.</summary>
    public bool IsPartial { get; set; }

    /// <summary>
    /// Whether the method is <c>async</c>. The return type must be awaitable —
    /// <c>void</c>, <c>Task</c>, <c>Task&lt;T&gt;</c>, <c>ValueTask</c>, and so on.
    /// </summary>
    public bool IsAsync { get; set; }

    /// <summary>The inheritance modifier — virtual, abstract, override, or sealed override.</summary>
    public Inheritance Inheritance { get; set; }

    internal bool IsAbstract => Inheritance == Inheritance.Abstract;

    /// <summary>The method's accessibility.</summary>
    public AccessModifier AccessModifier { get; set; }

    /// <summary>A void method: <c>void Name(...) { }</c>. Add parameters with <see cref="WithParameter{T}"/>.</summary>
    internal static MethodBuilder Action(string name, AccessModifier accessModifier)
        => new(name, accessModifier, PredefinedType(Token(SyntaxKind.VoidKeyword)), returnsVoid: true);

    /// <summary>A method returning <paramref name="returnType"/>; requires a body.</summary>
    internal static MethodBuilder Returning(string name, AccessModifier accessModifier, TypeNameBuilder returnType)
        => new(name, accessModifier, returnType.BuildTypeSyntax(), returnsVoid: false);

    #region FluentMethods

    /// <summary>Marks the method <c>static</c>.</summary>
    public MethodBuilder Static() => this.With(() => IsStatic = true);

    /// <summary>Marks the method <c>partial</c> (e.g. a source generator implementing a partial method).</summary>
    public MethodBuilder Partial() => this.With(() => IsPartial = true);

    /// <summary>
    /// Marks the method <c>async</c>. Pair it with an awaitable return type, e.g.
    /// <c>DefineMethod&lt;Task&gt;("SaveAsync").Async()</c>.
    /// </summary>
    public MethodBuilder Async() => this.With(() => IsAsync = true);

    /// <summary>Marks the method <c>virtual</c>.</summary>
    public MethodBuilder Virtual() => this.With(() => Inheritance = Inheritance.Virtual);

    /// <summary>
    /// Marks the method <c>abstract</c>: it emits no body, and the declaring type must
    /// itself be abstract.
    /// </summary>
    public MethodBuilder Abstract() => this.With(() => Inheritance = Inheritance.Abstract);

    /// <summary>Marks the method <c>override</c>.</summary>
    public MethodBuilder Override() => this.With(() => Inheritance = Inheritance.Override);

    /// <summary>Marks the method <c>sealed override</c>.</summary>
    public MethodBuilder SealedOverride() => this.With(() => Inheritance = Inheritance.SealedOverride);

    /// <summary>Sets the method's accessibility.</summary>
    public MethodBuilder WithAccessModifier(AccessModifier accessModifier) => this.With(() => AccessModifier = accessModifier);

    /// <summary>Appends a parameter of type <typeparamref name="T"/>.</summary>
    public MethodBuilder WithParameter<T>(string name) => this.With(() => _params.Add(Parameter<T>.New(name)));

    /// <summary>
    /// Documents the method with an XML <c>&lt;summary&gt;</c>. Newlines become separate
    /// comment lines, and XML markup characters are escaped.
    /// </summary>
    public MethodBuilder WithSummary(string text) => this.With(() => _docs.SetSummary(text));

    /// <summary>
    /// Documents a parameter: <c>&lt;param name="..."&gt;</c>. The name must match a
    /// parameter added with <see cref="WithParameter{T}"/>.
    /// </summary>
    public MethodBuilder WithParameterDoc(string parameterName, string text)
        => this.With(() => _docs.AddParameter(parameterName, text));

    /// <summary>Documents the return value: <c>&lt;returns&gt;</c>.</summary>
    public MethodBuilder WithReturnsDoc(string text) => this.With(() => _docs.SetReturns(text));

    /// <summary>Adds a generic type parameter, e.g. <c>WithTypeParameter("T")</c> for <c>Name&lt;T&gt;</c>.</summary>
    public MethodBuilder WithTypeParameter(string name) => this.With(() => _generics.AddTypeParameter(name));

    /// <summary>
    /// Sets the return type from a raw type name, e.g. <c>Returns("T")</c> or
    /// <c>Returns("List&lt;T&gt;")</c> — for returning a generic type parameter that is
    /// not a CLR type. Requires a body.
    /// </summary>
    public MethodBuilder Returns(string typeName) => this.With(() =>
    {
        _returnType = SyntaxParse.TypeName(typeName);
        _returnsVoid = false;
    });

    /// <summary>
    /// Constrains a type parameter, e.g. <c>WithConstraint("T", "class")</c>,
    /// <c>WithConstraint("T", "IComparable&lt;T&gt;")</c>, or <c>WithConstraint("T", "new()")</c>.
    /// Call once per constraint; C# order is class/struct first, new() last.
    /// </summary>
    public MethodBuilder WithConstraint(string typeParameter, string constraint)
        => this.With(() => _generics.AddConstraint(typeParameter, constraint));

    /// <summary>Adds an attribute, e.g. <c>WithAttribute("Obsolete")</c>.</summary>
    public MethodBuilder WithAttribute(string attribute) => this.With(() => _attributes.Add(SyntaxAttributes.Attribute(attribute)));

    /// <summary>
    /// Gives the method an expression body: <c>Name(...) =&gt; expression;</c>. Valid for
    /// both void and value-returning methods.
    /// </summary>
    public MethodBuilder AsExpressionBody(string expression)
        => this.With(() => _expressionBody = SyntaxParse.Expression(expression));

    /// <summary>
    /// Appends a complete statement to the method body, e.g. <c>"return a + b;"</c>.
    /// A value-returning method's body must return on all paths.
    /// </summary>
    public MethodBuilder AddStatement(string statement)
        => this.With(() => _statements.Add(SyntaxBodies.Statement(statement)));

    /// <summary>Replaces the method body with the given statements.</summary>
    public MethodBuilder WithBody(params string[] statements)
        => this.With(() =>
        {
            _statements.Clear();
            foreach (var statement in statements ?? throw new ArgumentNullException(nameof(statements)))
                _statements.Add(SyntaxBodies.Statement(statement));
        });

    #endregion

    internal MethodDeclarationSyntax BuildMethod()
    {
        var method = BuildMethodCore();
        return _docs.IsEmpty ? method : method.WithLeadingTrivia(_docs.Build());
    }

    private MethodDeclarationSyntax BuildMethodCore()
    {
        ValidateInheritance();
        ValidateAsync();

        var method = MethodDeclaration(_returnType, Identifier(Name))
            .WithAttributeLists(SyntaxAttributes.Lists(_attributes))
            .WithModifiers(SyntaxFormatting.Modifiers(
                AccessModifier, IsStatic, isPartial: IsPartial, inheritance: Inheritance, isAsync: IsAsync))
            .WithParameterList(SyntaxParameters.List(_params));

        method = _generics.ApplyTo(method, $"Method '{Name}'");

        // An abstract method declares no body at all — just a semicolon.
        if (IsAbstract)
            return method.WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

        if (_expressionBody is not null)
        {
            if (_statements.Count > 0)
                throw new InvalidOperationException(
                    $"Method '{Name}' cannot have both an expression body and statements.");

            return method
                .WithExpressionBody(ArrowExpressionClause(_expressionBody))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
        }

        // A statement block covers both void and value-returning methods; the caller is
        // responsible for returning on all paths when non-void.
        if (_statements.Count > 0)
            return method.WithBody(Block(_statements));

        // A non-void method with no body would emit `int Foo() { }`, which does not
        // compile: it needs either an expression body or statements.
        if (!_returnsVoid)
            throw new InvalidOperationException(
                $"Method '{Name}' returns non-void and needs a body. Use AsExpressionBody or AddStatement/WithBody.");

        return method.WithBody(Block());
    }

    // The inheritance modifiers are mutually exclusive by construction, so what remains
    // is their interaction with static, private, and the method body.
    private void ValidateInheritance()
    {
        if (Inheritance == Inheritance.None)
            return;

        if (IsStatic)
            throw new InvalidOperationException(
                $"Method '{Name}' cannot be both static and {Describe(Inheritance)}.");

        if (AccessModifier == AccessModifier.Private)
            throw new InvalidOperationException(
                $"Method '{Name}' cannot be private and {Describe(Inheritance)}.");

        if (IsPartial)
            throw new InvalidOperationException(
                $"Method '{Name}' cannot be both partial and {Describe(Inheritance)}.");

        if (IsAbstract && (_expressionBody is not null || _statements.Count > 0))
            throw new InvalidOperationException($"Abstract method '{Name}' cannot have a body.");
    }

    private void ValidateAsync()
    {
        if (!IsAsync)
            return;

        if (IsAbstract)
            throw new InvalidOperationException($"Method '{Name}' cannot be both abstract and async.");

        // Only the clearly-wrong cases are rejected: a built-in type other than void can
        // never be awaitable. Named types pass, so Task, ValueTask, IAsyncEnumerable and
        // any custom awaitable are all accepted without an allowlist to fall behind.
        if (_returnType is PredefinedTypeSyntax predefined
            && !predefined.Keyword.IsKind(SyntaxKind.VoidKeyword))
        {
            throw new InvalidOperationException(
                $"Async method '{Name}' cannot return '{predefined}'. Use void, Task, Task<T>, or another awaitable type.");
        }
    }

    private static string Describe(Inheritance inheritance)
        => inheritance == Inheritance.SealedOverride ? "sealed override" : inheritance.ToString().ToLowerInvariant();

    MemberDeclarationSyntax IMemberSyntaxBuilder.BuildMember() => BuildMethod();

    internal override SyntaxNode BuildSyntax() => BuildMethod();
}
