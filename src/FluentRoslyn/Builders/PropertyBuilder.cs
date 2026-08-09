using System;
using System.Collections.Generic;
using FluentRoslyn.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace FluentRoslyn.Builders;

/// <summary>
/// The fluent surface shared by every property builder, whatever its type came from.
/// <typeparamref name="TSelf"/> is the concrete kind, so chaining yields it — the same
/// CRTP shape as <see cref="FieldBuilderBase{TSelf}"/>.
/// </summary>
/// <typeparam name="TSelf">The concrete property builder type.</typeparam>
public abstract class PropertyBuilderBase<TSelf>(
    TypeNameBuilder typeName,
    string name,
    AccessModifier accessModifier)
    : PropertyBuilder(typeName, name, accessModifier)
    where TSelf : PropertyBuilderBase<TSelf>
{
    /// <summary>This builder as its concrete type, for fluent returns.</summary>
    private protected TSelf Self => (TSelf)this;

    #region FluentMethods

    /// <summary>Marks the property <c>static</c>.</summary>
    public TSelf Static() => Self.With(() => IsStatic = true);

    /// <summary>Sets the property's accessibility. Public by default.</summary>
    public TSelf WithAccessModifier(AccessModifier accessModifier) => Self.With(() => AccessModifier = accessModifier);

    /// <summary>Documents the property with an XML <c>&lt;summary&gt;</c>.</summary>
    public TSelf WithSummary(string text) => Self.With(() => Docs.SetSummary(text));

    /// <summary>
    /// Marks the property <c>required</c> (C# 11): callers must set it in an object
    /// initializer. Cannot combine with <c>static</c>, and needs a settable accessor.
    /// </summary>
    public TSelf Required() => Self.With(() => IsRequired = true);

    /// <summary>Adds an attribute, e.g. <c>WithAttribute("JsonIgnore")</c>.</summary>
    public TSelf WithAttribute(string attribute) => Self.With(() => Attributes.Add(SyntaxAttributes.AttributeList(attribute)));

    /// <summary>Emits a get-only auto-property (<c>{ get; }</c>) by dropping the setter.</summary>
    public TSelf GetOnly() => Self.With(() => HasSet = false);

    /// <summary>Emits the setter as an init accessor: <c>{ get; init; }</c>.</summary>
    public TSelf InitOnly() => Self.With(() =>
    {
        HasSet = true;
        SetterIsInit = true;
    });

    /// <summary>
    /// Restricts the setter's access, e.g. <c>{ get; private set; }</c>. The modifier
    /// must be strictly more restrictive than the property's own.
    /// </summary>
    public TSelf WithSetterAccessModifier(AccessModifier accessModifier)
        => Self.With(() => SetterAccessModifier = accessModifier);

    /// <summary>
    /// Sets a default value from a raw C# expression, e.g. <c>"new()"</c> or
    /// <c>"TimeSpan.Zero"</c>. The escape hatch for values a literal cannot express.
    /// </summary>
    public TSelf WithInitializerExpression(string expression)
        => Self.With(() => Initializer = ParseExpr(expression));

    /// <summary>
    /// Emits an expression-bodied property: <c>public int Count =&gt; _count;</c>.
    /// Replaces the accessor list entirely.
    /// </summary>
    public TSelf AsExpressionBody(string expression)
        => Self.With(() =>
        {
            IsAutoProperty = false;
            ExpressionBody = ParseExpr(expression);
        });

    /// <summary>
    /// Gives the getter an expression body: <c>get =&gt; expression;</c>. Turns the
    /// property into a non-auto property with expression-bodied accessors.
    /// </summary>
    public TSelf WithGetterExpression(string expression)
        => Self.With(() =>
        {
            IsAutoProperty = false;
            GetterExpression = ParseExpr(expression);
        });

    /// <summary>
    /// Gives the setter an expression body: <c>set =&gt; expression;</c>. The value
    /// being assigned is available as <c>value</c>.
    /// </summary>
    public TSelf WithSetterExpression(string expression)
        => Self.With(() =>
        {
            IsAutoProperty = false;
            HasSet = true;
            SetterExpression = ParseExpr(expression);
        });

    /// <summary>
    /// Gives the getter a statement body: <c>get { statements }</c>. The body must
    /// return on all paths.
    /// </summary>
    public TSelf WithGetterBody(params string[] statements)
        => Self.With(() =>
        {
            IsAutoProperty = false;
            GetterStatements = ParseStatements(statements);
        });

    /// <summary>
    /// Gives the setter a statement body: <c>set { statements }</c>. The value being
    /// assigned is available as <c>value</c>.
    /// </summary>
    public TSelf WithSetterBody(params string[] statements)
        => Self.With(() =>
        {
            IsAutoProperty = false;
            HasSet = true;
            SetterStatements = ParseStatements(statements);
        });

    #endregion
}

