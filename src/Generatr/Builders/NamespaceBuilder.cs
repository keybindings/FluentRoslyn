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

    internal NameSyntax BuildNameSyntax()
    {
        if (this == None)
            throw new InvalidOperationException("The None namespace has no name syntax.");

        return Parent == None
            ? IdentifierName(Name)
            : QualifiedName(Parent.BuildNameSyntax(), IdentifierName(Name));
    }

    internal override SyntaxNode BuildSyntax() => BuildNameSyntax();

    public override string ToString() => this == None ? string.Empty : base.ToString();

    internal static void NameValidation(string name)
    {

    }
}
