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
| ~~19~~ | ~~**Shared statement surface**~~ | Refactor | High | Med | **Done** (2026-08-02). The statement API was duplicated across the method and constructor builders. `StatementBuilder` now holds parameters, statements, and the emission logic once; `StatementBuilder<TSelf>` adds the fluent layer, CRTP-style like `TypeBuilder<TSelf>`. Shadowing and validation exist in exactly one place. Behaviour-preserving: 421 tests passed before and after, unchanged. |
| ~~20~~ | ~~**Typed `Return`**~~ | Feature | High | Med | **Done** (2026-08-02). `MethodBuilder` splits into `MethodBuilderBase<TSelf>` plus void and `MethodBuilder<TReturn>` kinds, so `Return(IReference<TReturn>)` is checked by the compiler. `DefineMethod<T>` now returns the typed builder. `Returns(string)` and bare `Return()` stay on the void kind — a raw return type cannot be checked, and a bare return in a method that owes a value does not compile. |
| ~~21~~ | ~~**Literal values**~~ | Feature | High | Low | **Done** (2026-08-02). `AssignLiteral` / `ReturnLiteral` reach the existing `SyntaxLiterals` machinery. Named apart from `Assign` rather than overloaded — see the diagnostics note below. |
| ~~22~~ | ~~**Typed accessor bodies**~~ | Feature | Med | Med | **Done** (2026-08-02). `WithGetter(g => g.Return(field))` / `WithSetter(s => s.Assign(field, s.Value))`. The setter's `value` is a typed `IReference<T>`, and it sits in the scope's parameter list, so a member named `value` qualifies with `this.` instead of self-assigning. |
| ~~23~~ | ~~**Null guards**~~ | Feature | Med | Low | **Done** (2026-08-02). `ThrowIfNull(reference)` emits the classic `if`/`throw`, not `ArgumentNullException.ThrowIfNull` — the consumer's target framework is unknown to the generator and the helper is .NET 6+. Constrained to reference types; the exception type routes through `TypeNameBuilder`, so it shortens under `SimplifyTypeNames`. |
| ~~25~~ | ~~**`SourceFile` — a file-level builder**~~ | Feature | High | High | **Done** (2026-08-02). A top-level type used to *be* a file, so two types could not share one, and 27 public methods describing file concerns were duplicated across six type builders. `SourceFile` owns usings, simplification, namespace style, and formatting; type builders lost all of it. **Breaking**, deliberately, while the version still says preview. `TypeNameSimplifier` needed no changes — it already worked on the whole compilation unit, so joint ambiguity analysis across types in one file came free. See [`DESIGN-source-files.md`](DESIGN-source-files.md). |
| ~~24~~ | ~~**Compound assignment**~~ | Feature | Med | Low | **Done** (2026-08-02). `Assign(target, op, value)` and the literal form cover the ten arithmetic and bitwise operators via an `AssignmentOperator` enum — one method pair rather than ten. `??=` is separate (`AssignIfNull` / `AssignIfNullLiteral`) because it needs a nullable target, a constraint the shared signature cannot state. Needs no expression grammar: the operands are still references and literals. |
| ~~18~~ | ~~**Receiver-typed handles**~~ | Feature | Med | Low | **Done** (2026-08-02). `AsCallableOn<TDeclaring, …>` yields `IMethodOn<TDeclaring, …>`, and `CallOn` rejects a receiver of the wrong type at compile time. Pairing needs no registry: a placeholder's emitted name and the declaring type's qualified name are the same string. Separate interface *and* method family — see the diagnostics note below. |
| ~~28~~ | ~~**Compile-checked templates** (steps 1–2)~~ | Feature | High | High | **Done** (2026-08-06), in a new analyzer-only package `FluentRoslyn.Templates`. A meta-generator runs on the *generator* project, finds `[Template]` methods, and lifts each into the FluentRoslyn calls that reproduce it — so the body is real, compiled, refactorable C#. Breaking a template is CS0103 and a **failed build in the template author's own compile**, where the string it replaces failed as CS8785, a *warning*, in the consumer's. The three-level chain is an example that CI runs end-to-end. Holes and statement bodies are deliberately unbuilt — see [`DESIGN-templates.md`](DESIGN-templates.md). |
| ~~27~~ | ~~**Computed values** — `new T(args)` and call results~~ | Feature | High | High | **Done** (2026-08-06). Closes the last structural limit from the statement design — `IValue<T>` sits above `IReference<T>`, so a value can now be produced rather than only named. `AsConstructable` mirrors `AsCallableOn` for `new T(…)`; `AsFunction`/`AsFunctionOn` are a return-carrying handle family, needed because `IMethod<T1…>` asserts argument types only and so cannot say what a call produces. Crosses the expression-grammar line deliberately, under a stated rule — see below and [`DESIGN-computed-values.md`](DESIGN-computed-values.md). |
| ~~26~~ | ~~**Reference paths**~~ | Feature | High | Low | **Done** (2026-08-05). `Member`/`MemberNamed`/`Item` build one `IReference<T>` from another, closing the assignment *target* column: before this only a simple name could be assigned to, so `this.a.b = x;` and `arr[i] = x;` had to be raw text. Every position that already takes a reference accepts a path with no new overload, because a path *is* one. Extends references, not expressions — see the note below. |

