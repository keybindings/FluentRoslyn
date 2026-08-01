using FluentRoslyn.Builders;

namespace FluentRoslyn.Abstractions;

internal interface IParameter : INamedBuilder
{
    TypeNameBuilder TypeName { get; }
}
