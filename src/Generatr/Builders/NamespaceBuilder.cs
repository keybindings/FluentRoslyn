using System;
using Generatr.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Generatr.Builders;

/// <summary>
/// A namespace, and the entry point to the library: start with
/// <see cref="Get(string)"/> and then declare a type on it.
/// </summary>
/// <example>
/// <code>
/// var cls = NamespaceBuilder.Get("MyApp.Models").Class("User");
/// </code>
/// </example>
public class NamespaceBuilder : NamedBuilder
{
    /// <summary>
    /// The global namespace. Types declared on it are emitted without any namespace
    /// declaration.
    /// </summary>
    public static readonly NamespaceBuilder None = new(string.Empty, _ => {});
    private NamespaceBuilder(NamespaceBuilder parent, string name) : base(name, Identifiers.Validate)
    {
        Parent = parent;
    }

    // None Builder
    private NamespaceBuilder(string name, Action<string> validation) : base(name, validation)
    {
        Parent = this;
    }

    /// <summary>
    /// The enclosing namespace. For a top-level namespace this is
    /// <see cref="None"/>; <see cref="None"/> is its own parent.
    /// </summary>
    public NamespaceBuilder Parent { get; }

    /// <summary>
    /// True for the global namespace, which has no name to emit.
    /// </summary>
    public bool IsGlobal => this == None;

    /// <summary>
    /// Gets a namespace by name. Dotted names are split into nested levels, so
    /// <c>Get("A.B.C")</c> is equivalent to <c>Get("A").Child("B").Child("C")</c>.
    /// </summary>
    /// <param name="name">The namespace name; each dotted level must be a valid identifier.</param>
    public static NamespaceBuilder Get(string name) => New(None, name);

    /// <summary>Gets a namespace nested inside this one.</summary>
    /// <param name="name">The child name; dotted names nest further.</param>
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

    /// <summary>Declares a class in this namespace.</summary>
    public ClassBuilder Class(string name)
        => new(this, name);

    /// <summary>Declares a struct in this namespace.</summary>
    public StructBuilder Struct(string name)
        => new(this, name);

    /// <summary>Declares an enum in this namespace.</summary>
    public EnumBuilder Enum(string name)
        => new(this, name);

    /// <summary>Declares a positional record in this namespace.</summary>
    public RecordBuilder Record(string name)
        => new(this, name);

    /// <summary>Declares an interface in this namespace.</summary>
    public InterfaceBuilder Interface(string name)
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

    /// <summary>
    /// The fully qualified namespace name, e.g. <c>"A.B.C"</c>; empty for the global
    /// namespace.
    /// </summary>
    public override string ToString() => IsGlobal ? string.Empty : base.ToString();
}
