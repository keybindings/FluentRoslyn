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
public class RecordBuilder : TypeDeclarationBuilder
{
    private readonly List<IParameter> _params = [];
    private readonly List<TypeSyntax> _interfaces = [];
    private readonly GenericParameters _generics = new();
    private bool _isStruct;

    internal RecordBuilder(NamespaceBuilder @namespace, string name) : base(@namespace, name)
    {
    }

    #region FluentMethods

    /// <summary>Sets the record's accessibility. Public by default.</summary>
    public RecordBuilder WithAccessModifier(AccessModifier accessModifier) => this.With(() => AccessModifier = accessModifier);

    /// <summary>Documents the record with an XML <c>&lt;summary&gt;</c>.</summary>
    public RecordBuilder WithSummary(string text) => this.With(() => AddSummary(text));

    /// <summary>Emits a block-scoped namespace instead of the default file-scoped form.</summary>
    public RecordBuilder BlockScopedNamespace() => this.With(() => IsFileScopedNamespace = false);

    /// <summary>Emits <c>record struct</c> rather than <c>record</c> (a record class).</summary>
    public RecordBuilder AsStruct() => this.With(() => _isStruct = true);

    /// <summary>Adds a positional parameter, e.g. <c>WithParameter&lt;string&gt;("Name")</c>.</summary>
    public RecordBuilder WithParameter<T>(string name) => this.With(() => _params.Add(Parameter<T>.New(name)));

    /// <summary>Adds an attribute, e.g. <c>WithAttribute("Serializable")</c>.</summary>
    public RecordBuilder WithAttribute(string attribute) => this.With(() => AddAttribute(attribute));

    /// <summary>Adds an implemented interface from a raw name, e.g. <c>WithInterface("IEquatable&lt;Person&gt;")</c>.</summary>
    public RecordBuilder WithInterface(string interfaceName)
        => this.With(() => _interfaces.Add(SyntaxParse.TypeName(interfaceName)));

    /// <summary>Adds an implemented interface from a type, e.g. <c>WithInterface&lt;IDisposable&gt;()</c>.</summary>
    public RecordBuilder WithInterface<TInterface>()
        => this.With(() => _interfaces.Add(TypeNameBuilder.New<TInterface>().BuildTypeSyntax()));

    /// <summary>Adds a generic type parameter, e.g. <c>WithTypeParameter("T")</c> for <c>Name&lt;T&gt;</c>.</summary>
    public RecordBuilder WithTypeParameter(string name)
        => this.With(() => _generics.AddTypeParameter(name));

    /// <summary>Constrains a type parameter, e.g. <c>WithConstraint("T", "class")</c>.</summary>
    public RecordBuilder WithConstraint(string typeParameter, string constraint)
        => this.With(() => _generics.AddConstraint(typeParameter, constraint));

    #endregion

    private protected override MemberDeclarationSyntax BuildDeclaration()
    {
        var kind = _isStruct ? SyntaxKind.RecordStructDeclaration : SyntaxKind.RecordDeclaration;

        var declaration = RecordDeclaration(kind, Token(SyntaxKind.RecordKeyword), Identifier(Name))
            .WithAttributeLists(BuildAttributeLists())
            .WithModifiers(SyntaxFormatting.Modifiers(AccessModifier))
            .WithParameterList(SyntaxParameters.List(_params))
            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

        declaration = _generics.ApplyTo(declaration, $"Record '{Name}'");

        if (SyntaxBaseList.From(_interfaces) is { } baseList)
            declaration = declaration.WithBaseList(baseList);

        // A record struct carries an explicit `struct` keyword; a record class carries none.
        return _isStruct
            ? declaration.WithClassOrStructKeyword(Token(SyntaxKind.StructKeyword))
            : declaration;
    }
}
