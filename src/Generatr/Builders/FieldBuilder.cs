using Generatr.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Generatr.Builders;

public class FieldBuilder<T> : FieldBuilder
{
    internal FieldBuilder(ClassBuilder @class, string name, AccessModifier accessModifier) : base(@class, TypeNameBuilder.New<T>(), name, accessModifier)
    {
    }
}

public abstract class FieldBuilder(
    ClassBuilder @class,
    TypeNameBuilder typeName,
    string name,
    AccessModifier accessModifier)
    : NamedBuilder(name, NameValidation), IAccessModifier, IMemberSyntaxBuilder
{
    public bool IsReadonly { get; set; }

    public bool IsStatic { get; set; }

    public ClassBuilder Class { get; } = @class;

    public AccessModifier AccessModifier { get; set; } = accessModifier;

    internal FieldDeclarationSyntax BuildField()
        => FieldDeclaration(VariableDeclaration(
                typeName.BuildTypeSyntax(),
                SingletonSeparatedList(VariableDeclarator(Identifier(Name)))))
            .WithModifiers(SyntaxFormatting.Modifiers(AccessModifier, IsStatic, IsReadonly));

    MemberDeclarationSyntax IMemberSyntaxBuilder.BuildMember() => BuildField();

    internal override SyntaxNode BuildSyntax() => BuildField();

    private static void NameValidation(string name)
    {

    }
}
