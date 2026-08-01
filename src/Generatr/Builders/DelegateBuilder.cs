using System;
using System.Collections.Generic;
using Generatr.Abstractions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Generatr.Builders;

/// <summary>
/// Builds a delegate declaration: <c>public delegate void Handler(int x);</c>. Obtained
/// from <see cref="NamespaceBuilder.Delegate(string)"/>, or <c>DefineDelegate</c> on a
/// type builder for a nested one.
/// </summary>
public class DelegateBuilder : TypeDeclarationBuilder
{
    private readonly List<IParameter> _params = [];
    private readonly GenericParameters _generics = new();
    private TypeSyntax _returnType;

    internal DelegateBuilder(
        NamespaceBuilder @namespace,
        string name,
        TypeSyntax returnType,
        TypeDeclarationBuilder? declaringType = null) : base(@namespace, name, declaringType)
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

    /// <summary>Adds a using directive, e.g. <c>WithUsing("System.Linq")</c>.</summary>
    public DelegateBuilder WithUsing(string namespaceName) => this.With(() => AddUsing(namespaceName));

    /// <summary>Sets the indentation string, e.g. a tab. Four spaces by default.</summary>
    public DelegateBuilder WithIndentation(string indentation) => this.With(() => SetFormatting(f => f.WithIndentation(indentation)));

    /// <summary>Sets the line endings, e.g. CRLF. LF by default, which keeps output byte-identical across operating systems.</summary>
    public DelegateBuilder WithLineEndings(string lineEndings) => this.With(() => SetFormatting(f => f.WithLineEndings(lineEndings)));

    /// <summary>Shortens generated type references and imports the namespaces they need.</summary>
    public DelegateBuilder SimplifyTypeNames() => this.With(() => EnableTypeNameSimplification());

    /// <summary>Emits a block-scoped namespace instead of the default file-scoped form.</summary>
    public DelegateBuilder BlockScopedNamespace() => this.With(() => IsFileScopedNamespace = false);

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
