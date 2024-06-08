using Generatr.Abstractions;

namespace Generatr.Builders;

public abstract class ReturnValueBuilder : IBuilder
{
    public abstract void Build(TabbedBuilder tb);
}