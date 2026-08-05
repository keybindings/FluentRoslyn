using System;
using System.Collections.Generic;
using FluentRoslyn.Abstractions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace FluentRoslyn.Builders;

/// <summary>
/// Builds a delegate declaration: <c>public delegate void Handler(int x);</c>. Obtained
/// from <see cref="NamespaceBuilder.Delegate(string)"/>, or <c>DefineDelegate</c> on a
/// type builder for a nested one.
/// </summary>
public class DelegateBuilder : TypeDeclarationBuilder
{
    private readonly List<IParameter> _params = [];
    private readonly GenericParameters _generics = new();

    internal override bool HasTypeParameters => _generics.Any;
    private TypeSyntax _returnType;

    internal DelegateBuilder(
        SourceFile file,
        string name,
        TypeSyntax returnType,
        TypeDeclarationBuilder? declaringType = null) : base(file, name, declaringType)
    {
        _returnType = returnType;
    }

    #region FluentMethods

    /// <summary>Sets the delegate's accessibility. Public by default.</summary>
    public DelegateBuilder WithAccessModifier(AccessModifier accessModifier)
        => this.With(() => AccessModifier = accessModifier);

    /// <summary>Documents the delegate with an XML <c>&lt;summary&gt;</c>.</summary>
    public DelegateBuilder WithSummary(string text) => this.With(() => AddSummary(text));

    /// <summary>Adds an attribute.</summary>
    public DelegateBuilder WithAttribute(string attribute) => this.With(() => AddAttribute(attribute));

    /// <summary>Appends a parameter of type <typeparamref name="T"/>.</summary>
    public DelegateBuilder WithParameter<T>(string name) => this.With(() => _params.Add(Parameter<T>.New(name)));

    /// <summary>
    /// Sets the return type from a raw name, e.g. <c>Returns("T")</c> for a generic
    /// return type that is not a CLR type.
    /// </summary>
    public DelegateBuilder Returns(string typeName)
        => this.With(() => _returnType = SyntaxParse.TypeName(typeName));

    /// <summary>Adds a generic type parameter, e.g. <c>WithTypeParameter("T")</c>.</summary>
    public DelegateBuilder WithTypeParameter(string name) => this.With(() => _generics.AddTypeParameter(name));

    /// <summary>Constrains a type parameter, e.g. <c>WithConstraint("T", "class")</c>.</summary>
    public DelegateBuilder WithConstraint(string typeParameter, string constraint)
        => this.With(() => _generics.AddConstraint(typeParameter, constraint));

    #endregion

    private protected override MemberDeclarationSyntax BuildDeclaration()
    {
        var declaration = DelegateDeclaration(_returnType, Identifier(Name))
            .WithAttributeLists(BuildAttributeLists())
            .WithModifiers(SyntaxFormatting.Modifiers(AccessModifier))
            .WithParameterList(SyntaxParameters.List(_params));

        return _generics.ApplyTo(declaration, $"Delegate '{Name}'");
    }
}
