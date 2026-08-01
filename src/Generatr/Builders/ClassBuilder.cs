using System;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Generatr.Builders;

/// <summary>
/// Builds a class declaration. Obtained from <see cref="NamespaceBuilder.Class(string)"/>.
/// </summary>
public class ClassBuilder : TypeBuilder<ClassBuilder>
{
    internal ClassBuilder(NamespaceBuilder @namespace, string name) : base(@namespace, name)
    {
    }

    /// <summary>Whether the class is <c>static</c>.</summary>
    public bool IsStatic { get; set; }

    /// <summary>Whether the class is <c>abstract</c>.</summary>
    public bool IsAbstract { get; set; }

    /// <summary>Whether the class is <c>sealed</c>.</summary>
    public bool IsSealed { get; set; }

    /// <summary>Whether the class is <c>partial</c>.</summary>
    public bool IsPartial { get; set; }

    /// <summary>The base class, emitted before any implemented interfaces.</summary>
    public ClassBuilder? ParentType { get; set; }

    // Abstract members are only legal in an abstract class.
    private protected override bool AllowsAbstractMembers => IsAbstract;

    #region FluentMethods

    /// <summary>Marks the class <c>static</c>.</summary>
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

    /// <summary>Marks the class <c>partial</c>.</summary>
    public ClassBuilder Partial()
    {
        IsPartial = true;
        return this;
    }

    /// <summary>Sets the base class, emitted before any implemented interfaces.</summary>
    public ClassBuilder WithParent(ClassBuilder type)
    {
        ParentType = type;
        return this;
    }

    #endregion

    private protected override TypeDeclarationSyntax BuildTypeDeclaration()
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
