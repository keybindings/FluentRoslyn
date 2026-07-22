using System;
using System.Collections.Generic;
using System.Text;
using Generatr.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Generatr.Builders;

/// <summary>
/// Builds a positional record: <c>public record Person(string Name, int Age);</c>.
/// </summary>
public class RecordBuilder : NamedBuilder
{
    private readonly List<IParameter> _params = [];
    private readonly List<AttributeSyntax> _attributes = [];
    private readonly List<TypeSyntax> _interfaces = [];
    private readonly List<string> _typeParameters = [];
    private readonly Dictionary<string, List<string>> _constraints = [];
    private bool _isStruct;

    internal RecordBuilder(NamespaceBuilder @namespace, string name) : base(name, Identifiers.Validate)
    {
        Namespace = @namespace;
    }

    public NamespaceBuilder Namespace { get; }

    public bool IsFileScopedNamespace { get; set; } = true;

    public AccessModifier AccessModifier { get; set; } = AccessModifier.Public;

    #region FluentMethods

    public RecordBuilder WithAccessModifier(AccessModifier accessModifier) => With(() => AccessModifier = accessModifier);

    public RecordBuilder BlockScopedNamespace() => With(() => IsFileScopedNamespace = false);

    /// <summary>Emits <c>record struct</c> rather than <c>record</c> (a record class).</summary>
    public RecordBuilder AsStruct() => With(() => _isStruct = true);

    /// <summary>Adds a positional parameter, e.g. <c>WithParameter&lt;string&gt;("Name")</c>.</summary>
    public RecordBuilder WithParameter<T>(string name) => With(() => _params.Add(Parameter<T>.New(name)));

    /// <summary>Adds an attribute, e.g. <c>WithAttribute("Serializable")</c>.</summary>
    public RecordBuilder WithAttribute(string attribute) => With(() => _attributes.Add(SyntaxAttributes.Attribute(attribute)));

    /// <summary>Adds an implemented interface from a raw name, e.g. <c>WithInterface("IEquatable&lt;Person&gt;")</c>.</summary>
    public RecordBuilder WithInterface(string interfaceName)
        => With(() => _interfaces.Add(SyntaxParse.TypeName(interfaceName)));

    /// <summary>Adds an implemented interface from a type, e.g. <c>WithInterface&lt;IDisposable&gt;()</c>.</summary>
    public RecordBuilder WithInterface<TInterface>()
        => With(() => _interfaces.Add(TypeNameBuilder.New<TInterface>().BuildTypeSyntax()));

    /// <summary>Adds a generic type parameter, e.g. <c>WithTypeParameter("T")</c> for <c>Name&lt;T&gt;</c>.</summary>
    public RecordBuilder WithTypeParameter(string name)
        => With(() => _typeParameters.Add(name ?? throw new ArgumentNullException(nameof(name))));

    /// <summary>Constrains a type parameter, e.g. <c>WithConstraint("T", "class")</c>.</summary>
    public RecordBuilder WithConstraint(string typeParameter, string constraint) => With(() =>
    {
        if (constraint is null) throw new ArgumentNullException(nameof(constraint));
        if (!_constraints.TryGetValue(typeParameter, out var list))
            _constraints[typeParameter] = list = [];
        list.Add(constraint);
    });

    #endregion

    internal RecordDeclarationSyntax BuildRecordDeclaration()
    {
        var kind = _isStruct ? SyntaxKind.RecordStructDeclaration : SyntaxKind.RecordDeclaration;

        var declaration = RecordDeclaration(kind, Token(SyntaxKind.RecordKeyword), Identifier(Name))
            .WithAttributeLists(SyntaxAttributes.Lists(_attributes))
            .WithModifiers(SyntaxFormatting.Modifiers(AccessModifier))
            .WithParameterList(SyntaxParameters.List(_params))
            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

        SyntaxGenerics.Validate($"Record '{Name}'", _typeParameters, _constraints);
        if (SyntaxGenerics.TypeParameterList(_typeParameters) is { } typeParams)
            declaration = declaration.WithTypeParameterList(typeParams);

        if (SyntaxBaseList.From(_interfaces) is { } baseList)
            declaration = declaration.WithBaseList(baseList);

        var clauses = SyntaxGenerics.ConstraintClauses(_typeParameters, _constraints);
        if (clauses.Count > 0)
            declaration = declaration.WithConstraintClauses(clauses);

        // A record struct carries an explicit `struct` keyword; a record class carries none.
        return _isStruct
            ? declaration.WithClassOrStructKeyword(Token(SyntaxKind.StructKeyword))
            : declaration;
    }

    public CompilationUnitSyntax BuildCompilationUnit()
        => Namespace.CompilationUnitFor(BuildRecordDeclaration(), IsFileScopedNamespace);

    public SourceText ToSourceText()
        => SourceText.From(ToString(), Encoding.UTF8);

    internal override SyntaxNode BuildSyntax() => BuildCompilationUnit();

    private RecordBuilder With(Action action)
    {
        action();
        return this;
    }
}
