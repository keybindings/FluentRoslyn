using System.Collections.Generic;
using System.Text;
using Generatr.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Generatr.Builders;

/// <summary>
/// Declaration-level machinery shared by every top-level type kind (class, struct,
/// record, enum, interface): namespace placement, access modifier, attribute storage,
/// and the compilation-unit / source-text pipeline. Concrete kinds implement
/// <see cref="BuildDeclaration"/>.
/// </summary>
public abstract class TypeDeclarationBuilder : NamedBuilder
{
    private readonly List<AttributeSyntax> _attributes = [];

    private protected TypeDeclarationBuilder(NamespaceBuilder @namespace, string name) : base(name, Identifiers.Validate)
    {
        Namespace = @namespace;
    }

    public NamespaceBuilder Namespace { get; }

    public bool IsFileScopedNamespace { get; set; } = true;

    public AccessModifier AccessModifier { get; set; } = AccessModifier.Public;

    /// <summary>Builds this kind's declaration node (class, enum, record, ...).</summary>
    protected abstract MemberDeclarationSyntax BuildDeclaration();

    public CompilationUnitSyntax BuildCompilationUnit()
        => Namespace.CompilationUnitFor(BuildDeclaration(), IsFileScopedNamespace);

    public SourceText ToSourceText()
        => SourceText.From(ToString(), Encoding.UTF8);

    /// <summary>The fully qualified name of this type, for use as a type reference.</summary>
    internal TypeSyntax BuildTypeSyntax()
        => Namespace.IsGlobal
            ? IdentifierName(Name)
            : QualifiedName(Namespace.BuildNameSyntax(), IdentifierName(Name));

    internal override SyntaxNode BuildSyntax() => BuildCompilationUnit();

    private protected SyntaxList<AttributeListSyntax> BuildAttributeLists()
        => SyntaxAttributes.Lists(_attributes);

    private protected void AddAttribute(string attribute)
        => _attributes.Add(SyntaxAttributes.Attribute(attribute));
}
