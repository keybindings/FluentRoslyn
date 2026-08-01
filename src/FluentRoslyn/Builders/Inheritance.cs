namespace FluentRoslyn.Builders;

/// <summary>
/// A member's inheritance modifier. These are mutually exclusive in C# — a member
/// cannot be both <c>virtual</c> and <c>abstract</c>, for example — so they are modelled
/// as one choice rather than independent flags, which makes the invalid combinations
/// unrepresentable. <c>sealed</c> is only legal alongside <c>override</c>, hence
/// <see cref="SealedOverride"/> rather than a standalone sealed option.
/// </summary>
public enum Inheritance
{
    /// <summary>No inheritance modifier.</summary>
    None = 0,

    /// <summary><c>virtual</c> — may be overridden by a derived type.</summary>
    Virtual,

    /// <summary><c>abstract</c> — no body; the derived type must supply one.</summary>
    Abstract,

    /// <summary><c>override</c> — replaces a base member.</summary>
    Override,

    /// <summary><c>sealed override</c> — overrides a base member and blocks further overriding.</summary>
    SealedOverride,
}
