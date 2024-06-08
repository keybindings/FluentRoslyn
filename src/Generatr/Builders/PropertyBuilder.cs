using System;
using Generatr.Abstractions;
using Generatr.Builders.KeywordBuilders;

namespace Generatr.Builders;

public class PropertyBuilder<T> : PropertyBuilder
{
    private readonly FieldBuilder<T> _backingField = null; // Only Set if not autoproperty
    private readonly OptionalKeyword _staticBuilder = OptionalKeyword.Static;
    private readonly TypeNameBuilder _typeName = TypeNameBuilder.New<T>();
    private readonly GetMethodBuilder _getMethodBuilder;
    public PropertyBuilder(ClassBuilder @class, string name, AccessModifier accessModifier) : base(@class, name, accessModifier)
    {
    }

    public override bool IsStatic { get => _staticBuilder.IsSet; set => _staticBuilder.IsSet = value; }

    public override void Build(TabbedBuilder tb)
    {
        AccessModifier.Build(tb);
        _staticBuilder.Build(tb);
        _typeName.Build(tb);
        base.Build(tb);

        if (!IsAutoProperty) throw new NotImplementedException();

        tb.OpenBracket();
        tb.Space();
        _getMethodBuilder.Build(tb);
    }

    public class GetMethodBuilder : INamedBuilder
    {
        private readonly PropertyBuilder<T> _prop;

        public GetMethodBuilder(PropertyBuilder<T> prop)
        {
            _prop = prop;
        }

        public string Name { get; }
        public void Build(TabbedBuilder tb)
        {
            throw new NotImplementedException();
        }
    }

}



public abstract class PropertyBuilder(ClassBuilder @class, string name, AccessModifier accessModifier) : NamedBuilder(name, NameValidation)
{
    public ClassBuilder Class { get; } = @class;

    public abstract bool IsStatic { get; set; }

    public bool HasGet { get; } = true;

    public bool HasSet { get; } = true;

    public bool IsAutoProperty { get; set; } = true;

    public AccessModifier AccessModifier { get; set; } = accessModifier;


    private static void NameValidation(string name)
    {

    }
}