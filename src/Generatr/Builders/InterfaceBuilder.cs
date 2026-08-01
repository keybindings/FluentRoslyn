using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Generatr.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Generatr.Builders;

/// <summary>
/// Builds an interface declaration. Obtained from
/// <see cref="NamespaceBuilder.Interface(string)"/>. Members are bodyless signatures.
/// </summary>
public class InterfaceBuilder : TypeDeclarationBuilder
{
    private readonly List<InterfacePropertyBuilder> _properties = [];
    private readonly List<InterfaceMethodBuilder> _methods = [];
    private readonly List<TypeSyntax> _baseInterfaces = [];
    private readonly GenericParameters _generics = new();

    internal InterfaceBuilder(NamespaceBuilder @namespace, string name, TypeDeclarationBuilder? declaringType = null) : base(@namespace, name, declaringType)
    {
    }

    #region FluentMethods

    /// <summary>Sets the interface's accessibility. Public by default.</summary>
    public InterfaceBuilder WithAccessModifier(AccessModifier accessModifier) => this.With(() => AccessModifier = accessModifier);

    /// <summary>Documents the interface with an XML <c>&lt;summary&gt;</c>.</summary>
    public InterfaceBuilder WithSummary(string text) => this.With(() => AddSummary(text));

    /// <summary>Adds a using directive, e.g. <c>WithUsing("System.Linq")</c>.</summary>
    public InterfaceBuilder WithUsing(string namespaceName) => this.With(() => AddUsing(namespaceName));

    /// <summary>Shortens generated type references and imports the namespaces they need.</summary>
    public InterfaceBuilder SimplifyTypeNames() => this.With(() => EnableTypeNameSimplification());

    /// <summary>Emits a block-scoped namespace instead of the default file-scoped form.</summary>
    public InterfaceBuilder BlockScopedNamespace() => this.With(() => IsFileScopedNamespace = false);

    /// <summary>Adds an attribute, e.g. <c>WithAttribute("Obsolete")</c>.</summary>
    public InterfaceBuilder WithAttribute(string attribute) => this.With(() => AddAttribute(attribute));

    /// <summary>Extends a base interface from a raw name, e.g. <c>Extends("IDisposable")</c>.</summary>
    public InterfaceBuilder Extends(string interfaceName)
        => this.With(() => _baseInterfaces.Add(SyntaxParse.TypeName(interfaceName)));

    /// <summary>Extends a base interface from a type, e.g. <c>Extends&lt;IDisposable&gt;()</c>.</summary>
    public InterfaceBuilder Extends<TInterface>()
        => this.With(() => _baseInterfaces.Add(TypeNameBuilder.New<TInterface>().BuildTypeSyntax()));

    /// <summary>Adds a generic type parameter, e.g. <c>WithTypeParameter("T")</c> for <c>IName&lt;T&gt;</c>.</summary>
    public InterfaceBuilder WithTypeParameter(string name)
        => this.With(() => _generics.AddTypeParameter(name));

    /// <summary>Constrains a type parameter, e.g. <c>WithConstraint("T", "class")</c>.</summary>
    public InterfaceBuilder WithConstraint(string typeParameter, string constraint)
        => this.With(() => _generics.AddConstraint(typeParameter, constraint));

    #endregion

    #region Members

    /// <summary>
    /// Declares a property signature of type <typeparamref name="T"/>:
    /// <c>T Name { get; set; }</c>.
    /// </summary>
    public InterfacePropertyBuilder DefineProperty<T>(string name)
    {
        var pb = new InterfacePropertyBuilder(name, TypeNameBuilder.New<T>());
        _properties.Add(pb);
        return pb;
    }

    /// <summary>Declares a <c>void</c> method signature.</summary>
    public InterfaceMethodBuilder DefineMethod(string name)
    {
        var mb = new InterfaceMethodBuilder(name, PredefinedType(Token(SyntaxKind.VoidKeyword)), []);
        _methods.Add(mb);
        return mb;
    }

    /// <summary>Declares a method signature returning <typeparamref name="TReturn"/>.</summary>
    public InterfaceMethodBuilder DefineMethod<TReturn>(string name)
    {
        var mb = new InterfaceMethodBuilder(name, TypeNameBuilder.New<TReturn>().BuildTypeSyntax(), []);
        _methods.Add(mb);
        return mb;
    }

    #endregion

    private protected override MemberDeclarationSyntax BuildDeclaration()
    {
        // Signatures group as properties then methods, preserving insertion order.
        var members = _properties.Select(p => (MemberDeclarationSyntax)p.BuildProperty())
            .Concat(_methods.Select(m => (MemberDeclarationSyntax)m.BuildMethod()));

        var declaration = InterfaceDeclaration(Name)
            .WithAttributeLists(BuildAttributeLists())
            .WithModifiers(SyntaxFormatting.Modifiers(AccessModifier))
            .WithMembers(List(members));

        declaration = _generics.ApplyTo(declaration, $"Interface '{Name}'");

        if (SyntaxBaseList.From(_baseInterfaces) is { } baseList)
            declaration = declaration.WithBaseList(baseList);

        return declaration;
    }
}

/// <summary>A method signature on an interface: <c>ReturnType Name(params);</c>.</summary>
public class InterfaceMethodBuilder : NamedBuilder
{
    private readonly List<IParameter> _params;
    private readonly List<AttributeSyntax> _attributes = [];
    private readonly GenericParameters _generics = new();
    private readonly DocComment _docs = new();
    private TypeSyntax _returnType;

