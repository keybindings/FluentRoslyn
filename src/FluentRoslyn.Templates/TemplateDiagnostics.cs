using Microsoft.CodeAnalysis;

namespace FluentRoslyn.Templates;

/// <summary>
/// The diagnostics the lifter reports. Every unsupported template is reported rather
/// than skipped: a skipped template compiles fine and simply produces no emitter, so
/// the failure would surface as a missing method somewhere else entirely.
/// </summary>
internal static class TemplateDiagnostics
{
    private const string Category = "FluentRoslyn.Templates";

    internal static readonly DiagnosticDescriptor MustBeStatic = new(
        "FRT001",
        "Template method must be static",
        "Template method '{0}' must be static. A template is lifted, never invoked, so it has no instance to run on.",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor ContainingTypeMustBePartial = new(
        "FRT002",
        "Type containing a template must be static and partial",
        "Type '{0}' contains a template, so it must be declared 'static partial'. The emitter methods are generated into the other half.",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor NeedsExpressionBody = new(
        "FRT003",
        "Template must have an expression body",
        "Template method '{0}' must have an expression body ('=> …'). Statement bodies are not lifted yet.",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor NoGenerics = new(
        "FRT004",
        "Template cannot be generic",
        "Template method '{0}' declares type parameters. The emitted builder call has nothing to supply them from.",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
