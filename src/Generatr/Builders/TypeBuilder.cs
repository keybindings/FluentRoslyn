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
/// The member-bearing type kinds (class, struct): adds member definition, implemented
/// interfaces, and generics on top of the shared declaration machinery. Concrete kinds
/// implement <see cref="BuildTypeDeclaration"/>.
/// </summary>
public abstract class TypeBuilder : TypeDeclarationBuilder
{
    private readonly List<FieldBuilder> _fields = [];
    private readonly List<ConstructorBuilder> _constructors = [];
    private readonly List<EventBuilder> _events = [];
    private readonly List<PropertyBuilder> _properties = [];
    private readonly List<MethodBuilder> _methods = [];
    private readonly List<TypeDeclarationBuilder> _nestedTypes = [];
    private readonly List<TypeSyntax> _interfaces = [];
    private readonly GenericParameters _generics = new();

    private protected TypeBuilder(NamespaceBuilder @namespace, string name, TypeDeclarationBuilder? declaringType) : base(@namespace, name, declaringType)
    {
    }

    #region Members

    /// <summary>Declares a field of type <typeparamref name="T"/>, private by default.</summary>
    public FieldBuilder<T> DefineField<T>(string name)
        => DefineField<T>(name, AccessModifier.Private);

    /// <summary>Declares a field of type <typeparamref name="T"/>.</summary>
    public FieldBuilder<T> DefineField<T>(string name, AccessModifier accessModifier)
    {
        var fb = new FieldBuilder<T>(name, accessModifier);
        _fields.Add(fb);
        return fb;
    }

    /// <summary>Declares a public constructor. Add parameters with <c>WithParameter&lt;T&gt;</c>.</summary>
    public ConstructorBuilder DefineConstructor()
        => DefineConstructor(AccessModifier.Public);

    /// <summary>Declares a constructor. Add parameters with <c>WithParameter&lt;T&gt;</c>.</summary>
    public ConstructorBuilder DefineConstructor(AccessModifier accessModifier)
    {
        var cb = new ConstructorBuilder(this, accessModifier);
        _constructors.Add(cb);
        return cb;
    }

    /// <summary>Declares a public auto-property of type <typeparamref name="T"/>.</summary>
    public PropertyBuilder<T> DefineProperty<T>(string name)
        => DefineProperty<T>(name, AccessModifier.Public);

    /// <summary>Declares an auto-property of type <typeparamref name="T"/>.</summary>
    public PropertyBuilder<T> DefineProperty<T>(string name, AccessModifier accessModifier)
    {
        var pb = new PropertyBuilder<T>(name, accessModifier);
        _properties.Add(pb);
        return pb;
    }

    /// <summary>
    /// Declares a field-like event whose handler type is <typeparamref name="THandler"/>,
    /// e.g. <c>DefineEvent&lt;EventHandler&gt;("Changed")</c>.
    /// </summary>
    public EventBuilder DefineEvent<THandler>(string name)
        => DefineEvent<THandler>(name, AccessModifier.Public);

    /// <summary>Declares a field-like event whose handler type is <typeparamref name="THandler"/>.</summary>
    public EventBuilder DefineEvent<THandler>(string name, AccessModifier accessModifier)
        => AddEvent(new EventBuilder(name, TypeNameBuilder.New<THandler>().BuildTypeSyntax(), accessModifier));

    /// <summary>
    /// Declares a field-like event whose handler type is named by a raw string — for a
    /// delegate that does not exist as a CLR type, such as one being generated alongside.
    /// </summary>
    public EventBuilder DefineEvent(string name, string handlerTypeName)
        => AddEvent(new EventBuilder(name, SyntaxParse.TypeName(handlerTypeName), AccessModifier.Public));

    private EventBuilder AddEvent(EventBuilder @event)
    {
        _events.Add(@event);
        return @event;
    }

    /// <summary>Declares a public <c>void</c> method with an empty body.</summary>
    public MethodBuilder DefineMethod(string name)
        => DefineMethod(name, AccessModifier.Public);

