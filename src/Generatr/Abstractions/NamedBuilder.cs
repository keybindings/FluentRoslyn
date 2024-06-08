using System;
using System.Text;
using Generatr.Builders;

namespace Generatr.Abstractions;

public abstract class NamedBuilder : INamedBuilder
{
    protected NamedBuilder(string name, Action<string> validNameCheck)
    {
        validNameCheck.Invoke(name ?? throw new ArgumentNullException(nameof(name)));
        Name = name;
    }

    public string Name { get; }

    public virtual void Build(TabbedBuilder tb)
    {
        tb.Append(Name);
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        var tabbedBuilder = new TabbedBuilder(sb);
        Build(tabbedBuilder);
        return tabbedBuilder.ToString();
    }
}