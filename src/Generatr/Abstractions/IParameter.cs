using Generatr.Builders;

namespace Generatr.Abstractions;

public interface IParameter : INamedBuilder
{
    TypeNameBuilder TypeName { get; }
}
