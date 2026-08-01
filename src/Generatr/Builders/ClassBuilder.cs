using System;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Generatr.Builders;

public class ClassBuilder : TypeBuilder<ClassBuilder>
{
    internal ClassBuilder(NamespaceBuilder @namespace, string name) : base(@namespace, name)
    {
    }

    public bool IsStatic { get; set; }

    public bool IsAbstract { get; set; }

    public bool IsSealed { get; set; }

    public bool IsPartial { get; set; }

    public ClassBuilder? ParentType { get; set; }

    // Abstract members are only legal in an abstract class.
    private protected override bool AllowsAbstractMembers => IsAbstract;

    #region FluentMethods

    public ClassBuilder Static()
    {
        IsStatic = true;
        return this;
    }

    /// <summary>Marks the class <c>abstract</c>, allowing it to declare abstract members.</summary>
    public ClassBuilder Abstract()
    {
        IsAbstract = true;
        return this;
    }

    /// <summary>Marks the class <c>sealed</c>.</summary>
    public ClassBuilder Sealed()
    {
        IsSealed = true;
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
        // static / abstract / sealed are mutually exclusive on a class.
        if (new[] { IsStatic, IsAbstract, IsSealed }.Count(set => set) > 1)
            throw new InvalidOperationException(
                $"Class '{Name}' can be at most one of static, abstract, or sealed.");

        var declaration = ApplyGenerics(ClassDeclaration(Name)
            .WithAttributeLists(BuildAttributeLists())
            .WithModifiers(SyntaxFormatting.Modifiers(
                AccessModifier,
                IsStatic,
                isPartial: IsPartial,
                inheritance: IsAbstract ? Inheritance.Abstract : Inheritance.None,
                isSealed: IsSealed))
            .WithMembers(BuildMembers()));

        // Base class (if any) first, then implemented interfaces.
        var baseList = BuildBaseList(ParentType?.BuildTypeSyntax());
        return baseList is null ? declaration : declaration.WithBaseList(baseList);
    }
}
