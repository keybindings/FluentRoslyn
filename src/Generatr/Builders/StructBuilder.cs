using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Generatr.Builders;

public class StructBuilder : TypeBuilder<StructBuilder>
{
    internal StructBuilder(NamespaceBuilder @namespace, string name) : base(@namespace, name)
    {
    }

    public bool IsReadonly { get; set; }

    public bool IsPartial { get; set; }

    #region FluentMethods

    public StructBuilder Readonly()
    {
        IsReadonly = true;
        return this;
    }

    public StructBuilder Partial()
    {
        IsPartial = true;
        return this;
    }

    #endregion

    protected override TypeDeclarationSyntax BuildTypeDeclaration()
    {
        var declaration = ApplyGenerics(StructDeclaration(Name)
            .WithAttributeLists(BuildAttributeLists())
            .WithModifiers(SyntaxFormatting.Modifiers(AccessModifier, isReadonly: IsReadonly, isPartial: IsPartial))
            .WithMembers(BuildMembers()));

        // Structs have no base class, only implemented interfaces.
        var baseList = BuildBaseList(null);
        return baseList is null ? declaration : declaration.WithBaseList(baseList);
    }
}
