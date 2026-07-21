using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Generatr.Builders;

public class ClassBuilder : TypeBuilder<ClassBuilder>
{
    internal ClassBuilder(NamespaceBuilder @namespace, string name) : base(@namespace, name)
    {
    }

    public bool IsStatic { get; set; }

    public bool IsPartial { get; set; }

    public ClassBuilder? ParentType { get; set; }

    #region FluentMethods

    public ClassBuilder Static()
    {
        IsStatic = true;
        return this;
    }

    public ClassBuilder Partial()
    {
        IsPartial = true;
        return this;
    }

    public ClassBuilder WithParent(ClassBuilder type)
    {
        ParentType = type;
        return this;
    }

    #endregion

    protected override TypeDeclarationSyntax BuildTypeDeclaration()
    {
        var declaration = ClassDeclaration(Name)
            .WithAttributeLists(BuildAttributeLists())
            .WithModifiers(SyntaxFormatting.Modifiers(AccessModifier, IsStatic, isPartial: IsPartial))
            .WithMembers(BuildMembers());

        // Base class (if any) first, then implemented interfaces.
        var baseList = BuildBaseList(ParentType?.BuildTypeSyntax());
        return baseList is null ? declaration : declaration.WithBaseList(baseList);
    }
}
