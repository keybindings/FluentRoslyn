using FluentRoslyn.Example.Decorator;

namespace FluentRoslyn.Example.Decorator.App;

/// <summary>
/// A consumer interface. The generator sees it only as an ISymbol, and emits a
/// forwarding implementation that logs around every member.
/// </summary>
[GenerateDecorator]
public interface IGreeter
{
    string Greet(string name);

    int Count { get; }

    void Reset();
}

/// <summary>The real implementation the decorator wraps.</summary>
public sealed class Greeter : IGreeter
{
    private int _count;

    public string Greet(string name)
    {
        _count++;
        return $"Hello, {name}";
    }

    public int Count => _count;

    public void Reset() => _count = 0;
}
