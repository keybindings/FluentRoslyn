using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Generatr.Enums;

namespace Generatr.Builders;

public class NamespaceBuilder : Builder
{
    private static readonly HashSet<char> InvalidChars = new(){' '};
    private NamespaceBuilder(NamespaceBuilder parent, string name) : base(name)
    {
        Parent = parent;
    }

    public NamespaceBuilder Parent { get; }

    public static NamespaceBuilder New(string name) => New(null!, name);

    public NamespaceBuilder Child(string name) => New(this, name);
    public ClassBuilder Class(string name, AccessModifiers accessModifier = AccessModifiers.Public)
        => new(this, name, accessModifier);
    
    protected override string Build()
    {
        // Go back through hierarchy
        var target = this;
        var firstFlag = true;
        var sb = new StringBuilder();
        while (target != null)
        {
            if (!firstFlag) sb.Insert(0, '.');
            // add point for as you go back up the hierarchy through namespaces
            sb.Insert(0, target.Name);
            firstFlag = false;
            target = target.Parent;
        }

        return sb.ToString();
    }

    private static NamespaceBuilder New(NamespaceBuilder parent, string name)
    {
        ContainsInvalidCharsCheck(name);
        return new(parent, name);
    }

    private static void ContainsInvalidCharsCheck(string val)
    {
        if (val.Any(IsInvalidChar))
            throw new ArgumentException(
                $"Name cannot contain invalid chars: {string.Join(", ", InvalidChars.Select(x => $"\"{x}\""))}");
    }

    public static bool IsInvalidChar(char c) => InvalidChars.Contains(c);
}