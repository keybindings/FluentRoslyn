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
    private readonly ClassBuilder _classContext;
    private readonly TypeSyntax _returnType;
    private readonly bool _returnsVoid;
    private readonly List<IParameter> _params;
    private ExpressionSyntax? _expressionBody;

    private MethodBuilder(
        ClassBuilder @class,
        string name,
        AccessModifier accessModifier,
        TypeSyntax returnType,
        bool returnsVoid,
        IEnumerable<IParameter> @params) : base(name, _ => { })
    {
        _classContext = @class;
        AccessModifier = accessModifier;
        _returnType = returnType;
        _returnsVoid = returnsVoid;
        _params = @params.ToList();
    }

    public bool IsStatic { get; set; }

    public AccessModifier AccessModifier { get; set; }

    /// <summary>A void method: <c>void Name(...) { }</c>.</summary>
    internal static MethodBuilder Action(ClassBuilder classContext, string name, AccessModifier accessModifier, IEnumerable<IParameter> @params)
        => new(classContext, name, accessModifier, PredefinedType(Token(SyntaxKind.VoidKeyword)), returnsVoid: true, @params);

    /// <summary>A method returning <paramref name="returnType"/>; requires a body.</summary>
    internal static MethodBuilder Returning(ClassBuilder classContext, string name, AccessModifier accessModifier, TypeNameBuilder returnType, IEnumerable<IParameter> @params)
        => new(classContext, name, accessModifier, returnType.BuildTypeSyntax(), returnsVoid: false, @params);

    #region FluentMethods

    public MethodBuilder Static() => With(() => IsStatic = true);

    public MethodBuilder WithAccessModifier(AccessModifier accessModifier) => With(() => AccessModifier = accessModifier);

    public MethodBuilder WithParameter<T>(string name) => With(() => _params.Add(Parameter<T>.New(name)));

    /// <summary>
    /// Gives the method an expression body: <c>Name(...) =&gt; expression;</c>. Valid for
    /// both void and value-returning methods.
    /// </summary>
    public MethodBuilder AsExpressionBody(string expression)
        => With(() => _expressionBody = ParseExpression(expression ?? throw new ArgumentNullException(nameof(expression))));

    #endregion

    internal MethodDeclarationSyntax BuildMethod()
    {
        var method = MethodDeclaration(_returnType, Identifier(Name))
            .WithModifiers(SyntaxFormatting.Modifiers(AccessModifier, IsStatic))
            .WithParameterList(ParameterList(SeparatedList(_params.Select(p =>
                SyntaxFactory.Parameter(Identifier(p.Name)).WithType(p.TypeName.BuildTypeSyntax())))));

        if (_expressionBody is not null)
            return method
                .WithExpressionBody(ArrowExpressionClause(_expressionBody))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

        // A non-void method with no body would emit `int Foo() { }`, which does not
        // compile. Statement bodies are not modelled yet, so require an expression body.
        if (!_returnsVoid)
            throw new NotImplementedException(
                $"Method '{Name}' returns non-void and needs a body. Use AsExpressionBody for now; statement bodies are coming.");

        return method.WithBody(Block());
    }

    MemberDeclarationSyntax IMemberSyntaxBuilder.BuildMember() => BuildMethod();

    internal override SyntaxNode BuildSyntax() => BuildMethod();

    private MethodBuilder With(Action action)
    {
        action();
        return this;
    }
}
