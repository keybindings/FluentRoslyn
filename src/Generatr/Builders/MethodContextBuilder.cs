using System.Collections.Generic;
using Generatr.Abstractions;

namespace Generatr.Builders;

internal class MethodContextBuilder : IBuilder
{
    internal List<StatementBuilder> Builders = new();

    public void Build(TabbedBuilder tb)
    {
        foreach (var b in Builders)
        {
            b.Build(tb);
        }
    }


}