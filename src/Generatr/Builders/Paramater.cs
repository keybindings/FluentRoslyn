using System;
using Generatr.Abstractions;

namespace Generatr.Builders;

public class Paramater<T> : NamedBuilder, IParameter
{
    private Paramater(string name) : base(name, NameValidation)
    {
        TypeName = TypeNameBuilder.New<T>();
    }

    public static IParameter New(string name) => new Paramater<T>(name);

    public TypeNameBuilder TypeName { get; }

    public override void Build(TabbedBuilder tb)
    {
        TypeName.Build(tb);
        tb.Space();
        base.Build(tb);
    }

    private static void NameValidation(string name)
    {

    }
}