    /// <summary>Declares a <c>void</c> method with an empty body.</summary>
    public MethodBuilder DefineMethod(string name, AccessModifier accessModifier)
        => AddMethod(MethodBuilder.Action(name, accessModifier));

    /// <summary>
    /// Declares a public method returning <typeparamref name="TReturn"/>. A
    /// value-returning method needs a body — see <c>AsExpressionBody</c> or
    /// <c>AddStatement</c>.
    /// </summary>
    public MethodBuilder DefineMethod<TReturn>(string name)
        => DefineMethod<TReturn>(name, AccessModifier.Public);

    /// <summary>
    /// Declares a method returning <typeparamref name="TReturn"/>. A value-returning
    /// method needs a body — see <c>AsExpressionBody</c> or <c>AddStatement</c>.
    /// </summary>
    public MethodBuilder DefineMethod<TReturn>(string name, AccessModifier accessModifier)
        => AddMethod(MethodBuilder.Returning(name, accessModifier, TypeNameBuilder.New<TReturn>()));

    private MethodBuilder AddMethod(MethodBuilder method)
    {
        _methods.Add(method);
        return method;
    }

    #endregion

    #region Nested types

    /// <summary>Declares a class nested inside this type.</summary>
    public ClassBuilder DefineClass(string name) => AddNested(new ClassBuilder(Namespace, name, this));

    /// <summary>Declares a struct nested inside this type.</summary>
    public StructBuilder DefineStruct(string name) => AddNested(new StructBuilder(Namespace, name, this));

    /// <summary>Declares an enum nested inside this type.</summary>
    public EnumBuilder DefineEnum(string name) => AddNested(new EnumBuilder(Namespace, name, this));

    /// <summary>Declares a positional record nested inside this type.</summary>
    public RecordBuilder DefineRecord(string name) => AddNested(new RecordBuilder(Namespace, name, this));

    /// <summary>Declares an interface nested inside this type.</summary>
    public InterfaceBuilder DefineInterface(string name) => AddNested(new InterfaceBuilder(Namespace, name, this));

    /// <summary>Declares a <c>void</c>-returning delegate nested inside this type.</summary>
    public DelegateBuilder DefineDelegate(string name)
        => AddNested(new DelegateBuilder(Namespace, name, PredefinedType(Token(SyntaxKind.VoidKeyword)), this));

    /// <summary>Declares a nested delegate returning <typeparamref name="TReturn"/>.</summary>
    public DelegateBuilder DefineDelegate<TReturn>(string name)
        => AddNested(new DelegateBuilder(Namespace, name, TypeNameBuilder.New<TReturn>().BuildTypeSyntax(), this));

    private TNested AddNested<TNested>(TNested nested) where TNested : TypeDeclarationBuilder
    {
        _nestedTypes.Add(nested);
        return nested;
    }

    #endregion

    /// <summary>Builds the type declaration for this kind (class, struct, ...).</summary>
    private protected abstract TypeDeclarationSyntax BuildTypeDeclaration();

    private protected override MemberDeclarationSyntax BuildDeclaration() => BuildTypeDeclaration();

    /// <summary>
    /// Whether this type may declare abstract members. Only an abstract class can;
    /// structs and non-abstract classes cannot.
    /// </summary>
    private protected virtual bool AllowsAbstractMembers => false;

    // Member group order: fields, constructors, events, properties, methods, nested
    // types;
    // within each group, least protected first, then alphabetical.
    private protected SyntaxList<MemberDeclarationSyntax> BuildMembers()
    {
        // An abstract member in a non-abstract type does not compile, and only the type
        // knows both halves — so the check belongs here rather than in the member builder.
        if (!AllowsAbstractMembers && _methods.FirstOrDefault(m => m.IsAbstract) is { } abstractMethod)
            throw new InvalidOperationException(
                $"Type '{Name}' declares abstract method '{abstractMethod.Name}' but is not abstract.");

        var members = new List<MemberDeclarationSyntax>();
        AddMemberGroup(members, _fields);
        AddMemberGroup(members, _constructors);
        AddMemberGroup(members, _events);
        AddMemberGroup(members, _properties);
        AddMemberGroup(members, _methods);
        AddNestedTypes(members);
        return List(members);
    }

