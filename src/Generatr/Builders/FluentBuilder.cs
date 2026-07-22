using System;

namespace Generatr.Builders;

internal static class FluentBuilder
{
    /// <summary>
    /// Runs a mutation and returns the same builder, so fluent setters can be written
    /// as <c>=&gt; this.With(() =&gt; Field = value)</c> instead of each builder carrying
    /// its own copy of the mutate-and-return-this helper.
    /// </summary>
    internal static TBuilder With<TBuilder>(this TBuilder builder, Action mutate)
    {
        mutate();
        return builder;
    }
}
