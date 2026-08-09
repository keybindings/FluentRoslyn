using System;
using System.Collections.Generic;
using FluentRoslyn.Abstractions;

namespace FluentRoslyn.Builders;

/// <summary>
/// What a typed handle asserts, and what has to be true for the assertion to hold. Shared
/// by <c>AsCallable</c>, <c>AsCallableOn</c>, <c>AsFunction</c>, <c>AsConstructable</c> and
/// <c>This&lt;T&gt;</c>, which all make the same two claims: that a type argument names the
/// declaring type, and that the asserted signature is the declared one.
/// </summary>
/// <remarks>
/// Both rules used to be written out per member kind — the pairing rule three times with
/// three message texts, the signature check twice. That is how they drifted: the method
/// side gained an order-proof re-check and the constructor side did not, and each new
/// hole had to be found separately. One copy means a rule added here reaches every handle
/// family at once.
/// </remarks>
internal static class HandleRules
{
    /// <summary>
    /// The pairing rule the whole receiver-checking story rests on: a placeholder's
    /// emitted name and the declaring type's qualified name are the same string, because
    /// that is what both become in the generated source. No registry is needed, and none
    /// could be — the placeholder is a CLR type and the declaring type is not.
    /// </summary>
    /// <param name="subject">How the caller names itself, e.g. <c>Method 'Foo'</c>.</param>
    /// <param name="declaringType">The type the member is declared on.</param>
    /// <param name="asserted">The type argument the caller supplied.</param>
    internal static void AssertDeclaringType(
        string subject, TypeDeclarationBuilder declaringType, Type asserted)
    {
        // Both sides render the same way. They did not: one went through
        // NamedBuilder.ToString(), which normalizes whitespace, and the other through a
        // plain ToString() on the syntax node -- so two spellings of one type could
        // differ by a space that neither caller wrote.
        var assertedName = TypeNameBuilder.New(asserted).BuildTypeSyntax().ToString();
        var declaredName = declaringType.BuildTypeSyntax().ToString();

        if (!string.Equals(assertedName, declaredName, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"{subject} is declared on '{declaredName}', but the handle asserts '{assertedName}'. " +
                "The type argument must name the declaring type — its [EmitsAs] placeholder when " +
                "that type is being generated.");
    }

    /// <summary>
    /// Validates the signature a handle asserts against the one the member declares.
    /// </summary>
    /// <param name="subject">How the caller names itself, e.g. <c>Method 'Foo'</c>.</param>
    /// <param name="accessibility">The member's accessibility.</param>
    /// <param name="isGeneric">Whether the member declares type parameters of its own.</param>
    /// <param name="declared">The member's declared parameters.</param>
    /// <param name="asserted">The types the handle asserts, in order.</param>
    internal static void AssertSignature(
        string subject,
        AccessModifier accessibility,
        bool isGeneric,
        IReadOnlyList<IParameter> declared,
        Type[] asserted)
    {
        RefuseUnreachable(subject, accessibility);

        // A call through a handle emits `target.Name(args)` with no type-argument list,
        // and nothing here can supply one: the handle carries argument types, not type
        // arguments. Inference would have to succeed in the consumer's compilation, which
        // is exactly what this library refuses to bet on -- CS0411 when it does not.
        if (isGeneric)
            throw new InvalidOperationException(
                $"{subject} declares type parameters, so a handle cannot name it: a call through the " +
                "handle would supply no type arguments. Emit the call with the raw call family instead.");

        if (declared.Count != asserted.Length)
            throw new InvalidOperationException(
                $"{subject} declares {declared.Count} parameter(s) but the handle asserts {asserted.Length}.");

        for (var i = 0; i < asserted.Length; i++)
        {
            var assertedType = TypeNameBuilder.New(asserted[i]).ToString();
            var declaredType = declared[i].TypeName.ToString();

            if (!string.Equals(assertedType, declaredType, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"{subject} parameter {i + 1} ('{declared[i].Name}') is '{declaredType}', " +
                    $"but the handle asserts '{assertedType}'.");
        }
    }

    /// <summary>
    /// Refuses a handle to a member that cannot be reached from wherever the handle ends
    /// up being used.
    /// </summary>
    /// <remarks>
    /// A handle exists to be carried to another builder and called there, and the library
    /// deliberately does not track where — <c>Call(target, handle, …)</c> can be emitted
    /// from any body in any file. So the only members it can honestly issue one for are
    /// the ones reachable from anywhere in the assembly the generator emits into:
    /// <c>public</c>, <c>internal</c>, and <c>protected internal</c> (which is
    /// protected-<em>or</em>-internal, so the internal half carries it). <c>protected</c>,
    /// <c>private protected</c> and <c>private</c> are all reachable only from somewhere
    /// specific, and CS0122 in the consumer's build anywhere else.
    /// </remarks>
    private static void RefuseUnreachable(string subject, AccessModifier accessibility)
    {
        if (ReferenceEquals(accessibility, AccessModifier.Public)
            || ReferenceEquals(accessibility, AccessModifier.Internal)
            || ReferenceEquals(accessibility, AccessModifier.ProtectedInternal))
            return;

        throw new InvalidOperationException(
            $"{subject} is '{Spelling(accessibility)}', so a handle cannot be issued for it: a call " +
            "through the handle can be emitted anywhere, and this one is only reachable from inside " +
            "its own type or a derived one. Widen it to internal or public, or emit the call with " +
            "the raw call family.");
    }

    // AccessModifier.None spells as the empty string, which reads as a missing word.
    private static string Spelling(AccessModifier accessibility)
        => ReferenceEquals(accessibility, AccessModifier.None)
            ? "implicitly private"
            : accessibility.ToString();
}
