# Computed values — design

**Status: proposed, 2026-08-05. Nothing here is built.**

This is the item the statement design deliberately separated out and warned about:

> Call results as values. Requires `Call` to have an expression form, not just a
> statement form. This is the first step that genuinely starts building an
> expression tree; see the line, below.

— [`DESIGN-statements.md`](DESIGN-statements.md), #21 follow-ons.

So this document's first job is not to describe an API. It is to say **where the new
line is**, in terms that can settle the next argument as well as this one. The API
follows from that.

## The problem

`DESIGN-statements.md` names it as structural limit ①: *only named references can be
values*. Reference paths (#26) widened where a value can **live** — `this.a.b` and
`arr[i]` are now assignable — but not what a value can **be**. Every right-hand side
is still a name or a constant.

What that costs, concretely:

```csharp
ctor.AddStatement("_items = new List<string>();");        // construction
run.AddStatement("_result = _service.Compute(input);");   // a call's result
run.AddStatement("_log.Add(_format.Render(item));");      // a call inside a call
```

All three are the shape a generator reaches for constantly, and all three are raw
text — the position where a typo produces source that compiles into the wrong thing,
which is the entire reason this library exists.

## Where the line goes

The risk the statement design named explicitly is worth restating, because it is the
reason to write this down before writing any code:

> These items are individually cheap, and the next one always looks cheap too. That
> is how a project acquires an expression tree by instalments without ever deciding
> to build one.

A rule that only describes what is being added now is useless — it will not answer
the next question. So the rule has to be about the *shape* of what is admissible:

> **Values are produced, never combined.**

A value may be **produced** four ways, and that list is closed:

| # | Producer | Status |
|---|---|---|
| 1 | a reference — a name, a member path, an element access | shipped (#14, #26) |
| 2 | a constant | shipped (#21) |
| 3 | `new T(args)` | this document |
| 4 | a method call's result | this document |

There is no way to **combine** two values. No `a + b`, no `a == b`, no `a ?? b`, no
`a is T`, no conditional, no cast. Nothing that needs precedence, associativity, or
short-circuit evaluation order.

The test for a future addition is one question: **does it name or produce a value, or
does it combine values?** Combining is over the line. That is a sharper test than
"does it feel like an expression tree", and it is the one to apply next time.

**Why this line and not the current one.** Producers 3 and 4 are the two ways C#
itself makes a value out of a *declaration the generator can already see*. A
constructor and a method both have a signature, and this library already has the
machinery to check a call against a signature — `AsCallable` validates the asserted
shape and freezes it. Extending that to two more producers reuses a mechanism that
exists. Operators would not: `a + b` has no declaration to check against, so the
library would have to model C#'s conversion and promotion rules itself, and get them
right, to say anything useful. That is a different project, and the honest place to
stop.

**What stays raw**, and is expected to stay raw indefinitely: `if`/`else`, loops,
`try`/`catch`, `await`, every operator, and every comparison. The position after this
work — *"assignment, calls, returns, guards, construction and call results are
checked; branching and arithmetic are raw"* — is explainable, which is the standard
the last line was held to.

## The abstraction

A computed value is not a reference. It has no name, so `nameof` cannot see it, and
it cannot be assigned *to*. That difference is the whole design:

```csharp
public interface IValue<T> { }               // something that produces a value of T
public interface IReference<T> : IValue<T>   // ...that also has a name and a location
```

- **A reference is a value.** Every existing call site keeps working unchanged.
- **A value is not a reference.** So `Assign`'s *target* stays `IReference<T>`, and
  `ThrowIfNull` stays on `IReference<T>` — it needs `nameof`, which needs a name.
  The split is not bureaucratic; it is exactly the set of operations that need a
  location rather than a value.

`IValue<T>` is **invariant**, for the same reason `IReference<T>` is: C# constraints
cannot express "implicitly convertible to", so exact matching is the only contract
that can be enforced, and covariance would let the mismatch the parameter exists to
catch slip through by inferring a common base.

### Widen, do not overload

The value side of the existing surface changes type; it does not gain an overload:

| Method | Today | Proposed |
|---|---|---|
| `Assign` target | `IReference<T>` | `IReference<T>` — unchanged, deliberately |
| `Assign` value | `IReference<T>` | `IValue<T>` |
| `Return` | `IReference<T>` | `IValue<T>` |
| `Call`/`CallOn` receiver | `IReference<T>` | `IReference<T>` — unchanged |
| `Call`/`CallOn` arguments | `IReference<T>` | `IValue<T>` |
| `ThrowIfNull` | `IReference<T>` | `IReference<T>` — unchanged |

This is the codebase's own standing lesson applied ahead of time rather than after
a bad diagnostic: **an overload whose type parameter binds differently blames the
wrong argument.** Adding `Assign(IReference<T>, IValue<T>)` *beside*
`Assign(IReference<T>, IReference<T>)` would leave two candidates where one suffices,
and the mismatch would report against whichever survived inference. One widened
signature leaves one candidate.

**Must be probed before building, not assumed** — the `AssignLiteral` correction is
the precedent for exactly this reasoning being right in principle and wrong in a case
nobody enumerated. The mismatch cases to probe, each expecting a diagnostic that
names the disagreement rather than a conversion against the wrong parameter:

- `Assign(stringProperty, intParameter)` — should stay `CS0411` on `Assign`.
- `Assign<string>(target, null)` — the case that broke last time.
- `Assign(stringProperty, callReturningInt)`.
- `AssignLiteral(target, literal)` still unambiguous beside the widened `Assign`.

### Receivers stay references, on purpose

`Call`'s receiver stays `IReference<T>` rather than widening to `IValue<T>`, which
would allow `Factory.Create().Configure()`. Two reasons, and the second is the real
one:

1. Shadow qualification is defined on a *root name*; a call has none.
2. A chained call on a temporary is the shape that most wants a local — it is where
   generated code becomes hard to read and impossible to breakpoint. Refusing it
   costs one statement and keeps the emitted code flat.

Revisit only if a real generator needs it, per the roadmap's standing rule.

## Producer 3 — `new T(args)`

`AsConstructable` mirrors `AsCallableOn` rather than `AsCallable`: a constructor is
always attached to a type, and the type *is* the result, so the declaring type is
never optional.

```csharp
var widget = file.Class("Widget");
widget.DefineConstructor(AccessModifier.Public)
    .WithParameter<string>("label", out var labelParam)
    .AsConstructable<WidgetPh, string>(out var newWidget);

owner.DefineConstructor(AccessModifier.Public)
    .WithParameter<string>("label", out var label)
    .Assign(current, Value.New(newWidget, label));      // Current = new MyApp.Widget(label);
```

`IConstructor<TDeclaring, T1…>` carries the declaring type *and* the argument types,
so both the result type and the arguments are checked. Validation is the one
`AsCallable` already does — assert the signature against the declared parameters by
emitted name, then freeze the parameter list — plus `AsCallableOn`'s receiver pairing
rule, which needs no registry because a placeholder's emitted name and the declaring
type's qualified name are the same string.

**The gap to state plainly: this only covers types the generator builds.** A
constructor handle comes from a `ConstructorBuilder`, so `new List<string>()` — a
type from the BCL or a shared library, with no builder — has nothing to derive a
handle from. Three ways to close it, in descending order of honesty:

- **(a) Leave it to `AddStatement` / `WithInitializerExpression`.** Zero machinery,
  and the raw-text seam is already documented everywhere else.
- **(b) A reflected handle: `Constructable.Of<List<string>>()`, checked against
  `typeof(T).GetConstructor(…)` at generation time.** Plain netstandard2.0
  reflection, and it is a real check — but a *generation-time* one, which this
  project has measured to be the weak kind: CS8785 is a warning, the consumer's
  build succeeds, and the generated code is silently missing. Buying a weak check
  with new public surface is a poor trade.
- **(c) Defer to compile-checked templates (#3 on the roadmap).** A CLR type's
  constructor is exactly the thing a template can express as real, compiled C#.

**Recommended: (a) now, and let a real example generator decide whether (b) earns
itself.** This is the roadmap's standing rule, and the case for (b) is weak enough
that guessing would probably guess wrong.

## Producer 4 — a call's result

### The blocker: handles carry no return type

`AsCallable<T1>(out IMethod<T1>)` asserts *argument* types only. `IMethod<T1>` cannot
say what a call through it produces, so it cannot be a value. A handle that carries
the result is a new family:

```csharp
widget.DefineMethod<int>("Measure")
    .WithParameter<string>("text", out _)
    .AsFunction<string>(out var measure);          // IFunction<int, string>
```

`AsFunction` lives on `MethodBuilder<TReturn>` only, so `TResult` comes from the
builder rather than being asserted — the same trick that makes `Return` compile-time
checked (#20). The void `MethodBuilder` does not get it, because there is no result;
that is the compiler enforcing the distinction rather than a guard.

Four families result, and the names are distinct rather than overloaded, for the
third time and the same reason:

| Handle | Made by | Carries | Used by |
|---|---|---|---|
| `IMethod<T1…>` | `AsCallable` | arguments | `Call` (statement) |
| `IMethodOn<TDeclaring, T1…>` | `AsCallableOn` | + receiver | `CallOn` (statement) |
| `IFunction<TResult, T1…>` | `AsFunction` | + result | a value |
| `IFunctionOn<TDeclaring, TResult, T1…>` | `AsFunctionOn` | + receiver, result | a value |

### Spelling the producer

Two candidate shapes, and this is the main open decision:

```csharp
// (i) extension on the receiver -- reads in the order the emitted code reads
method.Return(cache.Invoke(getOrAdd, key));

// (ii) static factory -- reads like the other value producers
method.Return(Value.Call(cache, getOrAdd, key));
```

**Recommended: (i) for calls, (ii) for construction.** A call has a receiver and
should read left-to-right like the source it emits, which is also how `References`
already spells `Member` and `Item`. Construction has no receiver, so there is nothing
for an extension to hang off. The inconsistency is only apparent: the two producers
have different shapes in C# itself.

The receiver-typed pair (`InvokeOn`) stays a separate name from `Invoke`, matching
`Call`/`CallOn` — the diagnostics reason has not changed.

### Emission

A value producer cannot build syntax when it is created: shadow qualification needs
the enclosing parameter list, which only the statement builder has. So an `IValue<T>`
is a **description**, resolved at emission — exactly the pattern `ElementPath`
already uses for a reference index, and the reason that pattern was worth
establishing. One internal seam, one dispatch point in `SyntaxReferences.Expression`.

## What this does not include: locals

`var result = target.Compute(x);` — binding a computed value to a name — is
deliberately **not** in this item, and it is the obvious next request.

It is not needed for correctness: a value can be nested where it is used. It becomes
necessary the moment a computed value is used *twice*, and it is the readable form
for a long chain. But it is genuinely separate work, because a local is a new kind of
name and therefore a new source of collisions — against parameters, against members,
against other locals — and the shadowing rules would have to grow a third case.

Sketch, for when it is pulled by a real need:

```csharp
method.CallInto(out var result, service, compute, input)   // var result = service.Compute(input);
      .Assign(field, result);                              // result is an IReference<int>
```

Note that this shape *also* solves producer 4 on its own, without `IValue<T>` — at
the cost of a temporary per intermediate. That is a real alternative design, not a
strawman; it is rejected here only because it makes the common single-use case
verbose, and because `IValue<T>` is what lets a call result be an argument to another
call without inventing a name for it.

## Sequencing

| Order | Step | Depends on | Notes |
|---|---|---|---|
| 1 | `IValue<T>`, widen the value side | — | Behaviour-preserving. Probe every mismatch diagnostic here, before anything depends on the shape |
| 2 | `AsConstructable` + `Value.New` | 1 | Mirrors `AsCallableOn`; validation machinery already exists |
| 3 | `AsFunction`/`AsFunctionOn` + `Invoke`/`InvokeOn` | 1 | The new handle families are the bulk of it |
| 4 | Locals (`CallInto`) | 1–3 | Deferred. Build when a real generator needs a value twice |

Step 1 is worth doing alone even though it adds no capability: it isolates the only
risky part — overload resolution across ~15 widened signatures — into a change whose
tests all already exist and must all still pass.

## Open decisions

1. **Producer spelling:** `target.Invoke(handle, args)` or `Value.Call(target, handle, args)`?
   *Recommended: the former for calls, `Value.New` for construction.*
2. **CLR-type construction:** raw text, a reflected handle, or wait for templates?
   *Recommended: raw text now; let an example generator make the case.*
3. **Arity ceiling:** the call families stop at three arguments today. Constructors
   plausibly need more — four? five? — and each one costs a handle type per family.
   *Recommended: match the existing three until something real needs a fourth.*
4. **Locals:** confirmed out of scope for this item?