## Reading of the table

- **#1–#4 are done** — the whole shippable story. The package builds with full
  API docs, CI is green, and the inheritance modifiers are in.
- **#5, #6, #7 and #10 are done** — usings, async methods, nested types, and doc
  comments on generated output. That clears every Medium-value item.
- **Every item in the table above is done.** The list is kept as a record of
  what was built and why. Work not yet started is in **Planned**, below.
- **New work should come from a real generator's needs**, not from a
  speculative list. #14 is the first item added that way: writing a constructor
  by hand made it obvious that `AddStatement("Name = name;")` is the one place
  the library still lets you emit silently wrong code.
- **Publishing is done.** `FluentRoslyn` `0.1.0-preview.3` is live on nuget.org —
  the original name `Generatr` collided with an unrelated database scaffolder,
  and ids are case-insensitive. Releases go out hands-off through Trusted
  Publishing (OIDC), so there is no API key to hold: bump `<Version>` and
  `<PackageReleaseNotes>`, then push a `vX.Y.Z` tag and
  `.github/workflows/release.yml` does the rest. Two things that bite: package
  metadata is immutable once pushed, and the nuget.org policy is keyed to the
  workflow's *file name*, so renaming `release.yml` breaks publishing until the
  policy is updated.

## Planned — pulled by a real generator

Everything below came from **writing a generator, not from a feature list**, which is
the standing rule working as intended. The generator (2026-08-06) is a classic one:
`[GenerateBuilder]` on a consumer's class, emitting a fluent builder from that class's
constructor — one `With…` setter per parameter and a `Build()` that calls the
constructor. It is the first example written against the **consumer's** types rather
than a shape the generator invented, it now lives in
`examples/FluentRoslyn.Example.Builders.*`, and CI runs it end-to-end.

