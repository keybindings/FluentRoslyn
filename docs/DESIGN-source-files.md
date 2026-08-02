# Source files — design

Status as of 2026-08-02. Proposes a `SourceFile` builder to own what is currently
owned by whichever type happens to be top-level. Not built; the API shape wants
agreement first, because this is the library's first breaking change since
publishing.

## The problem

**A top-level type *is* a file.** `NamespaceBuilder.Get(ns).Class(name)` returns a
type builder that wraps *itself* in a compilation unit. There is no container
holding several types, so one-type-per-file is not a convention here — it is the
only thing expressible.

The constraint is one line, in `NamespaceBuilder.CompilationUnitFor`:

```csharp
var body = SingletonList(member);
```

Two consequences follow.

**① You cannot put two types in one file.** A class and its options record, an
interface and its implementation, a type and a private helper — all common, none
expressible. The only workaround is `BuildCompilationUnit()` and merging syntax by
hand, which forfeits everything below.

**② File-level concerns live on type builders — 27 public methods across six
kinds.** `WithUsing`, `SimplifyTypeNames`, `BlockScopedNamespace`,
`WithIndentation`, `WithLineEndings` are each repeated on `TypeBuilder<TSelf>`,
`EnumBuilder`, `RecordBuilder`, `InterfaceBuilder`, and `DelegateBuilder`. Every
one describes a *file*. They sit on types because there is nowhere else to put
them.

`TypeImports` — the class that actually models usings-plus-simplification — is
already file-shaped. It is simply held by `TypeDeclarationBuilder` instead of by a
file.

## What is already correct

Worth stating before proposing changes, because it removes the part that looked
risky.

**The simplifier is already file-scoped.** `TypeNameSimplifier.Simplify` takes a
whole `CompilationUnitSyntax`; it groups annotated candidates across the entire
unit and derives shadowing from `DeclaredTypeNames(unit)`, which walks *every*
`BaseTypeDeclarationSyntax` in it:

```csharp
var candidates = unit.GetAnnotatedNodes(AnnotationKind)…
var declared   = DeclaredTypeNames(unit);
…
if (namespaces.Count != 1 || declared.Contains(group.Key)) continue;
```

So joint ambiguity analysis across several types in one file — the thing that
*must* be right and would be easy to get wrong — needs **no changes**. Both rules
already operate on the file, not the type. The simplifier has simply never been
handed more than one type.

That reduces this from "rework the simplifier and restructure the builders" to
"restructure the builders". The correctness-critical half is done.

## Proposed shape

```csharp
var file = SourceFile.InNamespace("MyApp.Models")
    .SimplifyTypeNames()
    .WithUsing("System.Linq");

var user    = file.Class("User");
var options = file.Record("UserOptions");

context.AddSource("Users.g.cs", file.ToSourceText());
```

`SourceFile` owns, and the six type builders lose:

| Concern | Today | Proposed |
|---|---|---|
| Namespace | on each type builder | `SourceFile.InNamespace(…)` |
| Usings (`TypeImports`) | one per type builder | one per file |
| Simplification | per type | per file |
| File-scoped vs block namespace | per type | per file |
| Indentation / line endings | per type | per file |
| `ToString` / `ToSourceText` / `BuildCompilationUnit` | per type | per file |

`CompilationUnitFor` takes a collection rather than a single member. Nested types
are unaffected — they are built through their declaring type and never own a file.

## Compatibility

`NamespaceBuilder.Get(ns).Class(name)` should keep working, creating an implicit
single-type file behind the scenes, so the common case and every existing example
survive. What breaks is calling the *file-level* methods on a type builder:

```csharp
NamespaceBuilder.Get("MyApp").Class("User").SimplifyTypeNames();   // was fine
```

Two options, and this is the main thing to decide:

- **(a) Remove them from type builders.** Honest — a type genuinely does not have
  usings — and stops the API implying that two types in one file could disagree
  about imports. Breaks 52 call sites across 11 test files, plus the README and
  the example generator.
- **(b) Keep them as deprecated forwarders** that configure the implicit file, with
  `[Obsolete]` pointing at `SourceFile`. Nothing breaks; the API carries two ways
  to do one thing until a later major version removes them.

**Recommended: (a).** The package is `0.1.0-preview`; a preview is exactly when
this is cheap, and forwarders that silently configure a hidden file are the kind of
thing that reads fine until two types share a file and one of them "loses" its
settings. Better to have the compiler point at every call site once.

## What this does *not* fix

The simplifier still only sees types it was told about through annotations. Types
named only inside raw strings — `AddStatement("var x = new System.Text.StringBuilder();")`
— are invisible to it and stay fully qualified, with no import added. `WithAttribute`
is in the same position, which is why an attribute can stay qualified in a file that
already imports its namespace. That is the same typed-versus-raw seam as everywhere
else, and it is unchanged by this proposal.

## Open decisions

1. **Compatibility:** (a) remove the file-level methods from type builders, or (b)
   keep deprecated forwarders? *Recommended: (a).*
2. **Entry point:** `SourceFile.InNamespace("…")`, or keep `NamespaceBuilder` as the
   root and add `NamespaceBuilder.Get("…").File()`? The former reads better and
   makes the file the obvious unit; the latter is a smaller change to the mental
   model.
3. **Scope:** does this land before or after the remaining statement work
   (reference paths, computed values)? It is a bigger change than either, and it
   touches every test that asserts on emitted output.

## Sizing

- 27 public methods removed from six builders, replaced by one set on `SourceFile`.
- `TypeImports` and the formatting fields move from `TypeDeclarationBuilder` up to
  `SourceFile`.
- `CompilationUnitFor` takes a collection.
- 52 call sites across 11 test files, plus the README's examples and the example
  generator.
- `TypeNameSimplifier`: **no change**.
