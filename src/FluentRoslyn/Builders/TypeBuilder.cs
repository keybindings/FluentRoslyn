using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentRoslyn.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace FluentRoslyn.Builders;

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
    // Typed as the operator contract, not IMemberSyntaxBuilder: validation iterates the
    // list directly, so nothing can be added that the operator rules silently skip.
    private readonly OperatorSet _operators = new();
    // Methods of differing return types are different classes, so the list is typed by
    // what a declaring type actually needs from them.
    private readonly List<IMethodMember> _methods = [];
    private readonly List<TypeDeclarationBuilder> _nestedTypes = [];
    private readonly List<TypeSyntax> _interfaces = [];
    private readonly GenericParameters _generics = new();

    private protected TypeBuilder(SourceFile file, string name, TypeDeclarationBuilder? declaringType) : base(file, name, declaringType)
    {
    }

    internal override bool HasTypeParameters => _generics.Any;

    /// <summary>
    /// A reference to <c>this</c> inside a body of this type, untyped — for a type with
    /// no <c>[EmitsAs]</c> placeholder to name it, which is every type a generator
    /// discovers rather than declares.
    /// </summary>
    /// <remarks>
    /// Untyped means it reaches only the positions that accept a bare value, above all
    /// <c>Return</c> on a method with a raw return type. Use <see cref="This{T}"/> when a
    /// placeholder exists and the typed surface is wanted. Using either from a static
    /// member is rejected at emission, because there is no <c>this</c> to emit.
    /// </remarks>
    /// <returns>A reference to <c>this</c>.</returns>
    public IReference This() => new ThisReference();

    /// <summary>
    /// A reference to <c>this</c> inside a body of this type, typed through the
    /// declaring type's <c>[EmitsAs]</c> placeholder — so it composes with the typed
    /// surface as a call receiver, an argument, or an assigned value.
    /// </summary>
    /// <remarks>
    /// <typeparamref name="T"/> is paired with this type by the rule <c>AsCallableOn</c>
    /// uses and for the same reason: a placeholder's emitted name and the declaring
    /// type's qualified name are the same string, because that is what both become in
    /// the generated source. A mismatch throws rather than emitting a <c>this</c> that
    /// claims to be something else.
    /// </remarks>
    /// <typeparam name="T">This type, named by its placeholder.</typeparam>
    /// <returns>A typed reference to <c>this</c>.</returns>
    public IReference<T> This<T>()
    {
        var asserted = TypeNameBuilder.New<T>().ToString();
        var declared = BuildTypeSyntax().ToString();

        if (!string.Equals(asserted, declared, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Type '{declared}' cannot hand back a 'this' typed as '{asserted}'. The type " +
                "argument must name this type — its [EmitsAs] placeholder when it is being generated.");

        return new ThisReference<T>();
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

    /// <summary>
    /// Declares a field whose type is named by text, private by default — for a type the
    /// generator cannot name as <c>T</c>, above all one discovered from the consumer's
    /// compilation as an <c>ISymbol</c>.
    /// </summary>
    /// <remarks>
    /// The name comes first, matching <see cref="DefineEvent(string, string)"/> and
    /// <c>Returns(string)</c>, the raw-name escape hatches this joins. Transposing the
    /// two is caught: the name is validated as a C# identifier, which a qualified type
    /// name is not. The result is a <see cref="RawFieldBuilder"/> rather than an
    /// <c>IReference&lt;T&gt;</c>, because there is no <c>T</c> to check against.
    /// </remarks>
    /// <param name="name">The field's name.</param>
    /// <param name="typeName">The field's type, as C# text. Parsed, so a malformed name is rejected.</param>
    /// <returns>The field builder.</returns>
    public RawFieldBuilder DefineField(string name, string typeName)
        => DefineField(name, typeName, AccessModifier.Private);

    /// <summary>Declares a field whose type is named by text.</summary>
    /// <param name="name">The field's name.</param>
    /// <param name="typeName">The field's type, as C# text.</param>
    /// <param name="accessModifier">The field's accessibility.</param>
    /// <returns>The field builder.</returns>
    public RawFieldBuilder DefineField(string name, string typeName, AccessModifier accessModifier)
    {
        var fb = new RawFieldBuilder(name, typeName, accessModifier);
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
    /// Declares a public auto-property whose type is named by text — for a type the
    /// generator cannot name as <c>T</c>, above all one discovered from the consumer's
    /// compilation. Implementing a discovered interface needs this, since the property
    /// types come from the interface rather than from the generator.
    /// </summary>
    /// <param name="name">The property's name.</param>
    /// <param name="typeName">The property's type, as C# text. Parsed, so a malformed name is rejected.</param>
    /// <returns>The property builder.</returns>
    public RawPropertyBuilder DefineProperty(string name, string typeName)
        => DefineProperty(name, typeName, AccessModifier.Public);

    /// <summary>Declares an auto-property whose type is named by text.</summary>
    /// <param name="name">The property's name.</param>
    /// <param name="typeName">The property's type, as C# text.</param>
    /// <param name="accessModifier">The property's accessibility.</param>
    /// <returns>The property builder.</returns>
    public RawPropertyBuilder DefineProperty(string name, string typeName, AccessModifier accessModifier)
    {
        var pb = new RawPropertyBuilder(name, typeName, accessModifier);
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

    /// <summary>
    /// Declares an operator returning <typeparamref name="TReturn"/>, e.g.
    /// <c>DefineOperator&lt;bool&gt;(OperatorKind.Equality)</c>. Operators are always
    /// <c>public static</c>, so there is nothing to configure.
    /// </summary>
    /// <typeparam name="TReturn">The operator's result type. Comparison operators return <c>bool</c>.</typeparam>
    /// <param name="kind">Which operator to declare.</param>
    /// <returns>The operator builder.</returns>
    public OperatorBuilder<TReturn> DefineOperator<TReturn>(OperatorKind kind)
        => AddOperator(new OperatorBuilder<TReturn>(kind));

    /// <summary>
    /// Declares an operator whose result type is named by text — for one returning the
    /// type being generated, which no type argument can name.
    /// </summary>
    /// <param name="kind">Which operator to declare.</param>
    /// <param name="resultTypeName">The result type, as C# text.</param>
    /// <returns>The operator builder.</returns>
    public OperatorBuilder DefineOperator(OperatorKind kind, string resultTypeName)
        => AddOperator(new OperatorBuilder(kind, SyntaxParse.TypeName(resultTypeName)));

    /// <summary>
    /// Declares a conversion to <typeparamref name="TTarget"/>:
    /// <c>public static implicit operator int(OrderId value)</c>.
    /// </summary>
    /// <typeparam name="TTarget">The type converted to.</typeparam>
    /// <param name="kind">Whether the conversion is implicit or explicit.</param>
    /// <returns>The operator builder.</returns>
    public OperatorBuilder<TTarget> DefineConversion<TTarget>(ConversionKind kind)
        => AddOperator(new OperatorBuilder<TTarget>(kind));

    /// <summary>
    /// Declares a conversion to a type named by text — for a conversion <em>to</em> the
    /// type being generated, whose name no type argument can supply.
    /// </summary>
    /// <param name="kind">Whether the conversion is implicit or explicit.</param>
    /// <param name="targetTypeName">The type converted to, as C# text.</param>
    /// <returns>The operator builder.</returns>
    public OperatorBuilder DefineConversion(ConversionKind kind, string targetTypeName)
        => AddOperator(new OperatorBuilder(kind, SyntaxParse.TypeName(targetTypeName)));

    private TOperator AddOperator<TOperator>(TOperator @operator)
        where TOperator : IOperatorMember
        => _operators.Add(@operator);

    /// <summary>Declares a public <c>void</c> method with an empty body.</summary>
    public MethodBuilder DefineMethod(string name)
        => DefineMethod(name, AccessModifier.Public);

    /// <summary>Declares a <c>void</c> method with an empty body.</summary>
    public MethodBuilder DefineMethod(string name, AccessModifier accessModifier)
        => AddMethod(MethodBuilder.Action(name, accessModifier));

    /// <summary>
    /// Declares a public method returning <typeparamref name="TReturn"/>. A
    /// value-returning method needs a body — see <c>Return</c>,
    /// <c>AsExpressionBody</c>, or <c>AddStatement</c>.
    /// </summary>
    public MethodBuilder<TReturn> DefineMethod<TReturn>(string name)
        => DefineMethod<TReturn>(name, AccessModifier.Public);

    /// <summary>
    /// Declares a method returning <typeparamref name="TReturn"/>. A value-returning
    /// method needs a body — see <c>Return</c>, <c>AsExpressionBody</c>, or
    /// <c>AddStatement</c>.
    /// </summary>
    public MethodBuilder<TReturn> DefineMethod<TReturn>(string name, AccessModifier accessModifier)
        => AddMethod(MethodBuilder<TReturn>.Returning(name, accessModifier));

    private TMethod AddMethod<TMethod>(TMethod method) where TMethod : IMethodMember
    {
        // The method learns its declaring type here so a callable handle can carry it,
        // which is what lets a call check its receiver.
        method.DeclaringType = this;
        _methods.Add(method);
        return method;
    }

    #endregion

    #region Nested types

    // Nested types share their declaring type's file: they are not files themselves, so
    // they never carry usings or formatting of their own.

    /// <summary>Declares a class nested inside this type.</summary>
    public ClassBuilder DefineClass(string name) => AddNested(new ClassBuilder(File, name, this));

    /// <summary>Declares a struct nested inside this type.</summary>
    public StructBuilder DefineStruct(string name) => AddNested(new StructBuilder(File, name, this));

    /// <summary>Declares an enum nested inside this type.</summary>
    public EnumBuilder DefineEnum(string name) => AddNested(new EnumBuilder(File, name, this));

    /// <summary>Declares a positional record nested inside this type.</summary>
    public RecordBuilder DefineRecord(string name) => AddNested(new RecordBuilder(File, name, this));

    /// <summary>Declares an interface nested inside this type.</summary>
    public InterfaceBuilder DefineInterface(string name) => AddNested(new InterfaceBuilder(File, name, this));

    /// <summary>Declares a <c>void</c>-returning delegate nested inside this type.</summary>
    public DelegateBuilder DefineDelegate(string name)
        => AddNested(new DelegateBuilder(File, name, PredefinedType(Token(SyntaxKind.VoidKeyword)), this));

    /// <summary>Declares a nested delegate returning <typeparamref name="TReturn"/>.</summary>
    public DelegateBuilder DefineDelegate<TReturn>(string name)
        => AddNested(new DelegateBuilder(File, name, TypeNameBuilder.New<TReturn>().BuildTypeSyntax(), this));

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

    /// <summary>
    /// Whether this type is a static class, which cannot declare operators (CS0715) or
    /// any instance member. Structs are never static.
    /// </summary>
    private protected virtual bool IsStaticType => false;

    /// <summary>Which kind of type this is, for the member rules that differ by kind.</summary>
    private protected virtual TypeKind Kind => TypeKind.Class;

    /// <summary>
    /// Whether this is a <c>readonly struct</c>, whose instance fields must be readonly
    /// and whose auto-properties must not be settable.
    /// </summary>
    private protected virtual bool IsReadonlyType => false;

    /// <summary>Whether this type is <c>partial</c>, which a partial member requires.</summary>
    private protected virtual bool IsPartialType => false;

    // Member group order: fields, constructors, events, properties, methods, nested
    // types;
    // within each group, least protected first, then alphabetical.
    private protected SyntaxList<MemberDeclarationSyntax> BuildMembers()
    {
        // Everything a member is forbidden by the type it sits in, and by the other
        // members beside it. Only the type knows both halves, so it all lives here --
        // and it lives in one place because Review 3 found six of these missing at once,
        // which said the gap was the concept rather than the individual cases.
        new MemberRules(Name, Kind, IsStaticType, IsReadonlyType, IsPartialType, AllowsAbstractMembers)
            .Validate(_fields, _constructors, _events, _properties, _methods, _nestedTypes, _interfaces.Count > 0);

        // The cross-member operator rules -- pairing, twinning, duplicates, and the
        // static-class refusal -- live on their own set, shared with every builder that
        // can carry operators.
        _operators.Validate(Name, IsStaticType);

        var members = new List<MemberDeclarationSyntax>();
        AddMemberGroup(members, _fields);
        AddMemberGroup(members, _constructors);
        AddMemberGroup(members, _events);
        AddMemberGroup(members, _properties);
        _operators.AppendMembers(members);
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
        where TMember : INamedBuilder, IAccessModifier, IMemberSyntaxBuilder
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
    private protected TypeBuilder(SourceFile file, string name, TypeDeclarationBuilder? declaringType) : base(file, name, declaringType)
    {
    }

    /// <summary>Sets the type's accessibility. Public by default.</summary>
    public TSelf WithAccessModifier(AccessModifier accessModifier)
    {
        AccessModifier = accessModifier;
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

    /// <summary>
    /// Adds an implemented interface that is being generated alongside — the interface's
    /// builder is the reference, so the name is spelled once.
    /// </summary>
    public TSelf WithInterface(InterfaceBuilder @interface)
    {
        AddInterface(TypeNameBuilder.For(@interface).BuildTypeSyntax());
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
