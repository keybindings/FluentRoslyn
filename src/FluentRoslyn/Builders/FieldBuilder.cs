using System;
using System.Collections.Generic;
using FluentRoslyn.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace FluentRoslyn.Builders;

/// <summary>
/// The fluent surface shared by every field builder, whatever its type came from.
/// <typeparamref name="TSelf"/> is the concrete kind, so chaining yields it — the same
/// CRTP shape as <see cref="TypeBuilder{TSelf}"/> and <see cref="MethodBuilderBase{TSelf}"/>.
/// </summary>
/// <typeparam name="TSelf">The concrete field builder type.</typeparam>
public abstract class FieldBuilderBase<TSelf>(
    TypeNameBuilder typeName,
    string name,
    AccessModifier accessModifier)
    : FieldBuilder(typeName, name, accessModifier)
    where TSelf : FieldBuilderBase<TSelf>
{
    /// <summary>This builder as its concrete type, for fluent returns.</summary>
    private protected TSelf Self => (TSelf)this;

    #region FluentMethods

    /// <summary>Marks the field <c>static</c>.</summary>
    public TSelf Static() => Self.With(() => IsStatic = true);

    /// <summary>Marks the field <c>readonly</c>.</summary>
    public TSelf Readonly() => Self.With(() => IsReadonly = true);

    /// <summary>Sets the field's accessibility. Private by default.</summary>
    public TSelf WithAccessModifier(AccessModifier accessModifier) => Self.With(() => AccessModifier = accessModifier);

    /// <summary>Documents the field with an XML <c>&lt;summary&gt;</c>.</summary>
    public TSelf WithSummary(string text) => Self.With(() => Docs.SetSummary(text));

    /// <summary>
    /// Marks the field <c>required</c> (C# 11): callers must set it in an object
    /// initializer. Cannot combine with <c>static</c> or <c>const</c>.
    /// </summary>
    public TSelf Required() => Self.With(() => IsRequired = true);

    /// <summary>Adds an attribute, e.g. <c>WithAttribute("JsonProperty(\"name\")")</c>.</summary>
    public TSelf WithAttribute(string attribute) => Self.With(() => Attributes.Add(SyntaxAttributes.AttributeList(attribute)));

    /// <summary>
    /// Marks the field <c>const</c>. A const field requires an initializer and cannot
    /// also be static or readonly.
    /// </summary>
    public TSelf Const() => Self.With(() => IsConst = true);

    /// <summary>
    /// Sets a field initializer from a raw C# expression, e.g. <c>"new()"</c>. The
    /// escape hatch for values a literal cannot express.
    /// </summary>
    public TSelf WithInitializerExpression(string expression)
        => Self.With(() => Initializer = SyntaxParse.Expression(expression));

    #endregion
}

/// <summary>
/// Builds a field declaration of type <typeparamref name="T"/>. Obtained from
/// <c>DefineField&lt;T&gt;</c> on a type builder.
/// </summary>
/// <typeparam name="T">The field's type.</typeparam>
public class FieldBuilder<T> : FieldBuilderBase<FieldBuilder<T>>, IReference<T>, IReferenceInfo
{
    internal FieldBuilder(string name, AccessModifier accessModifier) : base(TypeNameBuilder.New<T>(), name, accessModifier)
    {
    }

    ReferenceKind IReferenceInfo.Kind => ReferenceKind.Member;

    bool IReferenceInfo.IsStaticMember => IsStatic;

    /// <summary>
    /// Sets a field initializer: <c>= value;</c>. Supports the primitive types with a
    /// literal form; use <see cref="FieldBuilderBase{TSelf}.WithInitializerExpression"/>
    /// for other expressions.
    /// </summary>
    public FieldBuilder<T> WithInitializer(T value) => this.With(() => Initializer = SyntaxLiterals.Expression(value));
}

/// <summary>
/// Builds a field whose type is named by text rather than by a type argument. Obtained
/// from <c>DefineField(name, typeName)</c> on a type builder.
/// </summary>
/// <remarks>
/// The escape hatch for a type the generator cannot name as <c>T</c> — above all the
/// consumer's own types, which a generator only ever holds as an <c>ISymbol</c>
/// discovered at generation time. The cost is stated rather than hidden: this is
/// deliberately <em>not</em> an <see cref="IReference{T}"/>, because there is no
/// <c>T</c> to check against, so <c>Assign</c>, <c>Return</c> and the rest of the typed
/// surface cannot reach it. Bodies touching such a field go through
/// <c>AddStatement</c>. Everything structural — the declaration, modifiers, attributes,
/// docs — is still built rather than concatenated.
/// </remarks>
public sealed class RawFieldBuilder : FieldBuilderBase<RawFieldBuilder>, IReference, IReferenceInfo
{
    internal RawFieldBuilder(string name, string typeName, AccessModifier accessModifier)
        : base(TypeNameBuilder.ForRawName(typeName), name, accessModifier)
    {
    }

    ReferenceKind IReferenceInfo.Kind => ReferenceKind.Member;

    bool IReferenceInfo.IsStaticMember => IsStatic;
}

/// <summary>
/// The non-generic base of the field builders, carrying the state that does not depend
/// on how the field's type was supplied.
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

    internal List<AttributeListSyntax> Attributes { get; } = [];

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
