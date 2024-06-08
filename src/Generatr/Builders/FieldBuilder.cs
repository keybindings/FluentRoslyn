using System;
using Generatr.Abstractions;
using Generatr.Builders.KeywordBuilders;

namespace Generatr.Builders;

public class FieldBuilder<T> : FieldBuilder
{
    internal FieldBuilder(ClassBuilder @class, string name, AccessModifier accessModifier) : base(@class, TypeNameBuilder.New<T>(), name, accessModifier)
    {
    }


    protected override void BuildStaticInitialization(TabbedBuilder tb)
    {
    }

    public class StaticInitializationBuilder : IBuilder
    {
        public StaticInitializationBuilder(FieldBuilder<T> fb)
        {

        }

        public void Build(TabbedBuilder tb)
        {
            throw new NotImplementedException();
        }
    }
}

public abstract class FieldBuilder(
    ClassBuilder @class,
    TypeNameBuilder typeName,
    string name,
    AccessModifier accessModifier)
    : NamedBuilder(name, NameValidation), IAccessModifier
{
    private readonly OptionalKeyword _staticBuilder = OptionalKeyword.Static;
    private readonly OptionalKeyword _readonlyBuilder = OptionalKeyword.Readonly;

    public bool IsReadonly { get => _readonlyBuilder.IsSet; set => _readonlyBuilder.IsSet = value; }

    public bool IsStatic { get => _staticBuilder.IsSet; set => _staticBuilder.IsSet = value; }

    public ClassBuilder Class { get; } = @class;

    public AccessModifier AccessModifier { get; set; } = accessModifier;

    public override void Build(TabbedBuilder tb)
    {
        AccessModifier.Build(tb);
        tb.Space();
        _staticBuilder.Build(tb);
        _readonlyBuilder.Build(tb);
        typeName.Build(tb);
        tb.Space();
        base.Build(tb);
        BuildStaticInitialization(tb);
        tb.SemiColon();
    }

    protected abstract void BuildStaticInitialization(TabbedBuilder tb);

    private static void NameValidation(string name)
    {

    }
}