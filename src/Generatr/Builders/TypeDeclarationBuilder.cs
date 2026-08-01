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
    private readonly List<AttributeListSyntax> _attributes = [];
    private readonly DocComment _docs = new();
    private readonly TypeImports _imports = new();

    private protected TypeDeclarationBuilder(
        NamespaceBuilder @namespace,
        string name,
        TypeDeclarationBuilder? declaringType = null) : base(name, Identifiers.Validate)
    {
        Namespace = @namespace;
        DeclaringType = declaringType;
    }

    /// <summary>The namespace this type is declared in.</summary>
    public NamespaceBuilder Namespace { get; }

    /// <summary>
    /// The type this one is nested inside, or null when it is declared directly in a
    /// namespace.
    /// </summary>
    public TypeDeclarationBuilder? DeclaringType { get; }

    /// <summary>Whether this type is nested inside another.</summary>
    public bool IsNested => DeclaringType is not null;

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
    {
        var unit = Namespace.CompilationUnitFor(BuildDocumentedDeclaration(), IsFileScopedNamespace);
        return _imports.ApplyTo(unit, Namespace.IsGlobal ? null : Namespace.ToString());
    }

    // Doc trivia is attached centrally, so every type kind gets it without repeating the
    // wiring — and before NormalizeWhitespace, which is what indents it correctly.
    internal MemberDeclarationSyntax BuildDocumentedDeclaration()
    {
        var declaration = BuildDeclaration();
        return _docs.IsEmpty ? declaration : declaration.WithLeadingTrivia(_docs.Build());
    }

    /// <summary>
    /// The generated source as a UTF-8 <see cref="SourceText"/>, ready to hand to
    /// <c>context.AddSource(...)</c> from a source generator.
    /// </summary>
    public SourceText ToSourceText()
        => SourceText.From(ToString(), Encoding.UTF8);

    /// <summary>
    /// The fully qualified name of this type, for use as a type reference. A nested type
    /// is qualified by its declaring type (<c>Ns.Outer.Inner</c>), not by the namespace
    /// alone.
    /// </summary>
    internal TypeSyntax BuildTypeSyntax()
    {
        // Only the innermost namespace qualification is annotated; simplifying it turns
        // Ns.Outer.Inner into Outer.Inner, which is what `using Ns;` makes legal.
        if (DeclaringType is { } declaring)
            return QualifiedName((NameSyntax)declaring.BuildTypeSyntax(), IdentifierName(Name));

        if (Namespace.IsGlobal)
            return IdentifierName(Name);

        return QualifiedName(Namespace.BuildNameSyntax(), IdentifierName(Name))
            .WithAdditionalAnnotations(TypeNameSimplifier.Annotation(Namespace.ToString()));
    }

    // A nested type is not a file, so emitting it standalone gives just the declaration
    // rather than wrapping it in a namespace it does not own.
    internal override SyntaxNode BuildSyntax()
        => IsNested ? BuildDocumentedDeclaration() : BuildCompilationUnit();

    private protected SyntaxList<AttributeListSyntax> BuildAttributeLists()
        => SyntaxAttributes.Lists(_attributes);

    private protected void AddAttribute(string attribute)
        => _attributes.Add(SyntaxAttributes.AttributeList(attribute));

    private protected void AddSummary(string text)
        => _docs.SetSummary(text);

    private protected void AddUsing(string namespaceName)
        => _imports.Add(namespaceName);

    private protected void EnableTypeNameSimplification()
        => _imports.EnableSimplification();
}
