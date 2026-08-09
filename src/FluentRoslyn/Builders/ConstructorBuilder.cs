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
    private readonly TypeBuilder _declaringType;
    private ExpressionSyntax? _expressionBody;
    private ConstructorInitializerSyntax? _initializer;
    private AccessModifier _accessModifier = AccessModifier.Public;

    internal ConstructorBuilder(TypeBuilder declaringType, AccessModifier accessModifier) : base(declaringType.Name, _ => { })
    {
        _declaringType = declaringType;
        AccessModifier = accessModifier;
    }

    /// <summary>Whether this is a static constructor.</summary>
    public bool IsStatic { get; set; }

    /// <summary>
    /// The constructor's accessibility. Ignored for a static constructor, and frozen once
    /// a constructable handle exists: <c>new T(…)</c> through the handle can be emitted
    /// anywhere, and narrowing the constructor afterwards is CS0122 there.
    /// </summary>
    public AccessModifier AccessModifier
    {
        get => _accessModifier;
        set
        {
            RefuseSignatureChange("accessibility");
            _accessModifier = value;
        }
    }

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

    /// <summary>
    /// Hands back a typed handle to this parameterless constructor, so
    /// <c>Value.New(handle)</c> emits a checked <c>new T()</c>.
    /// <typeparamref name="TDeclaring"/> must name the declaring type — its
    /// <c>[EmitsAs]</c> placeholder when that type is being generated.
    /// </summary>
    /// <remarks>
    /// The constructor must be reachable from anywhere in the assembly (<c>public</c>,
    /// <c>internal</c> or <c>protected internal</c>): a <c>new T(…)</c> through the handle
    /// can be emitted from any body in any file, and this side cannot see where. Parameters
    /// and accessibility both freeze once the handle exists, so what it asserts stays true.
    /// </remarks>
    /// <typeparam name="TDeclaring">The type being constructed.</typeparam>
    /// <param name="constructor">Receives the handle.</param>
    /// <returns>This builder, so the chain continues.</returns>
    public ConstructorBuilder AsConstructable<TDeclaring>(out IConstructor<TDeclaring> constructor)
    {
        constructor = new ConstructorHandle0<TDeclaring>(ValidateHandle<TDeclaring>());
        return this;
    }

    /// <summary>Hands back a typed handle to this one-parameter constructor.</summary>
    /// <typeparam name="TDeclaring">The type being constructed.</typeparam>
    /// <typeparam name="T1">The parameter's type, validated against the declared one.</typeparam>
    /// <param name="constructor">Receives the handle.</param>
    /// <returns>This builder, so the chain continues.</returns>
    public ConstructorBuilder AsConstructable<TDeclaring, T1>(out IConstructor<TDeclaring, T1> constructor)
    {
        constructor = new ConstructorHandle1<TDeclaring, T1>(ValidateHandle<TDeclaring>(typeof(T1)));
        return this;
    }

    /// <summary>Hands back a typed handle to this two-parameter constructor.</summary>
    /// <typeparam name="TDeclaring">The type being constructed.</typeparam>
    /// <typeparam name="T1">The first parameter's type.</typeparam>
    /// <typeparam name="T2">The second parameter's type.</typeparam>
    /// <param name="constructor">Receives the handle.</param>
    /// <returns>This builder, so the chain continues.</returns>
    public ConstructorBuilder AsConstructable<TDeclaring, T1, T2>(out IConstructor<TDeclaring, T1, T2> constructor)
    {
        constructor = new ConstructorHandle2<TDeclaring, T1, T2>(ValidateHandle<TDeclaring>(typeof(T1), typeof(T2)));
        return this;
    }

    /// <summary>Hands back a typed handle to this three-parameter constructor.</summary>
    /// <typeparam name="TDeclaring">The type being constructed.</typeparam>
    /// <typeparam name="T1">The first parameter's type.</typeparam>
    /// <typeparam name="T2">The second parameter's type.</typeparam>
    /// <typeparam name="T3">The third parameter's type.</typeparam>
    /// <param name="constructor">Receives the handle.</param>
    /// <returns>This builder, so the chain continues.</returns>
    public ConstructorBuilder AsConstructable<TDeclaring, T1, T2, T3>(
        out IConstructor<TDeclaring, T1, T2, T3> constructor)
    {
        constructor = new ConstructorHandle3<TDeclaring, T1, T2, T3>(
            ValidateHandle<TDeclaring>(typeof(T1), typeof(T2), typeof(T3)));
        return this;
    }

    #endregion

    // The same bargain AsCallable strikes: the handle asserts a shape, validating it here
    // means a handle that exists is one that matches, and freezing the signature
    // afterwards keeps it that way. Both rules live in HandleRules -- the pairing one
    // because AsCallableOn and This<T> assert exactly the same thing, and the signature one
    // because having it twice is how the two sides came to guard different holes.
    private TypeNameBuilder ValidateHandle<TDeclaring>(params Type[] argumentTypes)
    {
        if (IsStatic)
            throw new InvalidOperationException(
                $"Constructor for '{Name}' is static; a static constructor cannot be called. " +
                "Remove Static(), or construct with AddStatement.");

        HandleRules.AssertDeclaringType(StatementContext, _declaringType, typeof(TDeclaring));
        HandleRules.AssertSignature(
            StatementContext, AccessModifier, isGeneric: false, Parameters, argumentTypes);

        FreezeSignature();
        return TypeNameBuilder.New<TDeclaring>();
    }

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
            return SyntaxBodies.ExpressionBodied(ctor, _expressionBody, Statements.Count, StatementContext);

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
