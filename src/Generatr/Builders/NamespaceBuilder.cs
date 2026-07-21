using System;
using Generatr.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Generatr.Builders;

public class NamespaceBuilder : NamedBuilder
{
    public static readonly NamespaceBuilder None = new(string.Empty, _ => {});
    private NamespaceBuilder(NamespaceBuilder parent, string name) : base(name, NameValidation)
    {
        Parent = parent;
    }

    // None Builder
    private NamespaceBuilder(string name, Action<string> validation) : base(name, validation)
    {
        Parent = this;
    }

    public NamespaceBuilder Parent { get; }

    /// <summary>
    /// True for the global namespace, which has no name to emit.
    /// </summary>
    public bool IsGlobal => this == None;

    public static NamespaceBuilder Get(string name) => New(None, name);

    public NamespaceBuilder Child(string name) => New(this, name);

    private static NamespaceBuilder New(NamespaceBuilder parent, string name)
    {
        if (name == null) throw new ArgumentNullException(nameof(name));

        var levels = name.Split('.');

        var target = new NamespaceBuilder(parent, levels[0]);

        if (levels.Length == 1) return target;

        for (var i = 1; i < levels.Length; i++)
        {
            target = New(target, levels[i]);
        }

        return target;
    }

    public ClassBuilder Class(string name)
        => new(this, name);

    public EnumBuilder Enum(string name)
        => new(this, name);

    /// <summary>
    /// Wraps a top-level type declaration in this namespace and a compilation unit. A
    /// global namespace yields the bare type; otherwise a file-scoped or block-scoped
    /// namespace declaration.
    /// </summary>
    internal CompilationUnitSyntax CompilationUnitFor(MemberDeclarationSyntax member, bool fileScoped)
    {
        if (IsGlobal)
            return CompilationUnit().WithMembers(SingletonList(member));

        var name = BuildNameSyntax();
        var body = SingletonList(member);

        MemberDeclarationSyntax namespaceDeclaration = fileScoped
            ? FileScopedNamespaceDeclaration(name).WithMembers(body)
            : NamespaceDeclaration(name).WithMembers(body);

        return CompilationUnit().WithMembers(SingletonList(namespaceDeclaration));
    }

    internal NameSyntax BuildNameSyntax()
    {
        if (IsGlobal)
            throw new InvalidOperationException("The global namespace has no name syntax.");

        return Parent.IsGlobal
            ? IdentifierName(Name)
            : QualifiedName(Parent.BuildNameSyntax(), IdentifierName(Name));
    }

    internal override SyntaxNode BuildSyntax() => BuildNameSyntax();

    public override string ToString() => IsGlobal ? string.Empty : base.ToString();

    internal static void NameValidation(string name)
    {

    }
}
