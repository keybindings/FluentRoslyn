using Generatr.Builders;
using Generatr.Enums;

namespace Generatr.Constants;

public class StringType : ClassBuilder
{
    internal StringType() : base(Namespaces.System, "string", AccessModifiers.Public)
    { }
}