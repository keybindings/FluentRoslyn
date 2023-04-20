using Generatr.Enums;

namespace Generatr.Builders;

public class FieldBuilder
{
    internal FieldBuilder(ClassBuilder @class, ClassBuilder type, string name, StandardAccessModifier accessModifierFlags)
    {
        Class = @class;
        Type = type;
        Name = name;
        AccessModifierFlags = accessModifierFlags;
    }

    public ClassBuilder Class { get; }

    public StandardAccessModifier AccessModifierFlags { get; set; }

    public ClassBuilder Type { get; set; }
    public string Name { get; }
}