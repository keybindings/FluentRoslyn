using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentRoslyn.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace FluentRoslyn.Builders;

/// <summary>
/// A generated source file: a namespace, its using directives, and the types declared in
/// it. This is the unit a generator hands to <c>context.AddSource(...)</c>.
/// </summary>
/// <remarks>
/// Usings, type-name simplification, namespace style, and formatting live here rather
/// than on a type builder, because they are properties of a file. Two types sharing a
/// file therefore cannot disagree about them — and simplification can consider every
/// type in the file when deciding whether a name is ambiguous, which is the only way
/// that decision can be correct.
/// </remarks>
/// <example>
/// <code>
/// var file = SourceFile.InNamespace("MyApp.Models").SimplifyTypeNames();
/// var user = file.Class("User");
/// var options = file.Record("UserOptions");
/// context.AddSource("Users.g.cs", file.ToSourceText());
/// </code>
/// </example>
public sealed class SourceFile : NamedBuilder
{
    private readonly List<TypeDeclarationBuilder> _types = [];
    private readonly TypeImports _imports = new();
    private SourceFormatting _formatting = SourceFormatting.Default;

    private SourceFile(NamespaceBuilder @namespace) : base(@namespace.ToString(), _ => { })
    {
        Namespace = @namespace;
    }

    /// <summary>The namespace every type in this file is declared in.</summary>
    public NamespaceBuilder Namespace { get; }

    /// <summary>
    /// Whether to emit a file-scoped namespace (<c>namespace N;</c>). True by default;
    /// see <see cref="BlockScopedNamespace"/>.
    /// </summary>
    public bool IsFileScopedNamespace { get; set; } = true;

    /// <summary>The types declared in this file, in declaration order.</summary>
    public IReadOnlyList<TypeDeclarationBuilder> Types => _types;

    /// <summary>Starts a file in the given namespace.</summary>
    /// <param name="namespaceName">A dotted namespace name; each level must be a valid identifier.</param>
    public static SourceFile InNamespace(string namespaceName)
        => new(NamespaceBuilder.Get(namespaceName));

    /// <summary>Starts a file in the given namespace.</summary>
    public static SourceFile InNamespace(NamespaceBuilder @namespace)
        => new(@namespace ?? throw new ArgumentNullException(nameof(@namespace)));

    /// <summary>
    /// Starts a file in the global namespace, so no namespace declaration is emitted.
    /// </summary>
    public static SourceFile InGlobalNamespace()
        => new(NamespaceBuilder.None);

    #region Types

    /// <summary>Declares a class in this file.</summary>
    public ClassBuilder Class(string name) => Add(new ClassBuilder(this, name));

    /// <summary>Declares a struct in this file.</summary>
    public StructBuilder Struct(string name) => Add(new StructBuilder(this, name));

    /// <summary>Declares an enum in this file.</summary>
    public EnumBuilder Enum(string name) => Add(new EnumBuilder(this, name));

    /// <summary>Declares a positional record in this file.</summary>
    public RecordBuilder Record(string name) => Add(new RecordBuilder(this, name));

    /// <summary>Declares an interface in this file.</summary>
    public InterfaceBuilder Interface(string name) => Add(new InterfaceBuilder(this, name));

    /// <summary>Declares a <c>void</c>-returning delegate in this file.</summary>
    public DelegateBuilder Delegate(string name)
        => Add(new DelegateBuilder(this, name, PredefinedType(Token(SyntaxKind.VoidKeyword))));

    /// <summary>Declares a delegate returning <typeparamref name="TReturn"/>.</summary>
    public DelegateBuilder Delegate<TReturn>(string name)
        => Add(new DelegateBuilder(this, name, TypeNameBuilder.New<TReturn>().BuildTypeSyntax()));

    private TType Add<TType>(TType type) where TType : TypeDeclarationBuilder
    {
        _types.Add(type);
        return type;
    }

    #endregion

    #region FluentMethods

    /// <summary>Adds a using directive, e.g. <c>WithUsing("System.Linq")</c>.</summary>
    public SourceFile WithUsing(string namespaceName) => this.With(() => _imports.Add(namespaceName));

    /// <summary>
    /// Shortens generated type references and imports the namespaces they need, so
    /// <c>System.Collections.Generic.List&lt;int&gt;</c> becomes <c>List&lt;int&gt;</c>
    /// under a <c>using System.Collections.Generic;</c>. A name stays fully qualified
    /// whenever the shortened form would bind somewhere else: offered by two namespaces,
    /// declared by a type, delegate or type parameter in this file, matching a namespace
    /// visible here, or already offered by a namespace <see cref="WithUsing"/> imported.
    /// </summary>
    /// <remarks>
    /// The one case a syntax-only pass cannot judge is a <see cref="WithUsing"/> of a
    /// namespace this file never names a type from: what it contains is unknowable from
    /// here, so a collision with a type in it is not caught.
    /// </remarks>
    public SourceFile SimplifyTypeNames() => this.With(() => _imports.EnableSimplification());

    /// <summary>
    /// Emits a block-scoped namespace (<c>namespace N { ... }</c>) instead of the default
    /// file-scoped form (<c>namespace N;</c>).
    /// </summary>
    public SourceFile BlockScopedNamespace() => this.With(() => IsFileScopedNamespace = false);

    /// <summary>Sets the indentation string, e.g. <c>"\t"</c>. Four spaces by default.</summary>
    public SourceFile WithIndentation(string indentation)
        => this.With(() => _formatting = _formatting.WithIndentation(indentation));

    /// <summary>
    /// Sets the line endings, e.g. <c>"\r\n"</c>. <c>"\n"</c> by default, which keeps
    /// output byte-identical across operating systems.
    /// </summary>
    public SourceFile WithLineEndings(string lineEndings)
        => this.With(() => _formatting = _formatting.WithLineEndings(lineEndings));

    #endregion

    /// <summary>
    /// Builds the whole file as a Roslyn syntax tree. The escape hatch for anything the
    /// fluent API cannot express.
    /// </summary>
    public CompilationUnitSyntax BuildCompilationUnit()
    {
        if (_types.Count == 0)
            throw new InvalidOperationException(
                $"Source file for namespace '{Name}' declares no types.");

        var unit = Namespace.CompilationUnitFor(
            _types.Select(t => t.BuildDocumentedDeclaration()), IsFileScopedNamespace);

        return _imports.ApplyTo(unit, Namespace.IsGlobal ? null : Namespace.ToString());
    }

    /// <summary>
    /// The generated source as a UTF-8 <see cref="SourceText"/>, ready to hand to
    /// <c>context.AddSource(...)</c> from a source generator.
    /// </summary>
    public SourceText ToSourceText() => SourceText.From(ToString(), Encoding.UTF8);

    internal override SyntaxNode BuildSyntax() => BuildCompilationUnit();

    private protected override SourceFormatting Formatting => _formatting;

    // Type builders render through their file's formatting; exposed rather than made
    // protected because they are not in this class's inheritance chain.
    internal SourceFormatting FormattingForTypes => _formatting;
}