    internal InterfaceMethodBuilder(string name, TypeSyntax returnType, IEnumerable<IParameter> @params) : base(name, Identifiers.Validate)
    {
        _returnType = returnType;
        _params = @params.ToList();
    }

    /// <summary>Appends a parameter of type <typeparamref name="T"/>.</summary>
    public InterfaceMethodBuilder WithParameter<T>(string name) => this.With(() => _params.Add(Parameter<T>.New(name)));

    /// <summary>Adds a generic type parameter: <c>ReturnType Name&lt;T&gt;(...);</c>.</summary>
    public InterfaceMethodBuilder WithTypeParameter(string name) => this.With(() => _generics.AddTypeParameter(name));

    /// <summary>Constrains a type parameter, e.g. <c>WithConstraint("T", "class")</c>.</summary>
    public InterfaceMethodBuilder WithConstraint(string typeParameter, string constraint)
        => this.With(() => _generics.AddConstraint(typeParameter, constraint));

    /// <summary>Adds an attribute, e.g. <c>WithAttribute("Obsolete")</c>.</summary>
    public InterfaceMethodBuilder WithAttribute(string attribute) => this.With(() => _attributes.Add(SyntaxAttributes.Attribute(attribute)));

    /// <summary>Documents the signature with an XML <c>&lt;summary&gt;</c>.</summary>
    public InterfaceMethodBuilder WithSummary(string text) => this.With(() => _docs.SetSummary(text));

    /// <summary>Documents a parameter: <c>&lt;param name="..."&gt;</c>.</summary>
    public InterfaceMethodBuilder WithParameterDoc(string parameterName, string text)
        => this.With(() => _docs.AddParameter(parameterName, text));

    /// <summary>Documents the return value: <c>&lt;returns&gt;</c>.</summary>
    public InterfaceMethodBuilder WithReturnsDoc(string text) => this.With(() => _docs.SetReturns(text));

    /// <summary>Sets the return type from a raw name, e.g. <c>Returns("T")</c> for a generic return.</summary>
    public InterfaceMethodBuilder Returns(string typeName) => this.With(() => _returnType = SyntaxParse.TypeName(typeName));

    internal MethodDeclarationSyntax BuildMethod()
    {
        var method = MethodDeclaration(_returnType, Identifier(Name))
            .WithAttributeLists(SyntaxAttributes.Lists(_attributes))
            .WithParameterList(SyntaxParameters.List(_params))
            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

        method = _generics.ApplyTo(method, $"Interface method '{Name}'");

        return _docs.IsEmpty ? method : method.WithLeadingTrivia(_docs.Build());
    }

    internal override SyntaxNode BuildSyntax() => BuildMethod();
}

/// <summary>A property signature on an interface: <c>Type Name { get; set; }</c>.</summary>
public class InterfacePropertyBuilder : NamedBuilder
{
    private readonly TypeNameBuilder _type;
    private readonly List<AttributeSyntax> _attributes = [];
    private readonly DocComment _docs = new();

    internal InterfacePropertyBuilder(string name, TypeNameBuilder type) : base(name, Identifiers.Validate)
    {
        _type = type;
    }

    /// <summary>Whether the signature declares a getter. True by default.</summary>
    public bool HasGet { get; set; } = true;

    /// <summary>Whether the signature declares a setter. True by default.</summary>
    public bool HasSet { get; set; } = true;

    /// <summary>Whether the setter is emitted as <c>init</c> rather than <c>set</c>.</summary>
    public bool SetterIsInit { get; set; }

    /// <summary>Drops the setter, leaving a get-only signature: <c>{ get; }</c>.</summary>
    public InterfacePropertyBuilder GetOnly() => this.With(() => HasSet = false);

    /// <summary>Emits the setter as an init accessor: <c>{ get; init; }</c>.</summary>
    public InterfacePropertyBuilder InitOnly() => this.With(() =>
    {
        HasSet = true;
        SetterIsInit = true;
    });

    /// <summary>Adds an attribute, e.g. <c>WithAttribute("Obsolete")</c>.</summary>
    public InterfacePropertyBuilder WithAttribute(string attribute) => this.With(() => _attributes.Add(SyntaxAttributes.Attribute(attribute)));

    /// <summary>Documents the signature with an XML <c>&lt;summary&gt;</c>.</summary>
    public InterfacePropertyBuilder WithSummary(string text) => this.With(() => _docs.SetSummary(text));

    internal PropertyDeclarationSyntax BuildProperty()
    {
        if (!HasGet && !HasSet)
            throw new InvalidOperationException($"Interface property '{Name}' must have a getter or a setter.");

        var accessors = new List<AccessorDeclarationSyntax>();
        if (HasGet) accessors.Add(Accessor(SyntaxKind.GetAccessorDeclaration));
        if (HasSet) accessors.Add(Accessor(SetterIsInit ? SyntaxKind.InitAccessorDeclaration : SyntaxKind.SetAccessorDeclaration));

        var property = PropertyDeclaration(_type.BuildTypeSyntax(), Identifier(Name))
            .WithAttributeLists(SyntaxAttributes.Lists(_attributes))
            .WithAccessorList(AccessorList(List(accessors)));

        return _docs.IsEmpty ? property : property.WithLeadingTrivia(_docs.Build());
    }

    internal override SyntaxNode BuildSyntax() => BuildProperty();

    private static AccessorDeclarationSyntax Accessor(SyntaxKind kind)
        => AccessorDeclaration(kind).WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
}