| # | Item | Category | Value | Effort | Notes |
|---|------|----------|:---:|:---:|-------|
| ~~29~~ | ~~**Members typed by name** — `DefineField(name, typeName)`, `WithParameter(name, typeName)`~~ | Feature | High | Low | **Done** (2026-08-06). This was the blocker: a generator driven by consumer symbols holds an `ISymbol`, never a CLR `T`, and `DefineField<T>`/`WithParameter<T>` were `<T>`-only — so a field or parameter of a consumer's type could not be declared **at all**, and the generator had to abandon the fluent API. What made it an oversight rather than a decision is the asymmetry: `Returns(string)` and `DefineEvent(name, handlerTypeName)` were already the raw-name escape hatch for exactly this case. Fields and parameters never got the same overload. A raw-typed field is a `RawFieldBuilder`, deliberately **not** an `IReference<T>` — there is no `T` to check against, and saying so beats a phantom type that lies. Transposing the two string arguments is caught, because the name is validated as an identifier and a qualified type name is not one. |
| ~~30~~ | ~~**`this` as a reference**~~ | Feature | Med | Low | **Done** (2026-08-06). `This()` gives an untyped reference for a type with no placeholder — which is every type a generator discovers — and `This<T>()` a typed one, paired with the declaring type by the rule `AsCallableOn` uses, so it composes with the whole typed surface. Emitted as `ThisExpression()`, not a parsed identifier. Two guards fall out of the shared emission path: `this` in a static member is refused, since there is none to emit, and `ThrowIfNull(this)` is refused, since `nameof(this)` is not legal C# and `this` is never null anyway. |
| ~~31~~ | ~~**Constructing a consumer's type**~~ | Feature | Med | Med | **Done** (2026-08-06). `Value.NewOfType(typeName, args…)` constructs a type named by text, and `MethodBuilder.Return(IValue)` returns it. Deliberately **untyped**: nothing about a consumer's constructor can be checked, and the result is an `IValue` rather than an `IValue<T>` so it reaches only the positions that accept a bare value and cannot pass for a checked one. What it buys over a raw statement is real but bounded, and worth stating as the general case for this whole tier: **the syntax is built rather than concatenated, and argument names come from the builders that declared them rather than from a second round of string formatting that can drift.** |
| 32 | **Assignment between consumer-typed members** | Feature | Med | Med | Follows from #29 rather than being fixed by it, and now the **only** raw statement left in the builders example. A raw-typed field and a raw-typed parameter share no `T`, so `Assign` cannot connect them and `_x = x;` stays text. Note the trap: an untyped `Assign(IReference, IValue)` beside the typed `Assign<T>` would be *applicable* to typed calls too, and C# prefers the non-generic candidate — silently routing checked assignments through the unchecked path. So this needs a distinct name, for the fourth time and the same reason. A checked form would need name-based type equality ("declared with the same type text"), which is weaker than the `<T>` contract and should only be built if it earns itself. |

**The pattern across all four:** the typed surface is complete for types the generator
*builds* and for types it can name as `<T>`, and thins out for types it *discovers*.
That is the third column of the reference story below. No *compile-time* story exists
for consumer types — but as #29 showed, that argues for a well-marked raw seam, not for
no API at all. With #29–#31 in, a symbol-driven generator builds every declaration and
every `return` through the fluent API, and the builders example is down to a single raw
statement.

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

- **`MemberNamed<T>("x")` is named apart from `Member(handle)`**, for the third
  time and the third instance of the same rule. The two would overload cleanly on
  argument type (`string` versus `IReference<T>`), but the type parameter binds
  differently — inferred from the handle, explicit-only from the string — so a
  mismatch picks the wrong candidate to blame. Probed:
  `config.Member<string>(intProperty)` under one name reports *"cannot convert
  `PropertyBuilder<int>` to `string`"*, pointing at the overload the caller did not
  mean. Under two names it reports *"cannot convert `PropertyBuilder<int>` to
  `IReference<string>`"* — the actual disagreement. The name also carries the
  meaning: `Member` derives the type, `MemberNamed` asserts it.

- **Values are produced, never combined.** This is the rule that admits #27 and
  still refuses an expression grammar, and it is stated as a rule rather than a
  list so it can settle the *next* argument too. A value may be produced four
  ways — a reference, a constant, `new T(…)`, a call's result — and that list is
  closed. Nothing combines two values: no `a + b`, no `a == b`, no `a ?? b`, no
  conditional, no cast, nothing needing precedence or evaluation order. The test
  for a future addition is one question: **does it produce a value, or combine
  values?** The reason the line sits exactly there is that a constructor and a
  method each have a *declaration* to check an asserted shape against, which is
  the machinery `AsCallable` already is. `a + b` has none, so the library would
  have to model C#'s conversion and promotion rules itself to say anything useful.

- **`Assign`'s target and `ThrowIfNull` still ask for `IReference<T>`** while every
  value position takes `IValue<T>`. That is not bureaucracy: it is exactly the set
  of operations needing a *location* rather than a value. You cannot assign to a
  call's result, and `nameof` cannot see one.

- **A call's receiver stays a reference too**, so `Factory.Create().Configure()` is
  not expressible. Shadow qualification is defined on a root name and a call has
  none; and a chain on a temporary is the shape that most wants a named local,
  which costs one statement and keeps generated code flat and breakpointable.

