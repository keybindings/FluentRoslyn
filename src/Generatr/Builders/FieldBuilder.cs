using System;
using System.Collections.Generic;
using Generatr.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Generatr.Builders;

/// <summary>
/// Builds a field declaration of type <typeparamref name="T"/>. Obtained from
/// <c>DefineField&lt;T&gt;</c> on a type builder.
/// </summary>
/// <typeparam name="T">The field's type.</typeparam>
public class FieldBuilder<T> : FieldBuilder
{
    internal FieldBuilder(string name, AccessModifier accessModifier) : base(TypeNameBuilder.New<T>(), name, accessModifier)
    {
    }

    #region FluentMethods

    /// <summary>Marks the field <c>static</c>.</summary>
    public FieldBuilder<T> Static() => this.With(() => IsStatic = true);

    /// <summary>Marks the field <c>readonly</c>.</summary>
    public FieldBuilder<T> Readonly() => this.With(() => IsReadonly = true);

    /// <summary>Sets the field's accessibility. Private by default.</summary>
    public FieldBuilder<T> WithAccessModifier(AccessModifier accessModifier) => this.With(() => AccessModifier = accessModifier);

    /// <summary>Documents the field with an XML <c>&lt;summary&gt;</c>.</summary>
    public FieldBuilder<T> WithSummary(string text) => this.With(() => Docs.SetSummary(text));

    /// <summary>
    /// Marks the field <c>required</c> (C# 11): callers must set it in an object
    /// initializer. Cannot combine with <c>static</c> or <c>const</c>.
    /// </summary>
    public FieldBuilder<T> Required() => this.With(() => IsRequired = true);

    /// <summary>Adds an attribute, e.g. <c>WithAttribute("JsonProperty(\"name\")")</c>.</summary>
    public FieldBuilder<T> WithAttribute(string attribute) => this.With(() => Attributes.Add(SyntaxAttributes.Attribute(attribute)));

    /// <summary>
    /// Marks the field <c>const</c>. A const field requires an initializer and cannot
    /// also be static or readonly.
    /// </summary>
    public FieldBuilder<T> Const() => this.With(() => IsConst = true);

    /// <summary>
    /// Sets a field initializer: <c>= value;</c>. Supports the primitive types with a
    /// literal form; use <see cref="WithInitializerExpression"/> for other expressions.
    /// </summary>
    public FieldBuilder<T> WithInitializer(T value) => this.With(() => Initializer = SyntaxLiterals.Expression(value));

    /// <summary>
    /// Sets a field initializer from a raw C# expression, e.g. <c>"new()"</c>. The
    /// escape hatch for values a literal cannot express.
    /// </summary>
    public FieldBuilder<T> WithInitializerExpression(string expression)
        => this.With(() => Initializer = SyntaxParse.Expression(expression));

    #endregion
}

/// <summary>
/// The non-generic base of <see cref="FieldBuilder{T}"/>, carrying the state that does
/// not depend on the field's type.
/// </summary>
public abstract class FieldBuilder(
    TypeNameBuilder typeName,
    string name,
    AccessModifier accessModifier)
    : NamedBuilder(name, Identifiers.Validate), IAccessModifier, IMemberSyntaxBuilder
{
    /// <summary>Whether the field is <c>readonly</c>.</summary>
    public bool IsReadonly { get; set; }

    /// <summary>Whether the field is <c>static</c>.</summary>
    public bool IsStatic { get; set; }

    /// <summary>
    /// Whether the field is <c>const</c>. A const field requires an initializer and
    /// cannot also be static or readonly.
    /// </summary>
    public bool IsConst { get; set; }

    /// <summary>
    /// Whether the field is <c>required</c>. Cannot combine with static or const.
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>The field's accessibility. Private by default.</summary>
    public AccessModifier AccessModifier { get; set; } = accessModifier;

    // The field's initializer expression, or null when it has none.
    internal ExpressionSyntax? Initializer { get; set; }

    internal List<AttributeSyntax> Attributes { get; } = [];

    internal DocComment Docs { get; } = new();

    internal FieldDeclarationSyntax BuildField()
    {
        if (IsConst)
        {
            if (Initializer is null)
                throw new InvalidOperationException($"Const field '{Name}' requires an initializer.");
            if (IsStatic || IsReadonly)
                throw new InvalidOperationException($"Const field '{Name}' cannot also be static or readonly.");
        }

        // A required member is set by the caller during initialization, which neither a
        // static nor a const field participates in.
        if (IsRequired && (IsStatic || IsConst))
            throw new InvalidOperationException($"Required field '{Name}' cannot also be static or const.");

        var declarator = VariableDeclarator(Identifier(Name));
        if (Initializer is not null)
            declarator = declarator.WithInitializer(EqualsValueClause(Initializer));

        var field = FieldDeclaration(VariableDeclaration(
                typeName.BuildTypeSyntax(),
                SingletonSeparatedList(declarator)))
            .WithAttributeLists(SyntaxAttributes.Lists(Attributes))
            .WithModifiers(SyntaxFormatting.Modifiers(
                AccessModifier, IsStatic, IsReadonly, isConst: IsConst, isRequired: IsRequired));

        return Docs.IsEmpty ? field : field.WithLeadingTrivia(Docs.Build());
    }

    MemberDeclarationSyntax IMemberSyntaxBuilder.BuildMember() => BuildField();

    internal override SyntaxNode BuildSyntax() => BuildField();
}
