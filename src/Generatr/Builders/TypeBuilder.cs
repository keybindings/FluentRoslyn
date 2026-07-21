using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Generatr.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Generatr.Builders;

/// <summary>
/// Shared machinery for the member-bearing type kinds (class, struct): member
/// definition, attribute storage, and the namespace/compilation-unit pipeline.
/// Concrete kinds implement <see cref="BuildTypeDeclaration"/>.
/// </summary>
public abstract class TypeBuilder : NamedBuilder
{
    private readonly List<FieldBuilder> _fields = [];
    private readonly List<ConstructorBuilder> _constructors = [];
    private readonly List<PropertyBuilder> _properties = [];
    private readonly List<MethodBuilder> _methods = [];
    private readonly List<AttributeSyntax> _attributes = [];
    private readonly List<TypeSyntax> _interfaces = [];
    private readonly List<string> _typeParameters = [];
    private readonly Dictionary<string, List<string>> _constraints = [];

    private protected TypeBuilder(NamespaceBuilder @namespace, string name) : base(name, NameValidation)
    {
        Namespace = @namespace;
    }

    public NamespaceBuilder Namespace { get; }

    public bool IsFileScopedNamespace { get; set; } = true;

    public AccessModifier AccessModifier { get; set; } = AccessModifier.Public;

    #region Members

    public FieldBuilder<T> DefineField<T>(string name)
        => DefineField<T>(name, AccessModifier.Private);

    public FieldBuilder<T> DefineField<T>(string name, AccessModifier accessModifier)
    {
        var fb = new FieldBuilder<T>(this, name, accessModifier);
        _fields.Add(fb);
        return fb;
    }

    public ConstructorBuilder DefineConstructor()
        => DefineConstructor(AccessModifier.Public);

    public ConstructorBuilder DefineConstructor(AccessModifier accessModifier, params IParameter[] parameters)
    {
        var cb = new ConstructorBuilder(this, accessModifier, parameters);
        _constructors.Add(cb);
        return cb;
    }

    public PropertyBuilder<T> DefineProperty<T>(string name)
        => DefineProperty<T>(name, AccessModifier.Public);

    public PropertyBuilder<T> DefineProperty<T>(string name, AccessModifier accessModifier)
    {
        var pb = new PropertyBuilder<T>(this, name, accessModifier);
        _properties.Add(pb);
        return pb;
    }

    public MethodBuilder DefineMethod(string name)
        => DefineMethod(name, AccessModifier.Public);

    public MethodBuilder DefineMethod(string name, AccessModifier accessModifier, params IParameter[] parameters)
        => AddMethod(MethodBuilder.Action(name, accessModifier, parameters));

    public MethodBuilder DefineMethod<TReturn>(string name)
        => DefineMethod<TReturn>(name, AccessModifier.Public);

    public MethodBuilder DefineMethod<TReturn>(string name, AccessModifier accessModifier, params IParameter[] parameters)
        => AddMethod(MethodBuilder.Returning(name, accessModifier, TypeNameBuilder.New<TReturn>(), parameters));

    private MethodBuilder AddMethod(MethodBuilder method)
    {
        _methods.Add(method);
        return method;
    }

    #endregion

    /// <summary>Builds the type declaration for this kind (class, struct, ...).</summary>
    protected abstract TypeDeclarationSyntax BuildTypeDeclaration();

    // Member group order: fields, constructors, properties, methods; within each group,
    // least protected first, then alphabetical.
    private protected SyntaxList<MemberDeclarationSyntax> BuildMembers()
    {
        var members = new List<MemberDeclarationSyntax>();
        AddMemberGroup(members, _fields);
        AddMemberGroup(members, _constructors);
        AddMemberGroup(members, _properties);
        AddMemberGroup(members, _methods);
        return List(members);
    }

    private protected SyntaxList<AttributeListSyntax> BuildAttributeLists()
        => SyntaxAttributes.Lists(_attributes);

    private protected void AddAttribute(string attribute)
        => _attributes.Add(SyntaxAttributes.Attribute(attribute));

    private protected void AddInterface(TypeSyntax @interface)
        => _interfaces.Add(@interface);

    private protected void AddTypeParameter(string name)
        => _typeParameters.Add(name ?? throw new ArgumentNullException(nameof(name)));

    private protected void AddConstraint(string typeParameter, string constraint)
    {
        if (constraint is null) throw new ArgumentNullException(nameof(constraint));
        if (!_constraints.TryGetValue(typeParameter, out var list))
            _constraints[typeParameter] = list = [];
        list.Add(constraint);
    }

