using FluentRoslyn.Example.ValueObjects;

namespace FluentRoslyn.Example.ValueObjects.App;

// Each of these is one half of a type. The generator writes the other half: the
// constructor, the Value property, and the equality and formatting members. The member
// set is identical for both — only the name and the wrapped type differ.

/// <summary>An order identifier, distinct from any other int.</summary>
[ValueObject(typeof(int))]
public readonly partial struct OrderId
{
}

/// <summary>A customer code, distinct from any other string.</summary>
[ValueObject(typeof(string))]
public readonly partial struct CustomerCode
{
}
