# Statement support — design

Status as of 2026-08-02. Covers the remaining statement work: what is left, how each
piece should look, and where the line is. Written after an audit of the current
surface; the roadmap records *what* shipped, this records *why the rest should take
the shape proposed here*.

## Where we are

Two statement kinds are type-checked — assignment (#14) and method calls
(#17/#18) — and both exist only in method and constructor bodies. Everything else
is raw text through `AddStatement` / `WithBody` / `AsExpressionBody`, parsed and
rejected if malformed, but not checked for meaning.

That is a coherent place to be: the *wiring* cases are covered completely. A
generated constructor that assigns parameters into properties needs no strings at
all. Add a `return`, a guard, or a branch, and you are back to text.

## Two structural limits

Most of the missing statement kinds are downstream of these, so they are worth
naming before the individual features.

**① Only named references can be values.** `Assign` and `Call` connect things that
already have names. There is no way to introduce a value — no literal, no `new
T(…)`, no using a call's result on the right-hand side. This is why the gap list
looks long: it is mostly one missing concept, not many missing statements.

Notably `SyntaxLiterals.Expression(object?)` already exists and already powers
`WithInitializer(T value)`. `Assign(prop, 42)` is impossible today only because no
overload reaches it.

**② Typed statements exist in two places, not three.** `MethodBuilder` and
`ConstructorBuilder` each carry the statement API; property accessors
(`WithGetterBody` / `WithSetterBody`) take strings only. Nothing conceptual blocks
accessors — the helpers were never routed there.

## #19 Shared statement surface (prerequisite)

**Problem.** `MethodBuilder` and `ConstructorBuilder` already duplicate the whole
statement API — `Assign`, `Call` ×4, `CallOn` ×4, plus `AddCall` — identical but
for the return type and the context string in error messages. Adding four more
statement kinds across three sites means roughly a dozen near-identical
implementations, and three places for a guard to be forgotten.

**Proposal.** Extract `StatementBuilder<TSelf> : NamedBuilder`, holding the
statement list and every statement-producing method, with `TSelf` returns so
chaining still yields the concrete builder. `MethodBuilder : StatementBuilder<MethodBuilder>`,
likewise `ConstructorBuilder`. This matches the existing `TypeBuilder<TSelf>` CRTP
idiom rather than inventing a shape.

Two details the extraction has to preserve:

- **The context string.** Errors currently read `Method 'X'` / `Constructor for 'X'`.
  Make it an abstract `private protected string StatementContext { get; }`.
- **The shadowing check** needs the enclosing parameter list and static-ness. Both
  become abstract members supplied by the concrete builder.

**Accessors are the awkward third case** — they are not builders, they are two
statement lists inside `PropertyBuilder`. See #22.

## #20 `Return`

The highest value per unit of effort: a value-returning method *must* have a body,
so today **every** non-void generated method contains a raw string.

```csharp
method.Return(someReference);   // return x;
method.Return();                // return;
```

**Guards.** `Return()` with a value on a `void` method throws; `Return(value)` on a
void method throws; a value whose type disagrees with the declared return type
throws.

**Open decision — how the return type is checked.** `MethodBuilder` is not generic
in its return type, so `Return<T>` cannot be checked by the compiler. Two options:

- **(a) Generation-time check.** Compare `TypeNameBuilder.New<T>().ToString()`
  against the declared return type's emitted name — exactly the rule
  `AsCallable`'s `ValidateHandle` already uses. Cheap, consistent, but the mismatch
  surfaces when the generator runs rather than when it compiles.
- **(b) `MethodBuilder<TReturn>`.** `DefineMethod<TReturn>` returns a generic
  subclass, so `Return(IReference<TReturn>)` is compile-checked. This is precisely
  the `PropertyBuilder<T>` / `FieldBuilder<T>` pattern the codebase already uses,
  and it matches the library's whole thesis. Costs a generic subclass whose fluent
  methods must return `MethodBuilder<TReturn>`; CRTP or `new`-shadowing both work.

**Recommendation: (b).** A generation-time check on the one statement that every
non-void method needs is a conspicuous hole in a library that sells compile-time
checking. Pre-publish, so the signature change is free.

## #21 Values beyond references

Closes limit ①. Scope this item to **literals and `null`**; object creation and
call-results are follow-ons (below).

```csharp
ctor.Assign(countProp, 0);
ctor.Assign(nameProp, "unnamed");
ctor.Assign(refProp, Value.Null<WidgetPh>());
```

**Corrected (2026-08-02, during implementation).** An earlier note here claimed
overloading `Assign` with a literal variant was safe, based on a probe of
inference cases only:

- `Assign<T>(IReference<T>, IReference<T>)` and `Assign<T>(IReference<T>, T)` do
  coexist for ordinary calls, and a mismatch reports the good `CS0411`.
- **But** an explicit type argument with `null` — `Assign<string>(target, null)` —
  fits *both* overloads and fails with `CS0121` ambiguity. That is legitimate
  usage; an existing test used exactly that shape and broke on it.

The probe was incomplete, not the reasoning. The library's own lesson applies
again: **use a distinct name, `AssignLiteral`.** Same conclusion as `CallOn` and
for the same underlying reason — two overloads whose type parameters bind
differently produce diagnostics that describe the wrong thing.

**`null` then needs no carrier.** With a distinct name there is no competing
overload, so `AssignLiteral(prop, null)` infers `T` from the target: it converts
for a reference type and is correctly rejected for a value type. The
`Value.Null<T>()` factory this section previously proposed is unnecessary.

**Also verified:** `public static implicit operator Value<T>(T literal)` — a
conversion from a bare type parameter — is legal C#, should a `Value<T>` wrapper
ever be preferred to overloads.

**Follow-ons, deliberately not in this item:**

- **Object creation.** `new T(args)` type-checked needs constructor handles
  mirroring `AsCallable` — call it `AsConstructable`. Natural, but it is its own
  feature.
- **Call results as values.** Requires `Call` to have an expression form, not just
  a statement form. This is the first step that genuinely starts building an
  expression tree; see the line, below.

## #22 Accessor bodies

Closes limit ②. Depends on #19.

```csharp
prop.WithGetter(g => g.Return(backingField))
    .WithSetter(s => s.Assign(backingField, s.Value));
```

**Design note.** A setter's implicit `value` is a typed reference for free — expose
it as `IReference<T>` on the setter's scope, so `Assign(field, s.Value)` checks
that the backing field's type matches the property's. That is a real check the
string form cannot offer.

**Open decision.** Lambda-configured scope (above) versus exposing a
`prop.Getter` / `prop.Setter` sub-builder. The lambda reads better and keeps the
scope closed; it is a new shape for this library, which so far has no
lambda-configured members.

## #23 Guard clauses

```csharp
ctor.ThrowIfNull(nameParam);
```

emitting the classic form:

```csharp
if (name is null) throw new System.ArgumentNullException(nameof(name));
```

**Why the classic form and not `ArgumentNullException.ThrowIfNull(name)`:** the
generated code compiles in the *consumer's* compilation, whose target framework is
unknown to the generator. `ArgumentNullException.ThrowIfNull` is .NET 6+. The
`if`/`throw` form compiles everywhere, including the `netstandard2.0` consumers
this library exists to serve. If a modern-form option is ever wanted it must be
opt-in, never the default.

**Constraint.** `where T : class` keeps it honest — a null check on a non-nullable
value type is meaningless. Nullable value types would need a second overload.

This is worth a dedicated item because a null-guard is plausibly the second
most-generated statement after assignment, and it can be covered without any
general branching support.

## The line we are not crossing yet

`if` / `else`, loops, `try` / `catch`, and `await` all need **composable boolean
and general expressions**. The roadmap's existing decision stands:

> Raw-string escape hatches … rather than a fluent expression model. A fluent
> expression tree is a much larger project and may never be worth it.

Items #19–#23 sit on the near side of that line: each adds a *statement* whose
parts are already-typed references or literals. Nothing in them requires an
expression grammar. Call-results-as-values (#21 follow-on) is the first item that
does, which is why it is separated out.

**The risk to name explicitly:** these items are individually cheap, and the next
one always looks cheap too. That is how a project acquires an expression tree by
instalments without ever deciding to build one. The position after #19–#23 —
*"assignment, calls, returns and guards are checked; branching is raw"* — is
defensible and explainable. Crossing further should be a deliberate decision with
a real generator's need behind it, per the roadmap's standing rule.

**Criteria for revisiting:** a real generator needs branching whose *conditions*
reference generated members, and the raw-string form has actually produced a bug
that a typed form would have caught. Absent that, the escape hatch is doing its
job.

## Sequencing

| Order | Item | Depends on | Notes |
|---|---|---|---|
| 1 | #19 shared statement surface | — | Do first, or the rest triplicates |
| 2 | #20 `Return` | #19 | Decide (a) vs (b) first — (b) changes `DefineMethod<T>`'s signature |
| 3 | #21 literals + null | #19 | Overload approach verified |
| 4 | #22 accessor bodies | #19 | Unblocks the third statement site |
| 5 | #23 guard clauses | #19 | Independent of #20–#22 |

#20's option (b) is the only item that changes an existing public signature, so it
should land before publishing rather than after.

## Open decisions

1. **#20:** generation-time check, or `MethodBuilder<TReturn>` for a compile-time
   one? *Recommended: the latter.*
2. **#22:** lambda-configured accessor scope, or exposed sub-builders?
   *Recommended: lambda.*
3. **Scope:** are #19–#23 the whole of statement work before publishing, or is
   `AsConstructable` (object creation) wanted too?