    // Applies the type-parameter list and where-clauses to a declaration.
    private protected TDeclaration ApplyGenerics<TDeclaration>(TDeclaration declaration)
        where TDeclaration : TypeDeclarationSyntax
    {
        SyntaxGenerics.Validate($"Type '{Name}'", _typeParameters, _constraints);

        if (SyntaxGenerics.TypeParameterList(_typeParameters) is { } list)
            declaration = (TDeclaration)declaration.WithTypeParameterList(list);

        var clauses = SyntaxGenerics.ConstraintClauses(_typeParameters, _constraints);
        return clauses.Count == 0 ? declaration : (TDeclaration)declaration.WithConstraintClauses(clauses);
    }

    /// <summary>
    /// Builds the base list from an optional base type followed by the implemented
    /// interfaces (C# requires the base class first). Null when there is neither.
    /// </summary>
    private protected BaseListSyntax? BuildBaseList(TypeSyntax? baseType)
    {
        var types = baseType is null ? _interfaces : Prepend(baseType, _interfaces);
        return SyntaxBaseList.From(types);
    }

    private static IEnumerable<TypeSyntax> Prepend(TypeSyntax first, IEnumerable<TypeSyntax> rest)
    {
        yield return first;
        foreach (var type in rest) yield return type;
    }

    public CompilationUnitSyntax BuildCompilationUnit()
        => Namespace.CompilationUnitFor(BuildTypeDeclaration(), IsFileScopedNamespace);

    public SourceText ToSourceText()
        => SourceText.From(ToString(), Encoding.UTF8);

    /// <summary>The fully qualified name of this type, for use as a type reference.</summary>
    internal TypeSyntax BuildTypeSyntax()
        => Namespace.IsGlobal
            ? IdentifierName(Name)
            : QualifiedName(Namespace.BuildNameSyntax(), IdentifierName(Name));

    internal override SyntaxNode BuildSyntax() => BuildCompilationUnit();

    private static void NameValidation(string name)
        => Identifiers.Validate(name);

    // AccessabilityLevel runs Public = 0 through Private = 5, so ascending gives
    // least protected first.
    private static void AddMemberGroup<TMember>(List<MemberDeclarationSyntax> members, IEnumerable<TMember> group)
        where TMember : NamedBuilder, IAccessModifier, IMemberSyntaxBuilder
        => members.AddRange(group
            .OrderBy(x => x.AccessModifier.AccessabilityLevel)
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .Select(x => x.BuildMember()));
}

/// <summary>
/// Adds fluent type-level setters that return the concrete builder, so class- and
/// struct-specific methods chain with the shared ones. TSelf is the concrete kind.
/// </summary>
public abstract class TypeBuilder<TSelf> : TypeBuilder
    where TSelf : TypeBuilder<TSelf>
{
    private protected TypeBuilder(NamespaceBuilder @namespace, string name) : base(@namespace, name)
    {
    }

    public TSelf WithAccessModifier(AccessModifier accessModifier)
    {
        AccessModifier = accessModifier;
        return (TSelf)this;
    }

    public TSelf BlockScopedNamespace()
    {
        IsFileScopedNamespace = false;
        return (TSelf)this;
    }

    /// <summary>Adds an attribute, e.g. <c>WithAttribute("Serializable")</c>.</summary>
    public TSelf WithAttribute(string attribute)
    {
        AddAttribute(attribute);
        return (TSelf)this;
    }

    /// <summary>
    /// Adds an implemented interface from a raw name, e.g.
    /// <c>WithInterface("IEquatable&lt;Point&gt;")</c>.
    /// </summary>
    public TSelf WithInterface(string interfaceName)
    {
        AddInterface(ParseTypeName(interfaceName ?? throw new ArgumentNullException(nameof(interfaceName))));
        return (TSelf)this;
    }

    /// <summary>Adds an implemented interface from a type, e.g. <c>WithInterface&lt;IDisposable&gt;()</c>.</summary>
    public TSelf WithInterface<TInterface>()
    {
        AddInterface(TypeNameBuilder.New<TInterface>().BuildTypeSyntax());
        return (TSelf)this;
    }

    /// <summary>Adds a generic type parameter, e.g. <c>WithTypeParameter("T")</c> for <c>Name&lt;T&gt;</c>.</summary>
    public TSelf WithTypeParameter(string name)
    {
        AddTypeParameter(name);
        return (TSelf)this;
    }

    /// <summary>
    /// Constrains a type parameter, e.g. <c>WithConstraint("T", "class")</c>. Call once
    /// per constraint; C# order is class/struct first, new() last.
    /// </summary>
    public TSelf WithConstraint(string typeParameter, string constraint)
    {
        AddConstraint(typeParameter, constraint);
        return (TSelf)this;
    }
}
