using System;
using System.Collections.Generic;
using System.Linq;
using Generatr.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Generatr.Builders;

public class MethodBuilder : NamedBuilder, IAccessModifier, IMemberSyntaxBuilder
{
    private TypeSyntax _returnType;
    private bool _returnsVoid;
    private readonly List<IParameter> _params;
    private readonly List<StatementSyntax> _statements = [];
    private readonly List<AttributeSyntax> _attributes = [];
    private readonly List<string> _typeParameters = [];
    private readonly Dictionary<string, List<string>> _constraints = [];
    private ExpressionSyntax? _expressionBody;

    private MethodBuilder(
        string name,
        AccessModifier accessModifier,
        TypeSyntax returnType,
        bool returnsVoid,
        IEnumerable<IParameter> @params) : base(name, Identifiers.Validate)
    {
        AccessModifier = accessModifier;
        _returnType = returnType;
        _returnsVoid = returnsVoid;
        _params = @params.ToList();
    }

    public bool IsStatic { get; set; }

    public AccessModifier AccessModifier { get; set; }

    /// <summary>A void method: <c>void Name(...) { }</c>.</summary>
    internal static MethodBuilder Action(string name, AccessModifier accessModifier, IEnumerable<IParameter> @params)
        => new(name, accessModifier, PredefinedType(Token(SyntaxKind.VoidKeyword)), returnsVoid: true, @params);

    /// <summary>A method returning <paramref name="returnType"/>; requires a body.</summary>
    internal static MethodBuilder Returning(string name, AccessModifier accessModifier, TypeNameBuilder returnType, IEnumerable<IParameter> @params)
        => new(name, accessModifier, returnType.BuildTypeSyntax(), returnsVoid: false, @params);

    #region FluentMethods

    public MethodBuilder Static() => With(() => IsStatic = true);

    public MethodBuilder WithAccessModifier(AccessModifier accessModifier) => With(() => AccessModifier = accessModifier);

    public MethodBuilder WithParameter<T>(string name) => With(() => _params.Add(Parameter<T>.New(name)));

    /// <summary>Adds a generic type parameter, e.g. <c>WithTypeParameter("T")</c> for <c>Name&lt;T&gt;</c>.</summary>
    public MethodBuilder WithTypeParameter(string name) => With(() => _typeParameters.Add(name ?? throw new ArgumentNullException(nameof(name))));

    /// <summary>
    /// Sets the return type from a raw type name, e.g. <c>Returns("T")</c> or
    /// <c>Returns("List&lt;T&gt;")</c> — for returning a generic type parameter that is
    /// not a CLR type. Requires a body.
    /// </summary>
    public MethodBuilder Returns(string typeName) => With(() =>
    {
        _returnType = SyntaxParse.TypeName(typeName);
        _returnsVoid = false;
    });

    /// <summary>
    /// Constrains a type parameter, e.g. <c>WithConstraint("T", "class")</c>,
    /// <c>WithConstraint("T", "IComparable&lt;T&gt;")</c>, or <c>WithConstraint("T", "new()")</c>.
    /// Call once per constraint; C# order is class/struct first, new() last.
    /// </summary>
    public MethodBuilder WithConstraint(string typeParameter, string constraint) => With(() =>
    {
        if (constraint is null) throw new ArgumentNullException(nameof(constraint));
        if (!_constraints.TryGetValue(typeParameter, out var list))
            _constraints[typeParameter] = list = [];
        list.Add(constraint);
    });

    /// <summary>Adds an attribute, e.g. <c>WithAttribute("Obsolete")</c>.</summary>
    public MethodBuilder WithAttribute(string attribute) => With(() => _attributes.Add(SyntaxAttributes.Attribute(attribute)));

    /// <summary>
    /// Gives the method an expression body: <c>Name(...) =&gt; expression;</c>. Valid for
    /// both void and value-returning methods.
    /// </summary>
    public MethodBuilder AsExpressionBody(string expression)
        => With(() => _expressionBody = SyntaxParse.Expression(expression));

    /// <summary>
    /// Appends a complete statement to the method body, e.g. <c>"return a + b;"</c>.
    /// A value-returning method's body must return on all paths.
    /// </summary>
    public MethodBuilder AddStatement(string statement)
        => With(() => _statements.Add(SyntaxBodies.Statement(statement)));

    /// <summary>Replaces the method body with the given statements.</summary>
    public MethodBuilder WithBody(params string[] statements)
        => With(() =>
        {
            _statements.Clear();
            foreach (var statement in statements ?? throw new ArgumentNullException(nameof(statements)))
                _statements.Add(SyntaxBodies.Statement(statement));
        });

    #endregion

    internal MethodDeclarationSyntax BuildMethod()
    {
        var method = MethodDeclaration(_returnType, Identifier(Name))
            .WithAttributeLists(SyntaxAttributes.Lists(_attributes))
            .WithModifiers(SyntaxFormatting.Modifiers(AccessModifier, IsStatic))
            .WithParameterList(SyntaxParameters.List(_params));

        method = ApplyGenerics(method);

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

    MemberDeclarationSyntax IMemberSyntaxBuilder.BuildMember() => BuildMethod();

    internal override SyntaxNode BuildSyntax() => BuildMethod();

    private MethodDeclarationSyntax ApplyGenerics(MethodDeclarationSyntax method)
    {
        SyntaxGenerics.Validate($"Method '{Name}'", _typeParameters, _constraints);

        if (SyntaxGenerics.TypeParameterList(_typeParameters) is { } list)
            method = method.WithTypeParameterList(list);

        var clauses = SyntaxGenerics.ConstraintClauses(_typeParameters, _constraints);
        return clauses.Count == 0 ? method : method.WithConstraintClauses(clauses);
    }

    private MethodBuilder With(Action action)
    {
        action();
        return this;
    }
}
