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

    public PropertyBuilder(ClassBuilder @class, string name, AccessModifier accessModifier) : base(@class, name, accessModifier)
    {
    }

    #region FluentMethods

    public PropertyBuilder<T> Static() => With(() => IsStatic = true);

    public PropertyBuilder<T> WithAccessModifier(AccessModifier accessModifier) => With(() => AccessModifier = accessModifier);

    /// <summary>Emits a get-only auto-property (<c>{ get; }</c>) by dropping the setter.</summary>
    public PropertyBuilder<T> GetOnly() => With(() => HasSet = false);

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
        => With(() => Initializer = ParseExpression(expression ?? throw new ArgumentNullException(nameof(expression))));

    #endregion

    internal override PropertyDeclarationSyntax BuildProperty()
    {
        if (!IsAutoProperty)
            throw new NotImplementedException("Only auto-properties are currently supported.");

        // An auto-property must have a getter: "{ set; }" alone does not compile.
        if (!HasGet)
            throw new InvalidOperationException($"Auto-property '{Name}' must have a getter.");

        var accessors = new List<AccessorDeclarationSyntax> { Accessor(SyntaxKind.GetAccessorDeclaration) };
        if (HasSet)
            accessors.Add(Accessor(SyntaxKind.SetAccessorDeclaration));

        var declaration = PropertyDeclaration(_typeName.BuildTypeSyntax(), Identifier(Name))
            .WithModifiers(SyntaxFormatting.Modifiers(AccessModifier, IsStatic))
            .WithAccessorList(AccessorList(List(accessors)));

        // An initialized property needs a closing semicolon after the accessor list.
        return Initializer is null
            ? declaration
            : declaration
                .WithInitializer(EqualsValueClause(Initializer))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
    }

    private static AccessorDeclarationSyntax Accessor(SyntaxKind kind)
        => AccessorDeclaration(kind).WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

    private PropertyBuilder<T> With(Action action)
    {
        action();
        return this;
    }
}

public abstract class PropertyBuilder(ClassBuilder @class, string name, AccessModifier accessModifier)
    : NamedBuilder(name, NameValidation), IAccessModifier, IMemberSyntaxBuilder
{
    public ClassBuilder Class { get; } = @class;

    public bool IsStatic { get; set; }

    public bool HasGet { get; set; } = true;

    public bool HasSet { get; set; } = true;

    public bool IsAutoProperty { get; set; } = true;

    public AccessModifier AccessModifier { get; set; } = accessModifier;

    // The property's default-value expression, or null when it has no initializer.
    internal ExpressionSyntax? Initializer { get; set; }

    internal abstract PropertyDeclarationSyntax BuildProperty();

    MemberDeclarationSyntax IMemberSyntaxBuilder.BuildMember() => BuildProperty();

    internal override SyntaxNode BuildSyntax() => BuildProperty();

    private static void NameValidation(string name)
    {

    }
}
