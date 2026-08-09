using System;
using System.Collections.Generic;
using System.Text;
using FluentRoslyn.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace FluentRoslyn.Builders;

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

    private protected TypeDeclarationBuilder(
        SourceFile file,
        string name,
        TypeDeclarationBuilder? declaringType = null) : base(name, Identifiers.Validate)
    {
        File = file ?? throw new ArgumentNullException(nameof(file));
        DeclaringType = declaringType;
    }

    /// <summary>
    /// The file this type is declared in. Usings, type-name simplification, namespace
    /// style, and formatting live there, because they are shared by every type in the
    /// file rather than owned by any one of them.
    /// </summary>
    public SourceFile File { get; }

    /// <summary>The namespace this type is declared in.</summary>
    public NamespaceBuilder Namespace => File.Namespace;

    /// <summary>
    /// The type this one is nested inside, or null when it is declared directly in a
    /// namespace.
    /// </summary>
    public TypeDeclarationBuilder? DeclaringType { get; }

    /// <summary>Whether this type is nested inside another.</summary>
    public bool IsNested => DeclaringType is not null;

    /// <summary>
    /// Whether this type declares generic type parameters. A generic type builder
    /// cannot be used as a type reference — the reference would need type arguments.
    /// </summary>
    internal virtual bool HasTypeParameters => false;

    /// <summary>The type's accessibility. Public by default.</summary>
    public AccessModifier AccessModifier { get; set; } = AccessModifier.Public;

    /// <summary>Builds this kind's declaration node (class, enum, record, ...).</summary>
    private protected abstract MemberDeclarationSyntax BuildDeclaration();

    /// <summary>
    /// Builds the whole file this type belongs to, as a Roslyn syntax tree. The escape
    /// hatch for anything the fluent API cannot express. Note this is the *file*: if
    /// other types share it, they are included, because a type cannot be rendered
    /// correctly without the usings its file carries.
    /// </summary>
    public CompilationUnitSyntax BuildCompilationUnit() => File.BuildCompilationUnit();

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
    /// alone. Throws if the type — or anything it is nested in — is generic, since a
    /// reference would have to supply type arguments this side cannot know.
    /// </summary>
    internal TypeSyntax BuildTypeSyntax()
    {
        RefuseAsReference();

        // Only the innermost namespace qualification is annotated; simplifying it turns
        // Ns.Outer.Inner into Outer.Inner, which is what `using Ns;` makes legal.
        if (DeclaringType is { } declaring)
            return QualifiedName((NameSyntax)declaring.BuildTypeSyntax(), IdentifierName(Name));

        if (Namespace.IsGlobal)
            return IdentifierName(Name);

        return QualifiedName(Namespace.BuildNameSyntax(), IdentifierName(Name))
            .WithAdditionalAnnotations(TypeNameSimplifier.Annotation(Namespace.ToString()));
    }

    /// <summary>
    /// Refuses to produce a name for a generic type, or for one nested inside a generic
    /// type. Emitting <c>Repository</c> where <c>Repository&lt;T&gt;</c> is declared is
    /// CS0305 in the consumer's build, and <c>Outer&lt;T&gt;.Inner</c> drops the outer's
    /// arguments exactly as silently — so the whole declaring chain is checked, not the
    /// leaf.
    /// </summary>
    /// <remarks>
    /// The guard lives here, on the method that produces the name, rather than on the
    /// callers that ask for one. It used to live on <c>TypeNameBuilder.For</c>, so every
    /// route that reached <c>BuildTypeSyntax</c> directly — <c>WithParent</c> on a class
    /// and on a record, the receiver and constructor pairings, <c>This&lt;T&gt;</c> —
    /// bypassed it and emitted the broken reference. Here they cannot.
    /// </remarks>
    private void RefuseAsReference()
    {
        for (var type = this; type is not null; type = type.DeclaringType)
        {
            if (!type.HasTypeParameters)
                continue;

            var subject = ReferenceEquals(type, this)
                ? $"Type '{Name}' declares type parameters"
                : $"Type '{Name}' is nested in '{type.Name}', which declares type parameters";

            throw new InvalidOperationException(
                $"{subject}, so a builder reference cannot name it — the reference would have to " +
                "supply the type arguments. Spell the constructed type with the raw-string overload " +
                "instead.");
        }
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

    private protected override SourceFormatting Formatting => File.FormattingForTypes;
}
