using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Generatr.Extensions;

namespace Generatr.Builders;

public class NamespaceBuilder : Builder
{
    private static readonly HashSet<char> InvalidChars = new(){' '};
    private NamespaceBuilder(NamespaceBuilder parent, string name) : base(name)
    {
        ContainsInvalidCharsCheck(name);
        Parent = parent;
    }

    public NamespaceBuilder Parent { get; }

    public static NamespaceBuilder New(string name) => New(null, name);

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

    //public ClassBuilder Class(string name, AccessModifiers accessModifier = AccessModifiers.Public)
    //    => new(this, name, accessModifier);
    
    protected override string Build()
    {
        // Go back through hierarchy
        var target = this;
        var sb = new StringBuilder(Name);
        target = target.Parent;

        while (target != null)
        {
            sb.Insert(0, '.');
            sb.Insert(0, target.Name);
            target = target.Parent;
        }

        return sb.ToString();
    }
    private static void ContainsInvalidCharsCheck(string val) =>
        val.Any(IsInvalidChar).Then(() => throw new ArgumentException(
            $"Name cannot contain invalid chars: {string.Join(", ", InvalidChars.Select(x => $"\"{x}\""))}"));

    public static bool IsInvalidChar(char c) => InvalidChars.Contains(c);
}