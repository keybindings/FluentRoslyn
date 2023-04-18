using System;

namespace Generatr.Builders;

public abstract class Builder
{
    public Builder(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
        Name = name;
    }

    public string Name { get; }

    protected abstract string Build();

    public override string ToString() => Build();
}