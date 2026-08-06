namespace FluentRoslyn.Builders;

/// <summary>What a reference points at, which decides whether it can be shadowed.</summary>
internal enum ReferenceKind
{
    /// <summary>A member of the declaring type — a property or a field.</summary>
    Member,

    /// <summary>A parameter of the enclosing method or constructor.</summary>
    Parameter,
}

/// <summary>
/// The details a reference must expose to be assigned to correctly. Kept internal so the
/// public <see cref="Abstractions.IReference{T}"/> surface stays a bare name.
/// </summary>
internal interface IReferenceInfo
{
    /// <summary>What the reference points at.</summary>
    ReferenceKind Kind { get; }

    /// <summary>Whether the referenced member is <c>static</c>, which rules out <c>this.</c>.</summary>
    bool IsStaticMember { get; }
}

/// <summary>
/// The declared type of a reference whose type came from text, so two of them can be
/// compared. Internal because the comparison is the library's business: the public
/// contract is that a mismatch throws, not how the match is decided.
/// </summary>
internal interface IRawTypeInfo
{
    /// <summary>The declared type as it will be emitted.</summary>
    string TypeText { get; }
}
