using System;
using System.Collections.Generic;
using Generatr.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Generatr.Builders;

public class FieldBuilder<T> : FieldBuilder
{
    internal FieldBuilder(TypeBuilder declaringType, string name, AccessModifier accessModifier) : base(declaringType, TypeNameBuilder.New<T>(), name, accessModifier)
    {
    }

    #region FluentMethods

    public FieldBuilder<T> Static() => With(() => IsStatic = true);

    public FieldBuilder<T> Readonly() => With(() => IsReadonly = true);

    public FieldBuilder<T> WithAccessModifier(AccessModifier accessModifier) => With(() => AccessModifier = accessModifier);

    /// <summary>Adds an attribute, e.g. <c>WithAttribute("JsonProperty(\"name\")")</c>.</summary>
    public FieldBuilder<T> WithAttribute(string attribute) => With(() => Attributes.Add(SyntaxAttributes.Attribute(attribute)));

    /// <summary>
    /// Marks the field <c>const</c>. A const field requires an initializer and cannot
    /// also be static or readonly.
    /// </summary>
    public FieldBuilder<T> Const() => With(() => IsConst = true);

    /// <summary>
    /// Sets a field initializer: <c>= value;</c>. Supports the primitive types with a
    /// literal form; use <see cref="WithInitializerExpression"/> for other expressions.
    /// </summary>
    public FieldBuilder<T> WithInitializer(T value) => With(() => Initializer = SyntaxLiterals.Expression(value));

    /// <summary>
    /// Sets a field initializer from a raw C# expression, e.g. <c>"new()"</c>. The
    /// escape hatch for values a literal cannot express.
    /// </summary>
    public FieldBuilder<T> WithInitializerExpression(string expression)
        => With(() => Initializer = SyntaxParse.Expression(expression));

    #endregion

    private FieldBuilder<T> With(Action action)
    {
        action();
        return this;
    }
}

public abstract class FieldBuilder(
    TypeBuilder declaringType,
    TypeNameBuilder typeName,
    string name,
    AccessModifier accessModifier)
    : NamedBuilder(name, NameValidation), IAccessModifier, IMemberSyntaxBuilder
{
    public bool IsReadonly { get; set; }

    public bool IsStatic { get; set; }

    public bool IsConst { get; set; }

    public TypeBuilder DeclaringType { get; } = declaringType;

    public AccessModifier AccessModifier { get; set; } = accessModifier;

    // The field's initializer expression, or null when it has none.
    internal ExpressionSyntax? Initializer { get; set; }

    internal List<AttributeSyntax> Attributes { get; } = [];

    internal FieldDeclarationSyntax BuildField()
    {
        if (IsConst)
        {
            if (Initializer is null)
                throw new InvalidOperationException($"Const field '{Name}' requires an initializer.");
            if (IsStatic || IsReadonly)
                throw new InvalidOperationException($"Const field '{Name}' cannot also be static or readonly.");
        }

        var declarator = VariableDeclarator(Identifier(Name));
        if (Initializer is not null)
            declarator = declarator.WithInitializer(EqualsValueClause(Initializer));

        return FieldDeclaration(VariableDeclaration(
                typeName.BuildTypeSyntax(),
                SingletonSeparatedList(declarator)))
            .WithAttributeLists(SyntaxAttributes.Lists(Attributes))
            .WithModifiers(SyntaxFormatting.Modifiers(AccessModifier, IsStatic, IsReadonly, isConst: IsConst));
    }

    MemberDeclarationSyntax IMemberSyntaxBuilder.BuildMember() => BuildField();

    internal override SyntaxNode BuildSyntax() => BuildField();

    private static void NameValidation(string name)
        => Identifiers.Validate(name);
}
