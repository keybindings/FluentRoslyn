using System;
using System.Collections.Generic;
using FluentRoslyn.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace FluentRoslyn.Builders;

/// <summary>
/// Builds a property declaration of type <typeparamref name="T"/>. Obtained from
/// <c>DefineProperty&lt;T&gt;</c> on a type builder. Supports auto-properties,
/// expression bodies, and statement-bodied accessors.
/// </summary>
/// <typeparam name="T">The property's type.</typeparam>
public class PropertyBuilder<T> : PropertyBuilder
{
    private readonly TypeNameBuilder _typeName = TypeNameBuilder.New<T>();

    /// <summary>
    /// Creates a standalone property builder. Prefer <c>DefineProperty&lt;T&gt;</c> on a
    /// type builder, which also attaches the property to that type.
    /// </summary>
    public PropertyBuilder(string name, AccessModifier accessModifier) : base(name, accessModifier)
    {
    }

    #region FluentMethods

    /// <summary>Marks the property <c>static</c>.</summary>
    public PropertyBuilder<T> Static() => this.With(() => IsStatic = true);

    /// <summary>Sets the property's accessibility. Public by default.</summary>
    public PropertyBuilder<T> WithAccessModifier(AccessModifier accessModifier) => this.With(() => AccessModifier = accessModifier);

    /// <summary>Documents the property with an XML <c>&lt;summary&gt;</c>.</summary>
    public PropertyBuilder<T> WithSummary(string text) => this.With(() => Docs.SetSummary(text));

    /// <summary>
    /// Marks the property <c>required</c> (C# 11): callers must set it in an object
    /// initializer. Cannot combine with <c>static</c>, and needs a settable accessor.
    /// </summary>
    public PropertyBuilder<T> Required() => this.With(() => IsRequired = true);

    /// <summary>Adds an attribute, e.g. <c>WithAttribute("JsonIgnore")</c>.</summary>
    public PropertyBuilder<T> WithAttribute(string attribute) => this.With(() => Attributes.Add(SyntaxAttributes.AttributeList(attribute)));

    /// <summary>Emits a get-only auto-property (<c>{ get; }</c>) by dropping the setter.</summary>
    public PropertyBuilder<T> GetOnly() => this.With(() => HasSet = false);

    /// <summary>Emits the setter as an init accessor: <c>{ get; init; }</c>.</summary>
    public PropertyBuilder<T> InitOnly() => this.With(() =>
    {
        HasSet = true;
        SetterIsInit = true;
    });

    /// <summary>
    /// Restricts the setter's access, e.g. <c>{ get; private set; }</c>. The modifier
    /// must be strictly more restrictive than the property's own.
    /// </summary>
    public PropertyBuilder<T> WithSetterAccessModifier(AccessModifier accessModifier)
        => this.With(() => SetterAccessModifier = accessModifier);

    /// <summary>
    /// Sets a default value: <c>{ get; set; } = value;</c>. Supports the primitive
    /// types with a literal form; use <see cref="WithInitializerExpression"/> for
    /// enums, object construction, or any other expression.
    /// </summary>
    public PropertyBuilder<T> WithInitializer(T value) => this.With(() => Initializer = SyntaxLiterals.Expression(value));

    /// <summary>
    /// Sets a default value from a raw C# expression, e.g. <c>"new()"</c> or
    /// <c>"TimeSpan.Zero"</c>. The escape hatch for values a literal cannot express.
    /// </summary>
    public PropertyBuilder<T> WithInitializerExpression(string expression)
        => this.With(() => Initializer = ParseExpr(expression));

    /// <summary>
    /// Emits an expression-bodied property: <c>public int Count =&gt; _count;</c>.
    /// Replaces the accessor list entirely.
    /// </summary>
    public PropertyBuilder<T> AsExpressionBody(string expression)
        => this.With(() =>
        {
            IsAutoProperty = false;
            ExpressionBody = ParseExpr(expression);
        });

    /// <summary>
    /// Gives the getter an expression body: <c>get =&gt; expression;</c>. Turns the
    /// property into a non-auto property with expression-bodied accessors.
    /// </summary>
    public PropertyBuilder<T> WithGetterExpression(string expression)
        => this.With(() =>
        {
            IsAutoProperty = false;
            GetterExpression = ParseExpr(expression);
        });

    /// <summary>
    /// Gives the setter an expression body: <c>set =&gt; expression;</c>. The value
    /// being assigned is available as <c>value</c>.
    /// </summary>
    public PropertyBuilder<T> WithSetterExpression(string expression)
        => this.With(() =>
        {
            IsAutoProperty = false;
            HasSet = true;
            SetterExpression = ParseExpr(expression);
        });

