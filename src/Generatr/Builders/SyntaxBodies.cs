using System;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Generatr.Builders;

internal static class SyntaxBodies
{
    /// <summary>
    /// Parses a single complete C# statement, e.g. <c>"return a + b;"</c> or an
    /// <c>if</c> block. The raw-text escape hatch until statements get a fluent model.
    /// </summary>
    internal static StatementSyntax Statement(string statement)
        => SyntaxParse.Statement(statement);
}
