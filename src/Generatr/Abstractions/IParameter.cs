using Generatr.Builders;

namespace Generatr.Abstractions;

public interface IParameter : INamedBuilder
{
    public TypeNameBuilder TypeName { get; }

}