    /// <summary>
    /// Gives the getter a statement body: <c>get { statements }</c>. The body must
    /// return on all paths.
    /// </summary>
    public PropertyBuilder<T> WithGetterBody(params string[] statements)
        => this.With(() =>
        {
            IsAutoProperty = false;
            GetterStatements = ParseStatements(statements);
        });

    /// <summary>
    /// Gives the setter a statement body: <c>set { statements }</c>. The value being
    /// assigned is available as <c>value</c>.
    /// </summary>
    public PropertyBuilder<T> WithSetterBody(params string[] statements)
        => this.With(() =>
        {
            IsAutoProperty = false;
            HasSet = true;
            SetterStatements = ParseStatements(statements);
        });

    #endregion

    internal override PropertyDeclarationSyntax BuildProperty()
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

        var property = PropertyDeclaration(_typeName.BuildTypeSyntax(), Identifier(Name))
            .WithAttributeLists(SyntaxAttributes.Lists(Attributes))
            .WithModifiers(SyntaxFormatting.Modifiers(AccessModifier, IsStatic, isRequired: IsRequired));

        var hasGetterBody = GetterExpression is not null || GetterStatements is not null;
        var hasSetterBody = SetterExpression is not null || SetterStatements is not null;

        // 1. Whole-property expression body: public int Count => _count;
        if (ExpressionBody is not null)
        {
            GuardNoInitializer("an expression-bodied property");
            if (hasGetterBody || hasSetterBody)
                throw new InvalidOperationException(
                    $"Property '{Name}' cannot combine a whole-property expression body with accessor bodies.");

            return property
                .WithExpressionBody(ArrowExpressionClause(ExpressionBody))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
        }

        // 2. Accessor bodies, expression or statement: { get => a; set { ...; } }.
        // Unlike auto-properties, a bodied property may be write-only ({ set => ...; }).
        if (hasGetterBody || hasSetterBody)
        {
            GuardNoInitializer("a property with accessor bodies");

            var bodied = new List<AccessorDeclarationSyntax>();

            if (hasGetterBody)
                bodied.Add(BuildAccessor(SyntaxKind.GetAccessorDeclaration, GetterExpression, GetterStatements));

            if (hasSetterBody)
            {
                ValidateSetterAccessModifier();
                bodied.Add(BuildAccessor(SetterKind(), SetterExpression, SetterStatements, SetterAccessModifier));
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
        AccessModifier? access = null)
    {
        if (expression is not null && statements is not null)
            throw new InvalidOperationException("An accessor cannot have both an expression body and a statement body.");

        var accessor = expression is not null
            ? AccessorDeclaration(kind)
                .WithExpressionBody(ArrowExpressionClause(expression))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken))
            : AccessorDeclaration(kind).WithBody(Block(statements!));

        return ApplyAccess(accessor, access);
    }

    private static AccessorDeclarationSyntax ApplyAccess(AccessorDeclarationSyntax accessor, AccessModifier? access)
        => access is null ? accessor : accessor.WithModifiers(SyntaxFormatting.Modifiers(access));

    private static ExpressionSyntax ParseExpr(string expression)
        => SyntaxParse.Expression(expression);

    private static List<StatementSyntax> ParseStatements(string[] statements)
    {
        if (statements is null) throw new ArgumentNullException(nameof(statements));

        var parsed = new List<StatementSyntax>(statements.Length);
        foreach (var statement in statements)
            parsed.Add(SyntaxBodies.Statement(statement));

        return parsed;
    }
}

/// <summary>
/// The non-generic base of <see cref="PropertyBuilder{T}"/>, carrying the state that
/// does not depend on the property's type.
/// </summary>
public abstract class PropertyBuilder(string name, AccessModifier accessModifier)
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

    internal abstract PropertyDeclarationSyntax BuildProperty();

    // Both emission paths (as a type member, and standalone via ToString) route through
    // here so docs are attached exactly once, wherever the property is built from.
    private PropertyDeclarationSyntax BuildDocumentedProperty()
    {
        var property = BuildProperty();
        return Docs.IsEmpty ? property : property.WithLeadingTrivia(Docs.Build());
    }

    MemberDeclarationSyntax IMemberSyntaxBuilder.BuildMember() => BuildDocumentedProperty();

    internal override SyntaxNode BuildSyntax() => BuildDocumentedProperty();

    private protected void ValidateSetterAccessModifier()
    {
        if (SetterAccessModifier is null)
            return;

        if (!AllowedAccessorModifiers.TryGetValue(AccessModifier, out var allowed) || !allowed.Contains(SetterAccessModifier))
            throw new InvalidOperationException(
                $"Property '{Name}': setter access modifier '{SetterAccessModifier}' must be strictly more restrictive than the property's '{AccessModifier}'.");
    }
}
