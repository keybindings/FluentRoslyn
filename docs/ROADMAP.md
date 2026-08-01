# Roadmap

Status as of 2026-08-01. Ordered by priority; value and effort are relative to
each other, not absolute.

FluentRoslyn is feature-complete for common source-generator scenarios and fully
tested. The remaining work splits into **shipping** (making it usable by other
people) and **long-tail language features** (add when a real generator needs
them, not speculatively).

## Priority table

| # | Item | Category | Value | Effort | Notes |
|---|------|----------|:---:|:---:|-------|
| ~~1~~ | ~~**NuGet packaging**~~ | Ship | High | Low | **Done** (2026-08-01). Full metadata, MIT license, README/LICENSE packed, XML docs shipped. Verified: dependency group is empty, so Roslyn does not flow to consumers. Not yet *published* to nuget.org. |
| ~~2~~ | ~~**CI pipeline**~~ | Ship | Med | Low | **Done** (2026-08-01). `.github/workflows/ci.yml` — restore, build, test, run the example end-to-end (asserts on output), pack, upload artifact. |
| ~~3~~ | ~~**Method modifiers** — `virtual`/`abstract`/`override`/`sealed`~~ | Feature | Med | Low | **Done** (2026-08-01). Modelled as an `Inheritance` enum (mutually exclusive by construction) rather than independent bools. Classes also gained `Abstract()`/`Sealed()`. |
| ~~4~~ | ~~**XML doc comments on the public API**~~ | Ship | Med | Med | **Done** (2026-08-01). Full coverage, enforced by `WarningsAsErrors;CS1591`. The pass also tightened members that were public but externally unreachable. |
| ~~5~~ | ~~**Using-directive management**~~ | Feature | Med | High | **Done** (2026-08-01). Opt-in `SimplifyTypeNames()` plus explicit `WithUsing(name)`. Builders annotate the nodes they emit so the simplifier knows namespace-vs-nested-type without semantics; ambiguous and self-declared names stay qualified. |
| ~~6~~ | ~~**`async` methods**~~ | Feature | Med | Low | **Done** (2026-08-01). `Async()` on MethodBuilder. Return-type guard rejects only non-awaitable built-ins, so custom awaitables pass without an allowlist. |
| ~~7~~ | ~~**Nested types** (type inside a type)~~ | Feature | Med | Med | **Done** (2026-08-01). All five kinds nestable in a class or struct, any depth. Nested types qualify through their declaring type (`Ns.Outer.Inner`) and emit standalone as a bare declaration. |
| ~~8~~ | ~~**`required` members** (C# 11)~~ | Feature | Low | Low | **Done** (2026-08-01). `Required()` on fields and properties, guarded against static/const and get-only, which can never satisfy the requirement. |
| ~~9~~ | ~~**Record inheritance** (`: Base(args)`)~~ | Feature | Low | Med | **Done** (2026-08-01). `WithParent` forwards arguments to the base primary constructor via `PrimaryConstructorBaseType`; base emitted before interfaces. |
| ~~10~~ | ~~**Emit `///` doc comments on generated members**~~ | Feature | Med | Med | **Done** (2026-08-01). `WithSummary` everywhere, plus `WithParameterDoc`/`WithReturnsDoc` on methods and constructors. Emitted as plain comment trivia so `NormalizeWhitespace` cannot reformat the XML attributes; text is XML-escaped. |
| ~~11~~ | ~~**Events / delegates**~~ | Feature | Low | Med each | **Done** (2026-08-01). `DefineEvent<THandler>` (field-like events, ordered after constructors) and `NamespaceBuilder.Delegate` / `DefineDelegate` for nested. |
| ~~12~~ | ~~**Attribute target specifiers** (`[return:]`, `[field:]`)~~ | Feature | Low | Low | **Done** (2026-08-01). Targets split off before parsing; an unrecognised one is rejected rather than silently dropped, and named arguments are not mistaken for targets. |
| ~~13~~ | ~~**Configurable formatting** (indent/eol)~~ | Polish | Low | Med | **Done** (2026-08-01). `WithIndentation`/`WithLineEndings`, strictly opt-in — the 4-space/LF default is unchanged, so byte-identical cross-OS output is still what you get for free. |
| ~~14~~ | ~~**Typed references + `Assign`**~~ | Feature | High | Med | **Done** (2026-08-02). `IReference<T>`; `PropertyBuilder<T>`/`FieldBuilder<T>` are references directly, parameters yield one via `WithParameter<T>(name, out …)` so chaining survives. `Assign` type-matches both sides at generator compile time, and qualifies with `this.` when a parameter shadows the member — otherwise `name = name;` would silently self-assign. |

## Reading of the table

- **#1–#4 are done** — the whole shippable story. The package builds with full
  API docs, CI is green, and the inheritance modifiers are in. The one remaining
  step to actual availability is publishing to nuget.org (needs an API key and a
  decision on whether 0.1.0 goes out as a preview).
- **#5, #6, #7 and #10 are done** — usings, async methods, nested types, and doc
  comments on generated output. That clears every Medium-value item.
- **Every item on this roadmap is now done.** The list is kept as a record of
  what was built and why.
- **New work should come from a real generator's needs**, not from a
  speculative list. #14 is the first item added that way: writing a constructor
  by hand made it obvious that `AddStatement("Name = name;")` is the one place
  the library still lets you emit silently wrong code.
- **Publishing is the remaining step.** The package is prepared as
  `FluentRoslyn` version `0.1.0-preview.1` — the original name `Generatr` collides
  with an unrelated database scaffolder on nuget.org, and ids are
  case-insensitive. Pushing needs a nuget.org API key. Note that package
  metadata is immutable once pushed, so settle the repository URL (i.e. any
  GitHub account rename) *before* the first push.

## Deliberate decisions (not gaps)

These look like omissions but are choices:

- **Fully-qualified type names by default.** Correct without any collision
  analysis. `SimplifyTypeNames()` (#5) opts into shortened names plus imports;
  it stays opt-in so the safe behaviour is what you get for free.
- **4-space indentation and `\n` line endings by default.** Guarantees output is
  byte-identical across operating systems, so generated files hash the same
  everywhere. `WithIndentation`/`WithLineEndings` (#13) override it per builder;
  they stay opt-in so the reproducible behaviour is the free one.
- **No trailing newline** after the final `}` of a compilation unit — this is
  what Roslyn's `NormalizeWhitespace` produces; tests pin the actual behaviour.
- **Raw-string escape hatches** for statements/expressions/constraints, rather
  than a fluent expression model. Malformed fragments are rejected at build
  time, so they cannot silently emit broken source. A fluent expression tree is
  a much larger project and may never be worth it. **Narrowed by #14:**
  assignment now has a typed form (`Assign`), because it was the one statement
  common enough — and error-prone enough — to justify the machinery. Every other
  statement is still raw text.
- **`Parameter<T>` / `IParameter` stay internal.** Parameters are still added
  only via `WithParameter<T>(name)`; the #14 overload hands back an
  `IReference<T>`, which is a name and a phantom type, not the parameter builder.
- **`IReference<T>` is invariant.** C# generic constraints cannot express
  "implicitly convertible to", so exact matching is the only contract that can
  actually be enforced. Covariance would let `Assign(stringTarget, objectValue)`
  compile by inferring a common base — the exact bug the type parameter exists
  to catch. Widening (`object` ← `string`, `long` ← `int`) falls back to
  `AddStatement`.

## Known constraints

- Targets `netstandard2.0` so the library is referenceable from source
  generators; this bounds available BCL APIs.
- Requires `Microsoft.CodeAnalysis.CSharp` 4.9.2+ on the consuming side, with
  `PrivateAssets="all"` so Roslyn does not flow to consumers.
