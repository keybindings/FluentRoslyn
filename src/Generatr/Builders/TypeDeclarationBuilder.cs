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

    /// <summary>The namespace this type is declared in.</summary>
    public NamespaceBuilder Namespace { get; }

    /// <summary>
    /// Whether to emit a file-scoped namespace (<c>namespace N;</c>). True by default;
    /// see <c>BlockScopedNamespace()</c> for the braced form.
    /// </summary>
    public bool IsFileScopedNamespace { get; set; } = true;

    /// <summary>The type's accessibility. Public by default.</summary>
    public AccessModifier AccessModifier { get; set; } = AccessModifier.Public;

    /// <summary>Builds this kind's declaration node (class, enum, record, ...).</summary>
    private protected abstract MemberDeclarationSyntax BuildDeclaration();

    /// <summary>
    /// Builds the whole file as a Roslyn syntax tree — the namespace declaration wrapping
    /// this type. The escape hatch for anything the fluent API cannot express.
    /// </summary>
    public CompilationUnitSyntax BuildCompilationUnit()
        => Namespace.CompilationUnitFor(BuildDeclaration(), IsFileScopedNamespace);

    /// <summary>
    /// The generated source as a UTF-8 <see cref="SourceText"/>, ready to hand to
    /// <c>context.AddSource(...)</c> from a source generator.
    /// </summary>
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