- **A reference path is not an expression.** `Member`/`Item` compose a *location*
  — they name where a value lives. Nothing about them evaluates, so there is no
  operator, no precedence, and no ordering to model, which is what keeps #26 on
  the near side of the expression-grammar line the statement design draws. The
  test of whether a later addition belongs here is the same: does it name
  something, or compute something?

- **`Item` is typed by the container, not by assertion.** Overloads on
  `IReference<T[]>`, `IReference<List<T>>` and `IReference<Dictionary<TKey, TValue>>`
  derive the element type from the receiver, so it cannot be asserted wrongly. The
  cost is that an interface-typed container (`IReadOnlyDictionary<,>`) and any
  custom indexer are not covered, because `IReference<T>` is invariant and the
  library has no way to know an arbitrary type's indexer signature. Deliberate: a
  general `Item<TItem>(this IReference target, …)` would accept *any* receiver and
  silently succeed on the mismatch that the typed overloads catch, which is the
  same trap as an untyped `Call` overload. Those cases stay with `AddStatement`.

- **`ThrowIfNull` refuses an element path.** `nameof` rejects an element access
  anywhere in the chain — measured as CS8081 for `nameof(items[0])` and CS8082 for
  `nameof(items[0].Length)` — so the guard would emit source that fails the
  *consumer's* build. Throwing at generation time is the worse kind of check by
  this project's own argument, but the alternative here is not a compile-time
  check; it is emitting code that cannot compile.

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
  common enough — and error-prone enough — to justify the machinery. **Narrowed
  again by #26:** the typed form now reaches member paths and indexed elements, so
  what is left to raw text on the assignment side is genuinely computed values.
  Branching, loops and operators are all still raw.
- **`Parameter<T>` / `IParameter` stay internal.** Parameters are still added
  only via `WithParameter<T>(name)`; the #14 overload hands back an
  `IReference<T>`, which is a name and a phantom type, not the parameter builder.
- **`IReference<T>` is invariant.** C# generic constraints cannot express
  "implicitly convertible to", so exact matching is the only contract that can
  actually be enforced. Covariance would let `Assign(stringTarget, objectValue)`
  compile by inferring a common base — the exact bug the type parameter exists
  to catch. Widening (`object` ← `string`, `long` ← `int`) falls back to
  `AddStatement`.

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
- **Compile-checked templates — now designed, see
  [`DESIGN-templates.md`](DESIGN-templates.md).** A meta-generator that runs on
  generator projects: the author writes real C# — compiled, type-checked,
  refactorable — and the meta-generator lifts it into the emission calls that
  reproduce it, with marked holes for the varying parts. Legal because a
  generator project is an ordinary library at its own build time, and re-measured
  in the design: the meta-generator uses `RegisterSourceOutput` (the normal
  generation pass, not the privileged post-initialization hook) and reads the
  *generator project's own syntax trees*; the generator consumes that generated
  code and emits into the app, which runs correctly. The ns2.0 rule constrains
  what a generator *is*, not what it runs *on*, and generators not seeing each
  other's output applies within *one* compilation — these are two.

  The measurement that justifies the feature rather than merely permitting it:
  breaking a template gives **CS0103, build FAILED, in the generator author's own
  compile** — an error that stops the build, where the raw-string form it replaces
  fails as CS8785, a *warning*, in the *consumer's* build. Right side of all three
  asymmetries at once.

  The hard ceiling, unchanged and unfixable: a hole is filled when the generator
  runs, with an expression naming the consumer's types, which do not exist when the
  template compiles. So the body and the hole's declared type are checked; whether
  the substituted expression is valid in the consumer's compilation is not. "Type
  safe, almost" is the honest maximum, and the answer is to keep holes narrow and
  few rather than to pretend otherwise.

## Known constraints

- Targets `netstandard2.0` so the library is referenceable from source
  generators; this bounds available BCL APIs.
- Requires `Microsoft.CodeAnalysis.CSharp` 4.9.2+ on the consuming side, with
  `PrivateAssets="all"` so Roslyn does not flow to consumers.
