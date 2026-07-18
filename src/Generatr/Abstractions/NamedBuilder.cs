using System;
using Microsoft.CodeAnalysis;

namespace Generatr.Abstractions;

public abstract class NamedBuilder : INamedBuilder
{
    protected NamedBuilder(string name, Action<string> validNameCheck)
    {
        validNameCheck.Invoke(name ?? throw new ArgumentNullException(nameof(name)));
        Name = name;
    }

    public string Name { get; }

    internal abstract SyntaxNode BuildSyntax();

    public override string ToString()
        => BuildSyntax().NormalizeWhitespace(eol: Environment.NewLine).ToFullString();
}
