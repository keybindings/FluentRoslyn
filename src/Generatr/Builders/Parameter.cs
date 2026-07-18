using Generatr.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Generatr.Builders;

public class Parameter<T> : NamedBuilder, IParameter
{
    private Parameter(string name) : base(name, NameValidation)
    {
        TypeName = TypeNameBuilder.New<T>();
    }

    public static IParameter New(string name) => new Parameter<T>(name);

    public TypeNameBuilder TypeName { get; }

    internal override SyntaxNode BuildSyntax()
        => SyntaxFactory.Parameter(SyntaxFactory.Identifier(Name))
            .WithType(TypeName.BuildTypeSyntax());

    private static void NameValidation(string name)
    {

    }
}
