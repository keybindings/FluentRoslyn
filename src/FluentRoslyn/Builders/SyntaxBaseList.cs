using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace FluentRoslyn.Builders;

internal static class SyntaxBaseList
{
    /// <summary>
    /// Builds a base list (<c>: A, B, C</c>) from the given types in order, or null
    /// when there are none. The caller is responsible for ordering (base class first).
    /// </summary>
    internal static BaseListSyntax? From(IEnumerable<TypeSyntax> baseTypes)
    {
        var list = baseTypes.Select(t => (BaseTypeSyntax)SimpleBaseType(t)).ToList();
        return list.Count == 0 ? null : BaseList(SeparatedList(list));
    }
}
