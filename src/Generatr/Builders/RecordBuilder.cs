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
/// Builds a positional record: <c>public record Person(string Name, int Age);</c>.
/// </summary>
public class RecordBuilder : TypeDeclarationBuilder
{
    private readonly List<IParameter> _params = [];
    private readonly List<TypeSyntax> _interfaces = [];
    private TypeSyntax? _baseType;
    private string[] _baseArguments = [];
    private readonly GenericParameters _generics = new();
    private bool _isStruct;

    internal RecordBuilder(NamespaceBuilder @namespace, string name, TypeDeclarationBuilder? declaringType = null) : base(@namespace, name, declaringType)
    {
    }

    #region FluentMethods

    /// <summary>Sets the record's accessibility. Public by default.</summary>
    public RecordBuilder WithAccessModifier(AccessModifier accessModifier) => this.With(() => AccessModifier = accessModifier);

    /// <summary>Documents the record with an XML <c>&lt;summary&gt;</c>.</summary>
    public RecordBuilder WithSummary(string text) => this.With(() => AddSummary(text));

    /// <summary>Adds a using directive, e.g. <c>WithUsing("System.Linq")</c>.</summary>
    public RecordBuilder WithUsing(string namespaceName) => this.With(() => AddUsing(namespaceName));

    /// <summary>Shortens generated type references and imports the namespaces they need.</summary>
    public RecordBuilder SimplifyTypeNames() => this.With(() => EnableTypeNameSimplification());

    /// <summary>Emits a block-scoped namespace instead of the default file-scoped form.</summary>
    public RecordBuilder BlockScopedNamespace() => this.With(() => IsFileScopedNamespace = false);

    /// <summary>Emits <c>record struct</c> rather than <c>record</c> (a record class).</summary>
    public RecordBuilder AsStruct() => this.With(() => _isStruct = true);

    /// <summary>Adds a positional parameter, e.g. <c>WithParameter&lt;string&gt;("Name")</c>.</summary>
    public RecordBuilder WithParameter<T>(string name) => this.With(() => _params.Add(Parameter<T>.New(name)));

    /// <summary>Adds an attribute, e.g. <c>WithAttribute("Serializable")</c>.</summary>
    public RecordBuilder WithAttribute(string attribute) => this.With(() => AddAttribute(attribute));

    /// <summary>
    /// Inherits from a base record, forwarding the given arguments to its primary
    /// constructor: <c>record Derived(int X) : Base(X)</c>. The base is emitted before any
    /// interfaces, as C# requires.
    /// </summary>
    public RecordBuilder WithParent(RecordBuilder parent, params string[] arguments)
    {
        if (parent is null) throw new ArgumentNullException(nameof(parent));
        return WithParent(parent.BuildTypeSyntax(), arguments);
    }

    /// <summary>
    /// Inherits from a base record named by a raw type name, forwarding the given
    /// arguments to its primary constructor.
    /// </summary>
    public RecordBuilder WithParent(string typeName, params string[] arguments)
        => WithParent(SyntaxParse.TypeName(typeName), arguments);

    private RecordBuilder WithParent(TypeSyntax baseType, string[] arguments) => this.With(() =>
    {
        _baseType = baseType;
        _baseArguments = arguments ?? throw new ArgumentNullException(nameof(arguments));
    });

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

        if (BuildBaseList() is { } baseList)
            declaration = declaration.WithBaseList(baseList);

        // A record struct carries an explicit `struct` keyword; a record class carries none.
        return _isStruct
            ? declaration.WithClassOrStructKeyword(Token(SyntaxKind.StructKeyword))
            : declaration;
    }

    // The base record comes first and, unlike an interface, carries an argument list
    // forwarding to its primary constructor — so it needs PrimaryConstructorBaseType
    // rather than the SimpleBaseType that SyntaxBaseList produces.
    private BaseListSyntax? BuildBaseList()
    {
        if (_baseType is null)
            return SyntaxBaseList.From(_interfaces);

        if (_isStruct)
            throw new InvalidOperationException($"Record struct '{Name}' cannot inherit from a base record.");

        var arguments = ArgumentList(SeparatedList(
            _baseArguments.Select(a => Argument(SyntaxParse.Expression(a)))));

        var baseTypes = new List<BaseTypeSyntax> { PrimaryConstructorBaseType(_baseType, arguments) };
        baseTypes.AddRange(_interfaces.Select(i => (BaseTypeSyntax)SimpleBaseType(i)));

        return BaseList(SeparatedList(baseTypes));
    }
}
