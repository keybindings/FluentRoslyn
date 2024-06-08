using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Generatr.Abstractions;

namespace Generatr.Builders;

public class NamespaceBuilder : NamedBuilder
{
    public static readonly NamespaceBuilder None = new(string.Empty, _ => {});
    private NamespaceBuilder(NamespaceBuilder parent, string name) : base(name, NameValidation)
    {
        Parent = parent;
    }

    // None Builder
    private NamespaceBuilder(string name, Action<string> validation) : base(name, validation)
    {
        Parent = this;
    }

    public NamespaceBuilder Parent { get; }

    public static NamespaceBuilder Get(string name) => New(None, name);

    public NamespaceBuilder Child(string name) => New(this, name);

    private static NamespaceBuilder New(NamespaceBuilder parent, string name)
    {
        if (name == null) throw new ArgumentNullException(nameof(name));

        var levels = name.Split('.');

        var target = new NamespaceBuilder(parent, levels[0]);

        if (levels.Length == 1) return target;

        for (var i = 1; i < levels.Length; i++)
        {
            target = New(target, levels[i]);
        }

        return target;
    }

    public ClassBuilder Class(string name)
        => new(this, name);

    public override void Build(TabbedBuilder tb)
    {
        PrivateBuild(tb);
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        var tb = new TabbedBuilder(sb);
        PrivateBuild(tb);
        return tb.ToString();
    }

    private void PrivateBuild(TabbedBuilder sb)
    {
        var first = true;
        foreach (var n in GetNames().Reverse())
        {
            if (!first) sb.Period();
            sb.Append(n);
            first = false;
        }
    }

    private IEnumerable<string> GetNames()
    {
        var target = this;
        while (target != None)
        {
            yield return target.Name;
            target = target.Parent;
        }
    }

    internal static void NameValidation(string name)
    {

    }
}