using FluentRoslyn.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace FluentRoslyn.Builders;

internal class Parameter<T> : NamedBuilder, IParameter
{
    private Parameter(string name) : base(name, Identifiers.Validate)
    {
        TypeName = TypeNameBuilder.New<T>();
    }

    internal static IParameter New(string name) => new Parameter<T>(name);

    public TypeNameBuilder TypeName { get; }

    internal override SyntaxNode BuildSyntax()
        => SyntaxFactory.Parameter(SyntaxFactory.Identifier(Name))
            .WithType(TypeName.BuildTypeSyntax());
}

/// <summary>
/// A parameter whose type is a builder reference rather than a CLR type — for
/// parameters of types being generated alongside.
/// </summary>
internal class Parameter : NamedBuilder, IParameter
{
    private Parameter(TypeNameBuilder typeName, string name) : base(name, Identifiers.Validate)
    {
        TypeName = typeName;
    }

    internal static IParameter Of(TypeDeclarationBuilder type, string name)
        => new Parameter(TypeNameBuilder.For(type), name);

    /// <summary>
    /// A parameter whose type is named by text — for a type the generator cannot name as
    /// <c>T</c>, such as one discovered from the consumer's compilation.
    /// </summary>
    internal static IParameter OfRawName(string name, string typeName)
        => new Parameter(TypeNameBuilder.ForRawName(typeName), name);

    public TypeNameBuilder TypeName { get; }

    internal override SyntaxNode BuildSyntax()
        => SyntaxFactory.Parameter(SyntaxFactory.Identifier(Name))
            .WithType(TypeName.BuildTypeSyntax());
}
