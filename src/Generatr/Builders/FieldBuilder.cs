using Generatr.Enums;

namespace Generatr.Builders;

public class FieldBuilder
{
    internal FieldBuilder(ClassBuilder @class, ClassBuilder type, string name, AccessModifiers accessModifier)
    {
        Class = @class;
        Type = type;
        Name = name;
        AccessModifier = accessModifier;
    }

    public ClassBuilder Class { get; }

    public AccessModifiers AccessModifier { get; set; }

    public ClassBuilder Type { get; set; }
    public string Name { get; }
}