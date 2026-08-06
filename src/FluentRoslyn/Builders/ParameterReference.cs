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

/// <summary>
/// A reference to a parameter whose type was given as text, handed back by
/// <c>WithParameter(name, typeName, out …)</c>. Carries the declared type so an
/// assignment against another raw-typed reference can still compare the two.
/// </summary>
internal sealed class RawParameterReference(string name, string typeText)
    : IRawReference, IReferenceInfo, IRawTypeInfo
{
    public string Name { get; } = name;

    ReferenceKind IReferenceInfo.Kind => ReferenceKind.Parameter;

    bool IReferenceInfo.IsStaticMember => false;

    string IRawTypeInfo.TypeText { get; } = typeText;
}