/// <summary>
/// Builds a property declaration of type <typeparamref name="T"/>. Obtained from
/// <c>DefineProperty&lt;T&gt;</c> on a type builder. Supports auto-properties,
/// expression bodies, and statement-bodied accessors.
/// </summary>
/// <typeparam name="T">The property's type.</typeparam>
public class PropertyBuilder<T> : PropertyBuilderBase<PropertyBuilder<T>>, IReference<T>, IReferenceInfo
{
    /// <summary>
    /// Creates a standalone property builder. Prefer <c>DefineProperty&lt;T&gt;</c> on a
    /// type builder, which also attaches the property to that type.
    /// </summary>
    /// <param name="name">The property's name.</param>
    /// <param name="accessModifier">The property's accessibility.</param>
    public PropertyBuilder(string name, AccessModifier accessModifier)
        : base(TypeNameBuilder.New<T>(), name, accessModifier)
    {
    }

    ReferenceKind IReferenceInfo.Kind => ReferenceKind.Member;

    bool IReferenceInfo.IsStaticMember => IsStatic;

    /// <summary>
    /// Sets a default value: <c>{ get; set; } = value;</c>. Supports the primitive
    /// types with a literal form; use
    /// <see cref="PropertyBuilderBase{TSelf}.WithInitializerExpression"/> for enums,
    /// object construction, or any other expression.
    /// </summary>
    public PropertyBuilder<T> WithInitializer(T value) => this.With(() => Initializer = SyntaxLiterals.Expression(value));

    /// <summary>
    /// Gives the getter a typed statement body:
    /// <c>WithGetter(g =&gt; g.Return(backingField))</c>. The scope carries the same
    /// statement API as a method body, plus a <c>Return</c> typed to this property.
    /// </summary>
    public PropertyBuilder<T> WithGetter(Action<GetterBody<T>> body)
        => this.With(() =>
        {
            if (body is null) throw new ArgumentNullException(nameof(body));

            var scope = new GetterBody<T>(Name, IsStatic);
            body(scope);

            IsAutoProperty = false;
            GetterStatements = scope.BuiltStatements;
        });

    /// <summary>
    /// Gives the setter a typed statement body:
    /// <c>WithSetter(s =&gt; s.Assign(backingField, s.Value))</c>. The incoming value is
    /// <c>s.Value</c>, typed to this property.
    /// </summary>
    public PropertyBuilder<T> WithSetter(Action<SetterBody<T>> body)
        => this.With(() =>
        {
            if (body is null) throw new ArgumentNullException(nameof(body));

            var scope = new SetterBody<T>(Name, IsStatic);
            body(scope);

            IsAutoProperty = false;
            HasSet = true;
            SetterStatements = scope.BuiltStatements;
        });
}

/// <summary>
/// Builds a property whose type is named by text rather than by a type argument.
/// Obtained from <c>DefineProperty(name, typeName)</c> on a type builder.
/// </summary>
/// <remarks>
/// The escape hatch for a type the generator cannot name as <c>T</c> — above all a
/// consumer's own type, which exists only as an <c>ISymbol</c> at generation time.
/// Implementing a discovered interface needs exactly this, since the property types come
/// from the interface rather than from the generator. As with
/// <see cref="RawFieldBuilder"/> the cost is stated: this is an
/// <see cref="IRawReference"/> and not an <see cref="IReference{T}"/>, so the typed
/// accessor scopes (<c>WithGetter</c>/<c>WithSetter</c>) and the typed initializer are
/// unavailable — accessor bodies go through <c>WithGetterBody</c>/<c>WithSetterBody</c>.
/// </remarks>
public sealed class RawPropertyBuilder : PropertyBuilderBase<RawPropertyBuilder>, IRawReference, IReferenceInfo, IRawTypeInfo
{
    internal RawPropertyBuilder(string name, string typeName, AccessModifier accessModifier)
        : base(TypeNameBuilder.ForRawName(typeName), name, accessModifier)
    {
    }

