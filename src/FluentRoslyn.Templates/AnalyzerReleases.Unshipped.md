; Unshipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|--------------------
FRT001 | FluentRoslyn.Templates | Error | A [Template] method must be static.
FRT002 | FluentRoslyn.Templates | Error | A type containing a template must be static and partial.
FRT003 | FluentRoslyn.Templates | Error | A template must have an expression body.
FRT004 | FluentRoslyn.Templates | Error | A template cannot declare type parameters.
