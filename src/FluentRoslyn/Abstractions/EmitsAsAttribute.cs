using System;

namespace FluentRoslyn.Abstractions;

/// <summary>
/// Marks a type in the generator's own assembly as a compile-time stand-in for a type
/// the generator emits. Anywhere the marked type is used as a type argument —
/// <c>DefineProperty&lt;T&gt;</c>, <c>WithParameter&lt;T&gt;</c>,
/// <c>WithInterface&lt;T&gt;</c>, <c>IReference&lt;T&gt;</c> — the <em>emitted</em> name
/// is written instead of the placeholder's own.
/// </summary>
/// <remarks>
/// This is what makes the typed surface usable for types that do not exist as CLR types
/// because the generator is the thing creating them. The placeholder never ships to
/// consumers; it exists so the C# compiler can check consistency between the definition
/// and every reference at generator-compile time. The name must be a plain
/// namespace-qualified identifier — no generics, arrays, or nesting markers.
/// </remarks>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface |
    AttributeTargets.Enum | AttributeTargets.Delegate,
    Inherited = false)]
public sealed class EmitsAsAttribute : Attribute
{
    /// <summary>Marks the type as emitting under <paramref name="fullTypeName"/>.</summary>
    /// <param name="fullTypeName">
    /// The namespace-qualified name the generated type will have, e.g.
    /// <c>"MyApp.Models.User"</c>. A bare identifier places it in the global namespace.
    /// </param>
    public EmitsAsAttribute(string fullTypeName)
    {
        FullTypeName = fullTypeName ?? throw new ArgumentNullException(nameof(fullTypeName));
    }

    /// <summary>The namespace-qualified name the generated type will have.</summary>
    public string FullTypeName { get; }
}