    ReferenceKind IReferenceInfo.Kind => ReferenceKind.Member;

    bool IReferenceInfo.IsStaticMember => IsStatic;

    string IRawTypeInfo.TypeText => TypeName.ToString();

    /// <summary>
    /// Gives the getter a built statement body:
    /// <c>WithGetter(g =&gt; g.Return(inner.MemberRaw("Count")))</c>. The counterpart of
    /// the typed <c>WithGetter</c>, with an unchecked <c>Return</c> — without it a
    /// raw-typed property's body would fall back to text, which is the seam this whole
    /// tier exists to close.
    /// </summary>
    /// <param name="body">Configures the getter's statements.</param>
    /// <returns>This builder, so the chain continues.</returns>
    public RawPropertyBuilder WithGetter(Action<RawGetterBody> body)
        => this.With(() =>
        {
            if (body is null) throw new ArgumentNullException(nameof(body));

            var scope = new RawGetterBody(Name, IsStatic);
            body(scope);

            IsAutoProperty = false;
            GetterStatements = scope.BuiltStatements;
        });

    /// <summary>
    /// Gives the setter a built statement body:
    /// <c>WithSetter(s =&gt; s.AssignRaw(backingField, s.Value))</c>. The incoming value
    /// carries this property's declared type text, so that assignment is still checked.
    /// </summary>
    /// <param name="body">Configures the setter's statements.</param>
    /// <returns>This builder, so the chain continues.</returns>
    public RawPropertyBuilder WithSetter(Action<RawSetterBody> body)
        => this.With(() =>
        {
            if (body is null) throw new ArgumentNullException(nameof(body));

            var scope = new RawSetterBody(Name, IsStatic, TypeName.ToString());
            body(scope);

            IsAutoProperty = false;
            HasSet = true;
            SetterStatements = scope.BuiltStatements;
        });
}

