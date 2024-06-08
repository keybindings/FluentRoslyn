namespace Generatr.Abstractions;

public interface INamedBuilder : IBuilder
{
    string Name { get; }

}