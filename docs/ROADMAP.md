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
| ~~15~~ | ~~**Builder references**~~ | Feature | Med | Low | **Done** (2026-08-02). A type being generated is referenced by its builder — `Returns(order)`, `WithParameter(order, "o")`, `WithInterface(iface)`, `Extends(iface)` — so the name is spelled once and only definable types can be referenced. Nested types qualify through their declaring chain. Referencing a *generic* type builder throws at emission (order-proof), since the reference cannot say what the type arguments are. |
| ~~16~~ | ~~**`[EmitsAs]` placeholders**~~ | Feature | High | Low | **Done** (2026-08-02). A stand-in type in the generator's own assembly maps to the emitted name, lighting up the whole `<T>` surface — including `IReference<T>`/`Assign` — for generated types. One hook in `TypeNameBuilder` covers every position; arrays/generics of placeholders compose; generic placeholders are rejected rather than guessed at. |
| ~~17~~ | ~~**Typed call handles**~~ | Feature | High | Med | **Done** (2026-08-02). `AsCallable(out IMethod<T1…>)` validates the asserted signature against the declared parameters (by emitted name, so CLR, placeholder, and builder-reference parameters validate uniformly) and freezes the signature; `Call(target, handle, args…)` type-checks arguments in the generator. Shadow qualification covers every reference position — including `Assign`'s value side, previously a gap. Static calls not modelled yet. |
| ~~18~~ | ~~**Receiver-typed handles**~~ | Feature | Med | Low | **Done** (2026-08-02). `AsCallableOn<TDeclaring, …>` yields `IMethodOn<TDeclaring, …>`, and `CallOn` rejects a receiver of the wrong type at compile time. Pairing needs no registry: a placeholder's emitted name and the declaring type's qualified name are the same string. Separate interface *and* method family — see the diagnostics note below. |

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

- **`CallOn` is named apart from `Call`** rather than overloading it. Sharing one
  name made a mismatched receiver report as *"cannot convert `IMethodOn<T>` to
  `IMethod`"*: C# drops a candidate whose type inference fails before it can
  produce a diagnostic, so the receiver-typed overload (where `TDeclaring` gets
  conflicting bounds from the target and the handle) vanished silently, and the
  surviving untyped overload blamed the handle instead of the disagreement.
  Distinct names leave one candidate, so the error is `CS0411` naming
  `CallOn<TDeclaring>` — the same diagnostic a mismatched `Assign` gives. The
  extra name buys an error that points at the actual problem.

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

## Planned (designed, not built)

- **#19–#23 — the rest of statement support.** Two statement kinds are typed
  (assignment, calls); return, guards, literals-as-values, and accessor bodies
  are not. Designed in [`DESIGN-statements.md`](DESIGN-statements.md): a shared
  statement surface first (the API is currently duplicated across method and
  constructor builders), then `Return`, literal values, accessor bodies, and
  null guards. That document also records where the line sits — none of #19–#23
  needs an expression grammar, and the first item that would is called out
  separately so it is not crossed by accident.

## Future direction (sketched, not committed)

The reference story splits by where the referenced type lives. Types in a
shared library both sides reference: fully typed today via the `<T>` overloads.
Types the generator itself emits: covered by #15–#17. The consumer's own types:
knowable only at generator run time via `ISymbol` — no compile-time story
exists or can.

- **`ClassFrom<T>` — the placeholder as the definition.** The largest of these,
  and the one the others lead toward. Today a placeholder *shadows* a builder
  definition: both state the shape, and nothing enforces agreement beyond the
  guards in #16–#18. Inverting that makes the placeholder the single source of
  truth — declare the shape as real C# in the generator project, and derive the
  builder from it by reflection at generation time:

  ```csharp
  [EmitsAs("MyApp.Models.Widget")]
  internal abstract class WidgetPh
  {
      public abstract string Label { get; set; }
      public abstract void SetLabel(string label);
  }

  var widget = ns.ClassFrom<WidgetPh>();
  widget.Implement(nameof(WidgetPh.SetLabel), m => m.Assign(label, labelParam));
  ```

  Signatures then cannot drift, because emission is derived from the thing that
  declares them — and `class DerivedPh : WidgetPh` becomes literally
  compile-checked inheritance, with the C# compiler doing the verifying rather
  than the library. Reflection over the generator's own assembly is plain
  netstandard2.0, so nothing exotic is required.

  Scope honestly: get-only and init properties, base types and interfaces,
  static and generic members, the `Implement`-by-name story and what happens
  when a declared member is never implemented. Bigger than #14–#18 combined.

  **The direction that does *not* work**, recorded so it is not re-derived:
  giving placeholders members generated *from* the fluent definition. The
  definition runs when the generator runs; the placeholder compiles before
  that. Only static extraction from literal builder chains could bridge it, and
  that goes blind the moment definitions become data-driven — which is the
  reason generators exist. The dependency has to point placeholder → builder,
  never the reverse.
- **Static calls.** `AsCallable` refuses a static method rather than emitting
  instance syntax. Modelling them needs a type-level receiver
  (`Type.Method(args)`) rather than a reference, which is a different shape
  from everything in #17–#18.
- **Compile-checked templates.** A meta-generator that runs on generator
  projects: the author writes real C# — compiled, type-checked, refactorable —
  and the meta-generator lifts it into the emission calls that reproduce it,
  with marked holes for the varying parts. Legal because a generator project is
  an ordinary library at its own build time — verified empirically with a
  three-level chain (meta-generator → generator → app), all netstandard2.0
  where required: the ns2.0 rule constrains what a generator *is*, not what it
  runs *on*. The hard ceiling: the consumer's types exist only when the
  generator runs, so template holes stay unchecked at the seams — "type safe,
  almost" is the honest maximum. Build it when a real generator's needs demand
  it, per the rule above.

## Known constraints

- Targets `netstandard2.0` so the library is referenceable from source
  generators; this bounds available BCL APIs.
- Requires `Microsoft.CodeAnalysis.CSharp` 4.9.2+ on the consuming side, with
  `PrivateAssets="all"` so Roslyn does not flow to consumers.
