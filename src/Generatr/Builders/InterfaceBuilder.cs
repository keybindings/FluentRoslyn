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

public class InterfaceBuilder : TypeDeclarationBuilder
{
    private readonly List<InterfacePropertyBuilder> _properties = [];
    private readonly List<InterfaceMethodBuilder> _methods = [];
    private readonly List<TypeSyntax> _baseInterfaces = [];
    private readonly GenericParameters _generics = new();

    internal InterfaceBuilder(NamespaceBuilder @namespace, string name) : base(@namespace, name)
    {
    }

    #region FluentMethods

    public InterfaceBuilder WithAccessModifier(AccessModifier accessModifier) => this.With(() => AccessModifier = accessModifier);

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

    public InterfacePropertyBuilder DefineProperty<T>(string name)
    {
        var pb = new InterfacePropertyBuilder(name, TypeNameBuilder.New<T>());
        _properties.Add(pb);
        return pb;
    }

    public InterfaceMethodBuilder DefineMethod(string name)
    {
        var mb = new InterfaceMethodBuilder(name, PredefinedType(Token(SyntaxKind.VoidKeyword)), []);
        _methods.Add(mb);
        return mb;
    }

    public InterfaceMethodBuilder DefineMethod<TReturn>(string name)
    {
        var mb = new InterfaceMethodBuilder(name, TypeNameBuilder.New<TReturn>().BuildTypeSyntax(), []);
        _methods.Add(mb);
        return mb;
    }

    #endregion

    protected override MemberDeclarationSyntax BuildDeclaration()
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
    private readonly TypeSyntax _returnType;
    private readonly List<IParameter> _params;

    internal InterfaceMethodBuilder(string name, TypeSyntax returnType, IEnumerable<IParameter> @params) : base(name, Identifiers.Validate)
    {
        _returnType = returnType;
        _params = @params.ToList();
    }

    public InterfaceMethodBuilder WithParameter<T>(string name)
    {
        _params.Add(Parameter<T>.New(name));
        return this;
    }

    internal MethodDeclarationSyntax BuildMethod()
        => MethodDeclaration(_returnType, Identifier(Name))
            .WithParameterList(SyntaxParameters.List(_params))
            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

    internal override SyntaxNode BuildSyntax() => BuildMethod();
}

/// <summary>A property signature on an interface: <c>Type Name { get; set; }</c>.</summary>
public class InterfacePropertyBuilder : NamedBuilder
{
    private readonly TypeNameBuilder _type;

    internal InterfacePropertyBuilder(string name, TypeNameBuilder type) : base(name, Identifiers.Validate)
    {
        _type = type;
    }

    public bool HasGet { get; set; } = true;

    public bool HasSet { get; set; } = true;

    /// <summary>Drops the setter, leaving a get-only signature: <c>{ get; }</c>.</summary>
    public InterfacePropertyBuilder GetOnly()
    {
        HasSet = false;
        return this;
    }

    internal PropertyDeclarationSyntax BuildProperty()
    {
        if (!HasGet && !HasSet)
            throw new InvalidOperationException($"Interface property '{Name}' must have a getter or a setter.");

        var accessors = new List<AccessorDeclarationSyntax>();
        if (HasGet) accessors.Add(Accessor(SyntaxKind.GetAccessorDeclaration));
        if (HasSet) accessors.Add(Accessor(SyntaxKind.SetAccessorDeclaration));

        return PropertyDeclaration(_type.BuildTypeSyntax(), Identifier(Name))
            .WithAccessorList(AccessorList(List(accessors)));
    }

    internal override SyntaxNode BuildSyntax() => BuildProperty();

    private static AccessorDeclarationSyntax Accessor(SyntaxKind kind)
        => AccessorDeclaration(kind).WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
}
