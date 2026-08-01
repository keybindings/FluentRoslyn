using System.Collections.Generic;
using System.Linq;
using FluentRoslyn.Abstractions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace FluentRoslyn.Builders;

internal static class SyntaxParameters
{
    /// <summary>Builds a parameter list from the builder's parameters.</summary>
    internal static ParameterListSyntax List(IEnumerable<IParameter> parameters)
        => ParameterList(SeparatedList(parameters.Select(p =>
            Parameter(Identifier(p.Name)).WithType(p.TypeName.BuildTypeSyntax()))));
}
