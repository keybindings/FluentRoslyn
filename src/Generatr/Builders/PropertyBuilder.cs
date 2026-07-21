using System;
using System.Collections.Generic;
using Generatr.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Generatr.Builders;

public class PropertyBuilder<T> : PropertyBuilder
{
    private readonly TypeNameBuilder _typeName = TypeNameBuilder.New<T>();

    public PropertyBuilder(TypeBuilder declaringType, string name, AccessModifier accessModifier) : base(declaringType, name, accessModifier)
    {
    }

    #region FluentMethods

    public PropertyBuilder<T> Static() => With(() => IsStatic = true);

    public PropertyBuilder<T> WithAccessModifier(AccessModifier accessModifier) => With(() => AccessModifier = accessModifier);

    /// <summary>Adds an attribute, e.g. <c>WithAttribute("JsonIgnore")</c>.</summary>
    public PropertyBuilder<T> WithAttribute(string attribute) => With(() => Attributes.Add(SyntaxAttributes.Attribute(attribute)));

    /// <summary>Emits a get-only auto-property (<c>{ get; }</c>) by dropping the setter.</summary>
    public PropertyBuilder<T> GetOnly() => With(() => HasSet = false);

    /// <summary>Emits the setter as an init accessor: <c>{ get; init; }</c>.</summary>
    public PropertyBuilder<T> InitOnly() => With(() =>
    {
        HasSet = true;
        SetterIsInit = true;
    });

    /// <summary>
    /// Restricts the setter's access, e.g. <c>{ get; private set; }</c>. The modifier
    /// must be strictly more restrictive than the property's own.
    /// </summary>
    public PropertyBuilder<T> WithSetterAccessModifier(AccessModifier accessModifier)
        => With(() => SetterAccessModifier = accessModifier);

    /// <summary>
    /// Sets a default value: <c>{ get; set; } = value;</c>. Supports the primitive
    /// types with a literal form; use <see cref="WithInitializerExpression"/> for
    /// enums, object construction, or any other expression.
    /// </summary>
    public PropertyBuilder<T> WithInitializer(T value) => With(() => Initializer = SyntaxLiterals.Expression(value));

    /// <summary>
    /// Sets a default value from a raw C# expression, e.g. <c>"new()"</c> or
    /// <c>"TimeSpan.Zero"</c>. The escape hatch for values a literal cannot express.
    /// </summary>
    public PropertyBuilder<T> WithInitializerExpression(string expression)
        => With(() => Initializer = ParseExpr(expression));

    /// <summary>
    /// Emits an expression-bodied property: <c>public int Count =&gt; _count;</c>.
    /// Replaces the accessor list entirely.
    /// </summary>
    public PropertyBuilder<T> AsExpressionBody(string expression)
        => With(() =>
        {
            IsAutoProperty = false;
            ExpressionBody = ParseExpr(expression);
        });

    /// <summary>
    /// Gives the getter an expression body: <c>get =&gt; expression;</c>. Turns the
    /// property into a non-auto property with expression-bodied accessors.
    /// </summary>
    public PropertyBuilder<T> WithGetterExpression(string expression)
        => With(() =>
        {
            IsAutoProperty = false;
            GetterExpression = ParseExpr(expression);
        });

    /// <summary>
    /// Gives the setter an expression body: <c>set =&gt; expression;</c>. The value
    /// being assigned is available as <c>value</c>.
    /// </summary>
    public PropertyBuilder<T> WithSetterExpression(string expression)
        => With(() =>
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
        => With(() =>
        {
            IsAutoProperty = false;
            GetterStatements = ParseStatements(statements);
        });

    /// <summary>
    /// Gives the setter a statement body: <c>set { statements }</c>. The value being
    /// assigned is available as <c>value</c>.
    /// </summary>
    public PropertyBuilder<T> WithSetterBody(params string[] statements)
        => With(() =>
        {
            IsAutoProperty = false;
            HasSet = true;
            SetterStatements = ParseStatements(statements);
        });

    #endregion

    internal override PropertyDeclarationSyntax BuildProperty()
    {
        var property = PropertyDeclaration(_typeName.BuildTypeSyntax(), Identifier(Name))
            .WithAttributeLists(SyntaxAttributes.Lists(Attributes))
            .WithModifiers(SyntaxFormatting.Modifiers(AccessModifier, IsStatic));

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

        // 2. Accessor bodies, expression or statement: { get => a; set { ...; } }
        if (hasGetterBody || hasSetterBody)
        {
            GuardNoInitializer("a property with accessor bodies");
            if (!hasGetterBody)
                throw new InvalidOperationException(
                    $"Property '{Name}' with a bodied setter must also have a getter.");

            var bodied = new List<AccessorDeclarationSyntax>
            {
                BuildAccessor(SyntaxKind.GetAccessorDeclaration, GetterExpression, GetterStatements),
            };

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
        => ParseExpression(expression ?? throw new ArgumentNullException(nameof(expression)));

    private static List<StatementSyntax> ParseStatements(string[] statements)
    {
        if (statements is null) throw new ArgumentNullException(nameof(statements));

        var parsed = new List<StatementSyntax>(statements.Length);
        foreach (var statement in statements)
            parsed.Add(SyntaxBodies.Statement(statement));

        return parsed;
    }

    private PropertyBuilder<T> With(Action action)
    {
        action();
        return this;
    }
}

public abstract class PropertyBuilder(TypeBuilder declaringType, string name, AccessModifier accessModifier)
    : NamedBuilder(name, NameValidation), IAccessModifier, IMemberSyntaxBuilder
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

    public TypeBuilder DeclaringType { get; } = declaringType;

    public bool IsStatic { get; set; }

    public bool HasGet { get; set; } = true;

    public bool HasSet { get; set; } = true;

    // When true, the setter is emitted as `init` rather than `set`.
    public bool SetterIsInit { get; set; }

    // A more restrictive access modifier on the setter (e.g. `private set`), or null
    // to inherit the property's own modifier.
    public AccessModifier? SetterAccessModifier { get; set; }

    public bool IsAutoProperty { get; set; } = true;

    public AccessModifier AccessModifier { get; set; } = accessModifier;

    internal List<AttributeSyntax> Attributes { get; } = [];

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

    MemberDeclarationSyntax IMemberSyntaxBuilder.BuildMember() => BuildProperty();

    internal override SyntaxNode BuildSyntax() => BuildProperty();

    private protected void ValidateSetterAccessModifier()
    {
        if (SetterAccessModifier is null)
            return;

        if (!AllowedAccessorModifiers.TryGetValue(AccessModifier, out var allowed) || !allowed.Contains(SetterAccessModifier))
            throw new InvalidOperationException(
                $"Property '{Name}': setter access modifier '{SetterAccessModifier}' must be strictly more restrictive than the property's '{AccessModifier}'.");
    }

    private static void NameValidation(string name)
    {

    }
}
