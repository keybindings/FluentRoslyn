using System;
using System.Collections.Generic;
using System.Linq;

namespace Generatr.Builders;

public abstract class NamedBuilder
{
    private static readonly HashSet<char> InvalidChars = new() { ' ' };

    protected NamedBuilder(string name)
    {
        DefaultNameInvalidAssertion(name ?? throw new ArgumentNullException(nameof(name)));
        Name = name;
    }

    public string Name { get; }

    protected abstract string Build();

    public override string ToString() => Build();

    protected virtual bool AdditionalNameAssertions(string name) => false;

    private void DefaultNameInvalidAssertion(string name)
    {
        if (name.Length == 0 || char.IsNumber(name[0]) || name.Any(IsInvalidChar) || AdditionalNameAssertions(name))
            throw new ArgumentOutOfRangeException(nameof(name), name, $"Name: \"{name}\" contains invalid chars.");
    }

    private static bool IsInvalidChar(char c) => InvalidChars.Contains(c);
}