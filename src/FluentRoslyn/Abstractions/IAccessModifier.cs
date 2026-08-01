using FluentRoslyn.Builders;

namespace FluentRoslyn.Abstractions;

/// <summary>
/// A member that carries an accessibility level. Used internally to order members by
/// accessibility when emitting a type — consistent with the other member-plumbing
/// interfaces (<see cref="IMemberSyntaxBuilder"/>, <see cref="IParameter"/>).
/// </summary>
internal interface IAccessModifier
{
    AccessModifier AccessModifier { get; set; }
}