    // Nested types sort by the same rule as members, but they are not IMemberSyntaxBuilder
    // so they cannot go through AddMemberGroup.
    private void AddNestedTypes(List<MemberDeclarationSyntax> members)
        => members.AddRange(_nestedTypes
            .OrderBy(x => x.AccessModifier.AccessabilityLevel)
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .Select(x => x.BuildDocumentedDeclaration()));

    private protected void AddInterface(TypeSyntax @interface)
        => _interfaces.Add(@interface);

    private protected void AddTypeParameter(string name)
        => _generics.AddTypeParameter(name);

    private protected void AddConstraint(string typeParameter, string constraint)
        => _generics.AddConstraint(typeParameter, constraint);

    // Applies the type-parameter list and where-clauses to a declaration.
    private protected TDeclaration ApplyGenerics<TDeclaration>(TDeclaration declaration)
        where TDeclaration : TypeDeclarationSyntax
        => _generics.ApplyTo(declaration, $"Type '{Name}'");

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
    private protected TypeBuilder(NamespaceBuilder @namespace, string name, TypeDeclarationBuilder? declaringType) : base(@namespace, name, declaringType)
    {
    }

    /// <summary>Sets the type's accessibility. Public by default.</summary>
    public TSelf WithAccessModifier(AccessModifier accessModifier)
    {
        AccessModifier = accessModifier;
        return (TSelf)this;
    }

    /// <summary>
    /// Emits a block-scoped namespace (<c>namespace N { ... }</c>) instead of the
    /// default file-scoped form (<c>namespace N;</c>).
    /// </summary>
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
    /// Documents the type with an XML <c>&lt;summary&gt;</c>. Newlines become separate
    /// comment lines, and XML markup characters are escaped.
    /// </summary>
    public TSelf WithSummary(string text)
    {
        AddSummary(text);
        return (TSelf)this;
    }

    /// <summary>Adds a using directive, e.g. <c>WithUsing("System.Linq")</c>.</summary>
    public TSelf WithUsing(string namespaceName)
    {
        AddUsing(namespaceName);
        return (TSelf)this;
    }

    /// <summary>
    /// Sets the indentation string, e.g. <c>"\t"</c>. Four spaces by default.
    /// </summary>
    public TSelf WithIndentation(string indentation)
    {
        SetFormatting(f => f.WithIndentation(indentation));
        return (TSelf)this;
    }

    /// <summary>
    /// Sets the line endings, e.g. <c>"\r\n"</c>. <c>"\n"</c> by default, which keeps
    /// output byte-identical across operating systems.
    /// </summary>
    public TSelf WithLineEndings(string lineEndings)
    {
        SetFormatting(f => f.WithLineEndings(lineEndings));
        return (TSelf)this;
    }

    /// <summary>
    /// Shortens generated type references and imports the namespaces they need, so
    /// <c>System.Collections.Generic.List&lt;int&gt;</c> becomes <c>List&lt;int&gt;</c>
    /// under a <c>using System.Collections.Generic;</c>. A name offered by two different
    /// namespaces, or one this file declares itself, stays fully qualified.
    /// </summary>
    public TSelf SimplifyTypeNames()
    {
        EnableTypeNameSimplification();
        return (TSelf)this;
    }

    /// <summary>
    /// Adds an implemented interface from a raw name, e.g.
    /// <c>WithInterface("IEquatable&lt;Point&gt;")</c>.
    /// </summary>
    public TSelf WithInterface(string interfaceName)
    {
        AddInterface(SyntaxParse.TypeName(interfaceName));
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
