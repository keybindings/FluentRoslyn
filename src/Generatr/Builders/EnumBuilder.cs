using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Generatr.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Generatr.Builders;

public class EnumBuilder : NamedBuilder
{
    private readonly List<(string Name, long? Value)> _members = [];
    private readonly List<AttributeSyntax> _attributes = [];
    private TypeNameBuilder? _underlyingType;

    internal EnumBuilder(NamespaceBuilder @namespace, string name) : base(name, NameValidation)
    {
        Namespace = @namespace;
    }

    public NamespaceBuilder Namespace { get; }

    public bool IsFileScopedNamespace { get; set; } = true;

    public AccessModifier AccessModifier { get; set; } = AccessModifier.Public;

    #region FluentMethods

    public EnumBuilder WithAccessModifier(AccessModifier accessModifier) => With(() => AccessModifier = accessModifier);

    public EnumBuilder BlockScopedNamespace() => With(() => IsFileScopedNamespace = false);

    /// <summary>Sets the underlying integral type: <c>enum Name : byte</c>.</summary>
    public EnumBuilder WithUnderlyingType<T>() => With(() => _underlyingType = TypeNameBuilder.New<T>());

    /// <summary>Adds an attribute, e.g. <c>WithAttribute("Flags")</c>.</summary>
    public EnumBuilder WithAttribute(string attribute) => With(() => _attributes.Add(SyntaxAttributes.Attribute(attribute)));

    /// <summary>Adds a member with an implicit value: <c>Name</c>.</summary>
    public EnumBuilder AddMember(string name) => With(() => _members.Add((RequireName(name), null)));

    /// <summary>Adds a member with an explicit value: <c>Name = value</c>.</summary>
    public EnumBuilder AddMember(string name, long value) => With(() => _members.Add((RequireName(name), value)));

    #endregion

    internal EnumDeclarationSyntax BuildEnumDeclaration()
    {
        var declaration = EnumDeclaration(Name)
            .WithAttributeLists(SyntaxAttributes.Lists(_attributes))
            .WithModifiers(SyntaxFormatting.Modifiers(AccessModifier))
            .WithMembers(SeparatedList(_members.Select(BuildMember)));

        return _underlyingType is null
            ? declaration
            : declaration.WithBaseList(BaseList(SingletonSeparatedList<BaseTypeSyntax>(
                SimpleBaseType(_underlyingType.BuildTypeSyntax()))));
    }

    public CompilationUnitSyntax BuildCompilationUnit()
        => Namespace.CompilationUnitFor(BuildEnumDeclaration(), IsFileScopedNamespace);

    public SourceText ToSourceText()
        => SourceText.From(ToString(), Encoding.UTF8);

    internal override SyntaxNode BuildSyntax() => BuildCompilationUnit();

    private static EnumMemberDeclarationSyntax BuildMember((string Name, long? Value) member)
    {
        var declaration = EnumMemberDeclaration(Identifier(member.Name));

        if (member.Value is not long value)
            return declaration;

        // Emit a plain integer literal (no L suffix) regardless of the underlying type.
        var literal = LiteralExpression(SyntaxKind.NumericLiteralExpression,
            Literal(value.ToString(CultureInfo.InvariantCulture), value));

        return declaration.WithEqualsValue(EqualsValueClause(literal));
    }

    private static string RequireName(string name)
    {
        Identifiers.Validate(name);
        return name;
    }

    private static void NameValidation(string name)
        => Identifiers.Validate(name);

    private EnumBuilder With(Action action)
    {
        action();
        return this;
    }
}
