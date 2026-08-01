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