/// <summary>
/// The non-generic base of the property builders, carrying the state and the emission
/// logic that do not depend on how the property's type was supplied.
/// </summary>
public abstract class PropertyBuilder(
    TypeNameBuilder typeName,
    string name,
    AccessModifier accessModifier)
    : NamedBuilder(name, Identifiers.Validate), IAccessModifier, IMemberSyntaxBuilder
{
    // C#'s rule for accessor modifiers: the modifier must be strictly more restrictive
    // than the property's own accessibility. The valid (property -> accessor) pairs do
    // not follow AccessabilityLevel's ordering (protected internal is broader than
    // protected, not narrower), so the allowed sets are enumerated explicitly.
    private static readonly Dictionary<AccessModifier, HashSet<AccessModifier>> AllowedAccessorModifiers = new()
    {
        [AccessModifier.Public] = [AccessModifier.ProtectedInternal, AccessModifier.Internal, AccessModifier.Protected, AccessModifier.PrivateProtected, AccessModifier.Private],
        [AccessModifier.ProtectedInternal] = [AccessModifier.Internal, AccessModifier.Protected, AccessModifier.PrivateProtected, AccessModifier.Private],
        [AccessModifier.Internal] = [AccessModifier.PrivateProtected, AccessModifier.Private],
        [AccessModifier.Protected] = [AccessModifier.PrivateProtected, AccessModifier.Private],
        [AccessModifier.PrivateProtected] = [AccessModifier.Private],
        [AccessModifier.Private] = [],
    };

    /// <summary>Whether the property is <c>static</c>.</summary>
    public bool IsStatic { get; set; }

    /// <summary>
    /// Whether the property is <c>required</c>. Cannot combine with static, and needs a
    /// settable accessor.
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// Whether the property declares a getter. True by default; an auto-property must
    /// have one.
    /// </summary>
    public bool HasGet { get; set; } = true;

    /// <summary>Whether the property declares a setter. True by default.</summary>
    public bool HasSet { get; set; } = true;

    /// <summary>When true, the setter is emitted as <c>init</c> rather than <c>set</c>.</summary>
    public bool SetterIsInit { get; set; }

    /// <summary>
    /// A more restrictive access modifier on the setter (e.g. <c>private set</c>), or
    /// null to inherit the property's own. Must be strictly more restrictive.
    /// </summary>
    public AccessModifier? SetterAccessModifier { get; set; }

    /// <summary>
    /// Whether the property is an auto-property (<c>{ get; set; }</c>). Set to false
    /// automatically when an expression or statement body is supplied.
    /// </summary>
    public bool IsAutoProperty { get; set; } = true;

    /// <summary>The property's accessibility. Public by default.</summary>
    public AccessModifier AccessModifier { get; set; } = accessModifier;

    internal List<AttributeListSyntax> Attributes { get; } = [];

    internal DocComment Docs { get; } = new();

    /// <summary>The property's declared type, for comparing two raw-typed references.</summary>
    internal TypeNameBuilder TypeName => typeName;

    // The property's default-value expression, or null when it has no initializer.
    internal ExpressionSyntax? Initializer { get; set; }

    // Whole-property expression body (=> expr), or null.
    internal ExpressionSyntax? ExpressionBody { get; set; }

    // Expression bodies for the individual accessors (get => expr / set => expr), or null.
    internal ExpressionSyntax? GetterExpression { get; set; }

    internal ExpressionSyntax? SetterExpression { get; set; }

    // Statement bodies for the individual accessors (get { ... } / set { ... }), or null.
    internal List<StatementSyntax>? GetterStatements { get; set; }

    internal List<StatementSyntax>? SetterStatements { get; set; }

    internal PropertyDeclarationSyntax BuildProperty()
    {
        // init accessors run during object initialization, which a static property has no
        // part in, so `static { get; init; }` does not compile.
        if (IsStatic && SetterIsInit)
            throw new InvalidOperationException($"Property '{Name}': a static property cannot have an init accessor.");

        if (IsRequired)
        {
            // A required member is assigned by the caller during initialization, so a
            // static or unsettable property can never satisfy it.
            if (IsStatic)
                throw new InvalidOperationException($"Property '{Name}': a static property cannot be required.");
            if (!HasSet)
                throw new InvalidOperationException(
                    $"Property '{Name}': a required property needs a set or init accessor.");
        }

        var property = PropertyDeclaration(typeName.BuildTypeSyntax(), Identifier(Name))
            .WithAttributeLists(SyntaxAttributes.Lists(Attributes))
            .WithModifiers(SyntaxFormatting.Modifiers(AccessModifier, IsStatic, isRequired: IsRequired));

        var hasGetterBody = GetterExpression is not null || GetterStatements is not null;
        var hasSetterBody = SetterExpression is not null || SetterStatements is not null;

        // 1. Whole-property expression body: public int Count => _count;
        if (ExpressionBody is not null)
        {
            GuardNoInitializer("an expression-bodied property");

            return SyntaxBodies.ExpressionBodied(
                property, ExpressionBody, hasGetterBody || hasSetterBody, $"Property '{Name}'");
        }

        // 2. Accessor bodies, expression or statement: { get => a; set { ...; } }.
        // Unlike auto-properties, a bodied property may be write-only ({ set => ...; }).
        if (hasGetterBody || hasSetterBody)
        {
            GuardNoInitializer("a property with accessor bodies");

            var bodied = new List<AccessorDeclarationSyntax>();

            if (hasGetterBody)
                bodied.Add(BuildAccessor(
                    SyntaxKind.GetAccessorDeclaration, GetterExpression, GetterStatements, $"Getter of '{Name}'"));

            if (hasSetterBody)
            {
                ValidateSetterAccessModifier();
                bodied.Add(BuildAccessor(
                    SetterKind(), SetterExpression, SetterStatements, $"Setter of '{Name}'", SetterAccessModifier));
            }
            else if (SetterAccessModifier is not null)
            {
                throw new InvalidOperationException($"Property '{Name}' has a setter access modifier but no setter.");
            }

            return property.WithAccessorList(AccessorList(List(bodied)));
        }

        // 3. Non-auto requested but no body supplied — a caller error now that both
        // expression and statement bodies are expressible.
        if (!IsAutoProperty)
            throw new InvalidOperationException(
                $"Property '{Name}' is marked non-auto but has no body. Use AsExpressionBody, " +
                "WithGetterExpression/WithSetterExpression, or WithGetterBody/WithSetterBody.");

        // 4. Auto-property: { get; set; }
        if (!HasGet)
            throw new InvalidOperationException($"Auto-property '{Name}' must have a getter.");

        var accessors = new List<AccessorDeclarationSyntax> { Accessor(SyntaxKind.GetAccessorDeclaration) };
        if (HasSet)
        {
            ValidateSetterAccessModifier();
            accessors.Add(Accessor(SetterKind(), SetterAccessModifier));
        }
        else if (SetterAccessModifier is not null)
        {
            throw new InvalidOperationException($"Property '{Name}' has a setter access modifier but no setter.");
        }

        var declaration = property.WithAccessorList(AccessorList(List(accessors)));

        // An initialized property needs a closing semicolon after the accessor list.
        return Initializer is null
            ? declaration
            : declaration
                .WithInitializer(EqualsValueClause(Initializer))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
    }

    // Both emission paths (as a type member, and standalone via ToString) route through
    // here so docs are attached exactly once, wherever the property is built from.
    private PropertyDeclarationSyntax BuildDocumentedProperty()
    {
        var property = BuildProperty();
        return Docs.IsEmpty ? property : property.WithLeadingTrivia(Docs.Build());
    }

    MemberDeclarationSyntax IMemberSyntaxBuilder.BuildMember() => BuildDocumentedProperty();

    internal override SyntaxNode BuildSyntax() => BuildDocumentedProperty();

    private SyntaxKind SetterKind()
        => SetterIsInit ? SyntaxKind.InitAccessorDeclaration : SyntaxKind.SetAccessorDeclaration;

    private void GuardNoInitializer(string context)
    {
        if (Initializer is not null)
            throw new InvalidOperationException($"Property '{Name}': {context} cannot have an initializer.");
    }

    private static AccessorDeclarationSyntax Accessor(SyntaxKind kind, AccessModifier? access = null)
        => ApplyAccess(AccessorDeclaration(kind).WithSemicolonToken(Token(SyntaxKind.SemicolonToken)), access);

    // Builds an accessor from whichever body form was supplied: an arrow expression, a
    // statement block, or (when both are null) it is a caller bug — the branch guards
    // ensure at least one is set before this is reached.
    private static AccessorDeclarationSyntax BuildAccessor(
        SyntaxKind kind,
        ExpressionSyntax? expression,
        List<StatementSyntax>? statements,
        string context,
        AccessModifier? access = null)
    {
        var accessor = expression is not null
            ? SyntaxBodies.ExpressionBodied(
                AccessorDeclaration(kind), expression, statements is not null, context)
            : AccessorDeclaration(kind).WithBody(Block(statements!));

        return ApplyAccess(accessor, access);
    }

    private static AccessorDeclarationSyntax ApplyAccess(AccessorDeclarationSyntax accessor, AccessModifier? access)
        => access is null ? accessor : accessor.WithModifiers(SyntaxFormatting.Modifiers(access));

    private protected static ExpressionSyntax ParseExpr(string expression)
        => SyntaxParse.Expression(expression);

    private protected static List<StatementSyntax> ParseStatements(string[] statements)
    {
        if (statements is null) throw new ArgumentNullException(nameof(statements));

        var parsed = new List<StatementSyntax>(statements.Length);
        foreach (var statement in statements)
            parsed.Add(SyntaxBodies.Statement(statement));

        return parsed;
    }

    private protected void ValidateSetterAccessModifier()
    {
        if (SetterAccessModifier is null)
            return;

        if (!AllowedAccessorModifiers.TryGetValue(AccessModifier, out var allowed) || !allowed.Contains(SetterAccessModifier))
            throw new InvalidOperationException(
                $"Property '{Name}': setter access modifier '{SetterAccessModifier}' must be strictly more restrictive than the property's '{AccessModifier}'.");
    }
}
