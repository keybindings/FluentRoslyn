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

    internal override PropertyDeclarationSyntax BuildProperty()
    {
        if (!IsAutoProperty)
            throw new NotImplementedException("Only auto-properties are currently supported.");

        var accessors = new List<AccessorDeclarationSyntax>();
        if (HasGet)
            accessors.Add(AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));
        if (HasSet)
            accessors.Add(AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));

        return PropertyDeclaration(_typeName.BuildTypeSyntax(), Identifier(Name))
            .WithModifiers(SyntaxFormatting.Modifiers(AccessModifier, IsStatic))
            .WithAccessorList(AccessorList(List(accessors)));
    }
}

public abstract class PropertyBuilder(ClassBuilder @class, string name, AccessModifier accessModifier)
    : NamedBuilder(name, NameValidation), IAccessModifier, IMemberSyntaxBuilder
{
    public ClassBuilder Class { get; } = @class;

    public bool IsStatic { get; set; }

    public bool HasGet { get; } = true;

    public bool HasSet { get; } = true;

    public bool IsAutoProperty { get; set; } = true;

    public AccessModifier AccessModifier { get; set; } = accessModifier;

    internal abstract PropertyDeclarationSyntax BuildProperty();

    MemberDeclarationSyntax IMemberSyntaxBuilder.BuildMember() => BuildProperty();

    internal override SyntaxNode BuildSyntax() => BuildProperty();

    private static void NameValidation(string name)
    {

    }
}
