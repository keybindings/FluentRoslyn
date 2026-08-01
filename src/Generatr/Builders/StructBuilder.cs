using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Generatr.Builders;

/// <summary>
/// Builds a struct declaration. Obtained from <see cref="NamespaceBuilder.Struct(string)"/>.
/// </summary>
public class StructBuilder : TypeBuilder<StructBuilder>
{
    internal StructBuilder(NamespaceBuilder @namespace, string name) : base(@namespace, name)
    {
    }

    /// <summary>Whether the struct is <c>readonly</c>.</summary>
    public bool IsReadonly { get; set; }

    /// <summary>Whether the struct is <c>partial</c>.</summary>
    public bool IsPartial { get; set; }

    #region FluentMethods

    /// <summary>Marks the struct <c>readonly</c>.</summary>
    public StructBuilder Readonly()
    {
        IsReadonly = true;
        return this;
    }

    /// <summary>Marks the struct <c>partial</c>.</summary>
    public StructBuilder Partial()
    {
        IsPartial = true;
        return this;
    }

    #endregion

    private protected override TypeDeclarationSyntax BuildTypeDeclaration()
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
