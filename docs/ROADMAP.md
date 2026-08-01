# Roadmap

Status as of 2026-08-01. Ordered by priority; value and effort are relative to
each other, not absolute.

Generatr is feature-complete for common source-generator scenarios and fully
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
| 5 | **Using-directive management** | Feature | Med | High | Output is fully qualified today (`System.Console`). Real payoff, but needs import collection + dedup + collision handling — a genuine subsystem. |
| ~~6~~ | ~~**`async` methods**~~ | Feature | Med | Low | **Done** (2026-08-01). `Async()` on MethodBuilder. Return-type guard rejects only non-awaitable built-ins, so custom awaitables pass without an allowlist. |
| 7 | **Nested types** (type inside a type) | Feature | Med | Med | Real generator need (builders, DTOs). Requires letting `TypeBuilder` hold child types. |
| 8 | **`required` members** (C# 11) | Feature | Low | Low | One modifier on field/property. |
| 9 | **Record inheritance** (`: Base(args)`) | Feature | Low | Med | Positional-record base with base-args; noted deferred during review. |
| ~~10~~ | ~~**Emit `///` doc comments on generated members**~~ | Feature | Med | Med | **Done** (2026-08-01). `WithSummary` everywhere, plus `WithParameterDoc`/`WithReturnsDoc` on methods and constructors. Emitted as plain comment trivia so `NormalizeWhitespace` cannot reformat the XML attributes; text is XML-escaped. |
| 11 | **Events / delegates** | Feature | Low | Med each | No current builders; niche for most generators. |
| 12 | **Attribute target specifiers** (`[return:]`, `[field:]`) | Feature | Low | Low | The attribute probe strips them today. |
| 13 | **Configurable formatting** (indent/eol) | Polish | Low | Med | Currently hardcoded 4-space/LF (deliberate — see decisions below). Options only if someone asks. |

## Reading of the table

- **#1–#4 are done** — the whole shippable story. The package builds with full
  API docs, CI is green, and the inheritance modifiers are in. The one remaining
  step to actual availability is publishing to nuget.org (needs an API key and a
  decision on whether 0.1.0 goes out as a preview).
- **#6 and #10 are done** — async methods and doc comments on generated output.
- **Everything else is demand-driven.** Add these when a real generator needs
  them, not speculatively. Of what is left, **#7 (nested types)** is the most
  likely to be wanted first; **#5 (usings)** is the largest and would most
  change how output reads.
- **#5 and below** are real but demand-driven. #5 (usings) is the one large
  feature that would most change how the output *feels*, if non-qualified names
  ever become desirable.

## Deliberate decisions (not gaps)

These look like omissions but are choices:

- **Fully-qualified type names.** Avoids using-collision handling entirely. See
  #5 if this should change.
- **4-space indentation, `\n` line endings, hardcoded.** Guarantees output is
  byte-identical across operating systems. See #13.
- **No trailing newline** after the final `}` of a compilation unit — this is
  what Roslyn's `NormalizeWhitespace` produces; tests pin the actual behaviour.
- **Raw-string escape hatches** for statements/expressions/constraints, rather
  than a fluent expression model. Malformed fragments are rejected at build
  time, so they cannot silently emit broken source. A fluent expression tree is
  a much larger project and may never be worth it.
- **`Parameter<T>` / `IParameter` are internal.** Parameters are added
  exclusively via `WithParameter<T>(name)` — one obvious way.

## Known constraints

- Targets `netstandard2.0` so the library is referenceable from source
  generators; this bounds available BCL APIs.
- Requires `Microsoft.CodeAnalysis.CSharp` 4.9.2+ on the consuming side, with
  `PrivateAssets="all"` so Roslyn does not flow to consumers.
