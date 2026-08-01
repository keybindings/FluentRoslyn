using Generatr.Builders;

namespace Generatr.Abstractions;

internal interface IParameter : INamedBuilder
{
    TypeNameBuilder TypeName { get; }
}
