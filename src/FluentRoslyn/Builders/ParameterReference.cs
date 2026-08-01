using FluentRoslyn.Abstractions;

namespace FluentRoslyn.Builders;

/// <summary>
/// A reference to a parameter, handed back by <c>WithParameter&lt;T&gt;(name, out …)</c>.
/// The name has already been validated by the parameter it was created from.
/// </summary>
internal sealed class ParameterReference<T>(string name) : IReference<T>, IReferenceInfo
{
    public string Name { get; } = name;

    ReferenceKind IReferenceInfo.Kind => ReferenceKind.Parameter;

    bool IReferenceInfo.IsStaticMember => false;
}
