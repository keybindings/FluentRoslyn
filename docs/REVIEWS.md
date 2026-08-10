# Review log

What has been code-reviewed, when, over which commits, and what came out of it.

This file exists because "has this been reviewed?" is otherwise unanswerable, and the
honest answer has usually been "part of it, a while ago". Each entry records the commit
range so coverage can be recomputed rather than remembered. **Add an entry whenever a
review runs, including one that finds nothing.**

Findings carry an id (`R<review>-<n>`) so a commit message can name what it fixes.

## Coverage at a glance

| Range | Lines added to `src/` | Reviewed |
|---|---:|---|
| Start → `2977d02` (2026-07-22) | — | ✅ Review 1 |
| `2977d02` → `34cb904` (preview.7) | 6,470 across 83 files | ✅ Review 3 |
| `34cb904` → `0163b82` (preview.8 + docs) | 596 across 6 files | ✅ Review 2 |

**All three ranges are now reviewed.** The middle row — roughly 80% of the library, and
every item from #14 through #38 — was the long-standing gap, and Review 3 closed it.
What is open is no longer coverage but the **35 unfixed findings** it produced; see the
fix notes below. Keep the ordering lesson this table caught once already: Review 2 was
scoped by the tool to the newest feature rather than to the codebase, so *running* a
review is not the same as covering what you meant to. Check the range it chose.

## Review 3 — 2026-08-08 — the previously unreviewed 80%

- **Range:** `2977d02..34cb904`, reviewed at HEAD — 83 files, ~6,470 added lines,
  roadmap items #14–#38. The operator feature was excluded as already covered.
- **Method:** every finding was reproduced by generating source and compiling it
  (in-memory `CSharpCompilation`, Roslyn 4.9.2), several by loading the assembly and
  executing it. The suite was green throughout, so **none of these is caught today**.
- **Findings:** 61. **Fixed 2026-08-08:** R3-01, R3-13, R3-14, R3-15, R3-16,
  R3-26. **Fixed 2026-08-09:** R3-12, and the simplifier cluster — R3-02, R3-31,
  R3-32, R3-33, R3-34, R3-43; the type-identity cluster — R3-17, R3-18, R3-19,
  R3-20, R3-21 — with R3-59 and R3-60, which are the same lines as R3-19 and R3-20;
  and the rest of the duplication cluster, R3-56, R3-57, R3-58, R3-61. **Fixed
  2026-08-10:** R3-07 and R3-23, the float/double special-value cluster in
  `SyntaxLiterals.Expression`. **26 fixed, 35 open.**

Six independent passes found R3-01 without collusion, which is the clearest signal
in the set: it is one missing override with a wide blast radius.

### Emits source that compiles and binds the wrong thing

The worst class — no diagnostic anywhere, generator and consumer both green.

| id | file:line | finding |
|---|---|---|
| R3-01 | `AccessorBody.cs:16,54,89,125` | **None of the four accessor scopes overrides `IsStaticContext`** (`StatementBuilder.cs:36` defaults false), and `PropertyBuilder` constructs them from the property *name* only, never its `IsStatic`. Every static-context guard is dead inside property bodies. `Static()` + `WithGetter(g => g.Return(instanceField))` → **CS0120**; `g.Return(This())` → **CS0026**; a `value`-shadowed member emits `this.` in a static setter → CS0026. The identical shape in a static *method* is correctly refused. Found by six passes. |
| R3-02 | `TypeNameSimplifier.cs:82` | `DeclaredTypeNames` collects only `BaseTypeDeclarationSyntax`, so a **type parameter** or a **delegate** of the same name does not block simplification. Proved by execution: `Class("Host").WithTypeParameter("StringBuilder")` + a `System.Text.StringBuilder` property gives a property whose reflected type is `System.Int32`. `file.Delegate("EventHandler")` + a `System.EventHandler` field binds to the generated delegate. Compiles clean, wrong type. |
| R3-03 | `SyntaxReferences.cs:334` | `IsShadowed` compares raw spellings, so **`@Name` never shadows `Name`** — the same C# identifier. The `this.` qualification never fires, the emitted `Name` binds to the parameter, and the member is never read. Zero diagnostics. `Identifiers.Validate` explicitly permits the `@` prefix, and defensive `@`-prefixing is a standard generator idiom. |
| R3-04 | `StatementBuilder.cs:46` | Shadow qualification is resolved **at statement-add time** against the parameter list as it then stands, so `WithParameter` *after* a statement silently re-shadows a member already emitted bare. `ThrowIfNull(prop); CallStatic(…, prop); WithParameter<string>("Name")` guards and prints the parameter. Reordering the calls fixes it, which is what makes it invisible. |
| R3-05 | `TypeNameBuilder.cs:121` | Array rank specifiers are emitted **outermost-last**, so mixed-rank jagged arrays are a different type: `DefineProperty<int[][,]>` emits `int[, ][]` (`int[*,*][]`). Consumer assignment → CS0029. |
| R3-06 | `TypeNameBuilder.cs:154`, `TypeDeclarationBuilder.cs:98` | Global-namespace types emit as **bare names with no `global::`**, so an enclosing namespace captures them — a same-named `Probe.Widget` wins over the intended global one. There is no way to express the reference correctly. A current test locks this in. |
| R3-07 | `SyntaxLiterals.cs:28` | Negative zero emits `-0`. Executed: `double.IsNegative` is `False` and `1.0/Z` is `+∞` where `-0.0` went in. |
| R3-08 | `PropertyBuilder.cs:390` | `GetOnly()` is **silently discarded** by any accessor body — the bodied branch reads only which body fields are set. `WithSetterExpression(…).GetOnly()` emits a *set-only* property: the caller asked for the opposite and it compiles. |
| R3-09 | `TemplateLifter.cs:103,239` | Templates **silently drop `out`/`ref`/`in`/`params` and default values**. `TryParse(string s, out int value)` lifts to a plain `int value` parameter — compiles, and discards the parsed result forever. No FRT diagnostic, contradicting the package's stated "every unsupported template is reported rather than skipped". |
| R3-10 | `TemplateLifter.cs:102` | Named tuples and `dynamic` are **erased** by the `<T>` round-trip: `(int A, string B)` becomes `System.ValueTuple<int,string>` → CS1061 on `p.A`; `dynamic` becomes `object` (compiles, different semantics); `string?` annotations are dropped. |
| R3-11 | `TemplateLifter.cs:100,172` | A **nested or generic template class** produces a fabricated *top-level* type rather than a partial half, so the author's call site fails with CS0117. Worse, a top-level `Ns.Templates` and a nested `Ns.Outer.Templates` **merge into one emitted class**. FRT002 does not fire. |

### Emits source the consumer's build rejects

| id | file:line | finding |
|---|---|---|
| R3-12 | `Identifiers.cs:18` | `SyntaxFacts.IsValidIdentifier` is lexical only, so **every reserved keyword passes** every name path — type, member, method, parameter, type parameter, enum member, namespace. `DefineProperty<int>("class")` emits a file that does not parse. Reachable wherever a name comes from consumer data or an `ISymbol` (which strips `@`). One added `GetKeywordKind` test fixes it; the `@` escape hatch already works. |
| R3-13 | `TypeBuilder.cs:334` | A **static class** emits instance fields, properties, methods and constructors → CS0708 ×n, CS0710, and CS0714/CS0736 with an interface. `IsStaticType` exists and is passed to the operator set only. |
| R3-14 | `TypeBuilder.cs:334` | A **readonly struct** emits the default member shapes → CS8340 (field), CS8341 (auto-property). Two documented calls taking their defaults. The value-objects example avoids it only by the author's memory. |
| R3-15 | `TypeBuilder.cs:334` | **No duplicate or colliding name detection** for members, nested types, or types in a file → CS0101, CS0102, CS0111, and CS0542 (a member named after its enclosing type — easy to hit by accident). `EnumBuilder` does exactly this check for its own members. |
| R3-16 | `MethodBuilder.cs:411` | `Virtual()` and `Partial()` are never checked against the declaring type → CS0549 (sealed), CS0106 (struct), CS0751 (non-partial type). The `abstract` equivalent *is* checked. |
| R3-17 | `ClassBuilder.cs:94`, `RecordBuilder.cs:58` | `WithParent(builder)` calls `BuildTypeSyntax()` directly, **bypassing the generic-builder guard** → `class IntBox : Container` → CS0305. `WithInterface(InterfaceBuilder)` routes through `TypeNameBuilder.For` and refuses properly. |
| R3-18 | `TypeNameBuilder.cs:113` | The generic guard checks only the **leaf** builder, not its declaring chain, so a type nested in a generic type emits `Outer.Inner` with the outer's arguments dropped → CS0305 through `WithInterface`, `WithParameter`, `Returns`, `WithParent`, `CallStatic`. |
| R3-19 | `MethodBuilder.cs:345`, `ConstructorBuilder.cs:152`, `TypeBuilder.cs:70` | Receiver/constructor/`This<T>` pairing compares against `TypeDeclarationBuilder.BuildTypeSyntax()`, which **silently drops type parameters**, so `AsCallableOn`/`AsConstructable` accept a generic declaring type → CS0305. The `CallStatic` path refuses it correctly. |
| R3-20 | `MethodBuilder.cs:310` | `ValidateHandle` never looks at **method type parameters**, so a handle to a generic method emits a call with no type-argument list → CS0411. Adding `WithTypeParameter` *after* the handle is also allowed — the freeze covers `Parameters` only. |
| R3-21 | `MethodBuilder.cs:310`, `ConstructorBuilder.cs:143` | Handles ignore **accessibility**: `AsCallableOn` on a private method, or `WithAccessModifier(Private)` after the handle exists → CS0122. |
| R3-22 | `ConstructorBuilder.cs:186` | `BuildConstructorCore` lacks the **`_handleIssued && IsStatic` re-check** that `MethodBuilder.cs:367` has and documents as "order-proof". `AsConstructable(…)` then `Static()` emits `static C()` while the handle still emits `new C()` → CS7036/CS1729. |
| R3-23 | `SyntaxLiterals.cs:27` | `float`/`double` **NaN and ±Infinity** emit as bare `NaN`/`Infinity`/`NaNF` → CS0103, through every literal path (`WithInitializer`, `AssignLiteral`, `ReturnLiteral`, `Value.Literal`). |
| R3-24 | `StatementBuilder.cs:324` | `ThrowIfNullRaw` emits `x is null` for a **value-typed** raw reference → CS0037. The reference already carries the declared type text; the typed sibling has `where TValue : class` to make this unrepresentable. |
| R3-25 | `StatementBuilder.cs:225` | `AssignRaw` never consults the target's **settability** → CS0191 (readonly field), CS0200 (get-only), CS8852 (init-only). The typed `Assign` shares the hole. |
| R3-26 | `FieldBuilder.cs:85` | A **struct field initializer** emits without the explicitly declared constructor C# requires → CS8983. |
| R3-27 | `PropertyBuilder.cs:363` | `Required()` on a **bodied** property slips the `!HasSet` guard (`HasSet` defaults true and the bodied path ignores it) → CS9034. The auto-property path is guarded correctly. |
| R3-28 | `FieldBuilder.cs:168`, `PropertyBuilder.cs:357` | `required` **visibility** is unmodelled → CS9032. `DefineField<T>(name).Required()` is private by default, so the most obvious call never compiles. `Required()` + `Readonly()` on a field → CS9034. |
| R3-29 | `PropertyBuilder.cs:372` | An **empty accessor scope** emits `get { }` on a value-returning property → CS0161. The analogous empty method body throws. |
| R3-30 | `DocComment.cs:70` | Doc text containing a lone `\r`, U+2028 or U+0085 **escapes the `///` comment** and the remainder parses as code → CS1002/CS1585. The class already sanitizes markup; the line-terminator set is incomplete. |
| R3-31 | `TypeNameSimplifier.cs:55` | A shortened name that matches a **visible namespace segment** → CS0118, including namespaces the simplifier itself just imported, an enclosing namespace, and the file's own. Locally decidable. |
| R3-32 | `TypeNameSimplifier.cs:67` | The rewrite passes `original.Right`, discarding **already-simplified descendants**, so a generic's inner type argument stays qualified while its import is still recorded → an unused `using` that then makes an unrelated name ambiguous (CS0104). |
| R3-33 | `TypeImports.cs:37` | `WithUsing(…)` directives are **invisible to the ambiguity analysis** → CS0104. The `SimplifyTypeNames` doc promises more than the check delivers. |
| R3-34 | `TypeNameBuilder.cs:282` | `[EmitsAs]` splits a **nested type name** at the last dot, so `MyApp.Outer.Inner` records `MyApp.Outer` as a namespace → `using MyApp.Outer;` → CS0138 + CS0246. Emits correctly *without* simplification, so it breaks only when someone turns it on. |
| R3-35 | `Value.cs:51`, `Invocations.cs:88`, `StatementBuilder.cs:410` | Raw **type-name** slots accept any parsable `TypeSyntax`, including forms illegal in the position used: `new int[]()` CS1586, `new Uri?()` CS8628, `new int*()` CS1919, `new (int,int)()` CS8181, `new dynamic()` CS8386; `string[].Join(…)` for the static-call slots. `ToDisplayString()` produces these routinely. |
| R3-36 | `StatementBuilder.cs:165` | **`ref` slips through** the raw parameter-type overload (`ParseTypeName("ref int")` is a valid `RefTypeSyntax`), and no call family emits the `ref` keyword → CS1620. `out`, `in`, `params` are all correctly rejected; `ref` is the single hole. |
| R3-37 | `SyntaxReferences.cs:300` | A **parameter reference used outside its own body** short-circuits to a bare identifier without checking the current scope's parameter list → CS0103. One `Any` over `Parameters` turns it into a generator-time throw. |
| R3-38 | `TemplateLifter.cs:235` | Two templates **overloading one name** emit two identical `Emit…(TypeBuilder)` methods → CS0111 in a generated file the author cannot edit, with no FRT diagnostic. |
| R3-39 | `TemplateLifter.cs:230` | **Verbatim `@`-names** lift to unparseable source: `@class(int @int)` emits `class(int int)`. The body keeps its `@` (lifted as text); only the declaration breaks. |
| R3-40 | `TemplateLifter.cs:138` | `Qualified` skips any `SimpleName` that is a `MemberAccessExpression.Name`, so **namespace-qualified spellings are not `global::`-qualified** — the example's own `System.Console.WriteLine` binds wrongly in a consumer namespace containing `System` → CS0117. Both the doc and `DESIGN-templates.md` claim every type reference is fully qualified. |

### False rejection — refuses legal code

| id | file:line | finding |
|---|---|---|
| R3-41 | `MethodBuilder.cs:322`, `ConstructorBuilder.cs:167` | `ValidateHandle` compares **exact emitted text**, so legal alternate spellings are refused: `WithParameter("n","System.Int32")` + `AsCallable<int>`, `"List<int>"` + `AsCallable<List<int>>`, `"int?"` + `AsCallable<int?>`. No false accepts found. |
| R3-42 | `StatementBuilder.cs:230` | `AssignRaw`'s type-text comparison false-rejects `int` vs `global::System.Int32`, `int?` vs `System.Nullable<int>`, `global::Probe.Inner` vs `Probe.Inner` — the natural mix of hand-written and `ToDisplayString()` names. A false reject throws at generation time → CS8785 and *no output at all*. |
| R3-43 | `TypeNameSimplifier.cs:55` | A reference to a type declared in the **same `SourceFile`** is blocked by the `declared` check and stays fully qualified, though it is the one case always safe to shorten. Over-conservative rather than wrong. |

### Missing surface

| id | file:line | finding |
|---|---|---|
| R3-44 | `PropertyBuilder.cs:370`, `EventBuilder.cs:56` | `Inheritance` is wired into `SyntaxFormatting.Modifiers` for every member but exposed **only on methods**, so `abstract`/`override` properties are inexpressible — which blocks implementing an abstract base and blocks the roadmap's own `ClassFrom<T>` direction. Compounding: `TypeBuilder.cs:338` reads `_methods` rather than a contract, so adding `Abstract()` to `PropertyBuilder` would emit abstract properties into non-abstract classes silently — the `_operators` shape from Review 2, one level down. |
| R3-45 | `InterfaceBuilder.cs:73,134`, `RecordBuilder.cs:45`, `DelegateBuilder.cs:44` | The **raw-type escape hatch is missing** from the interface, record and delegate arms, though `Parameter.OfRawName` and `TypeNameBuilder.ForRawName` are already internal and general. A generator can *implement* a discovered interface but cannot *declare* one. |
| R3-46 | `RecordBuilder.cs:17` | `RecordBuilder` extends `TypeDeclarationBuilder`, not `TypeBuilder`, so a record can declare positional parameters and operators **and nothing else** — no field, property, method, constructor, event or nested type. The Review-2 operator fix landed as a private second member pipeline rather than closing the gap, so two now exist for the same job. |
| R3-47 | `NamedBuilder.cs:36` | Only `SourceFile` and `TypeDeclarationBuilder` override `Formatting`, so a **member's `ToString()` disagrees with its own file** (four spaces/LF where the file says tabs/CRLF). Member `ToString()` is a documented public path the suite exercises. |

### Efficiency

Measured with `GC.GetAllocatedBytesForCurrentThread` + Stopwatch, Release. Root cause
behind several: `NamedBuilder.ToString()` always runs `NormalizeWhitespace` — 152 B /
3.3 µs versus 2,089 B / 41.5 µs on the same node for a byte-identical string.

| id | file:line | finding |
|---|---|---|
| R3-48 | `TypeNameBuilder.cs:160` | The namespace is stringified through `NormalizeWhitespace` on **every qualified type reference**, and the result is read only when `SimplifyTypeNames()` was requested — so **by default it is computed and never used**. 10–28% of a realistic type's whole build cost, depending on namespace depth. |
| R3-49 | `TypeDeclarationBuilder.cs:83` | Per-type `ToSourceText()` rebuilds the **entire file**, so the natural `foreach (var t in file.Types)` loop is O(n²) *and* emits N identical copies of the whole file. 20 types: 13.6 MB / 95 ms versus 756 KB / 5.9 ms. |
| R3-50 | `SyntaxReferences.cs:232` | The `ArgumentNullException` type reference is rebuilt per null guard — ~1.8 KB of invariant work each, ~14 KB for an 8-parameter constructor. |
| R3-51 | `MethodBuilder.cs:322`, `ConstructorBuilder.cs:151`, `TypeBuilder.cs:71` | Type identity is decided by stringifying syntax **asymmetrically** (one side through `NamedBuilder.ToString()`'s double normalization, the other a plain `ToString()`) — 8.9 KB per comparison, and the two sides can normalize differently. Same hazard class as R2-08. |
| R3-52 | `PropertyBuilder.cs:223,258`, `Parameter.cs:51` | Raw type text is stringified → re-parsed → stringified again: one `WithSetter` + `AssignRaw` costs +12 KB and +498 µs over the same raw auto-property. This is the main symbol-driven path. |
| R3-53 | `SourceFile.cs:39` | The constructor pays a `NormalizeWhitespace` purely to name itself — **89% of `InNamespace`'s allocation**, before a single type exists. |
| R3-54 | `TypeBuilder.cs:398` | No `Count == 0` early-out on the six member groups: 264 B per empty group, 1,424 B for all six, on every build. The zero-operator early-out from Review 2, generalised. |
| R3-55 | `TypeNameBuilder.cs:187` | `new string(type.Name.TakeWhile(…).ToArray())` for an arity suffix that is usually absent — 176 B → 0 B with `IndexOf`/`Substring`, on every `New<T>`. |

### Duplication and dead surface

| id | file:line | finding |
|---|---|---|
| R3-56 | `MethodBuilder.cs:390`, `ConstructorBuilder.cs:214`, `PropertyBuilder.cs:383,480` | The expression-body application is written **five times** with **four different guard messages** — one of which names no member at all. `SyntaxBodies` is the shelf built for exactly this and holds one method. |
| R3-57 | `SyntaxReferences.cs:89` | `Invocation` hand-builds what `InvocationValue<T>` builds, while its two siblings correctly delegate — **and two doc comments assert the opposite**, one claiming the forms "cannot drift". |
| R3-58 | `ReferencePath.cs:43,77` | `MemberPath<T>` and `RawMemberPath` are **character-identical** apart from the interface list, including the non-obvious `CanNameOf` recursion that decides whether `ThrowIfNull` refuses to emit. |
| R3-59 | `MethodBuilder.cs:310`, `ConstructorBuilder.cs:143` | The handle-signature check and the parameter-freeze latch exist twice, though `StatementBuilder` already owns `Parameters` and declares `OnParametersMutating` as the extension point — the consolidation that created it left these behind. |
| R3-60 | `TypeBuilder.cs:69`, `MethodBuilder.cs:337`, `ConstructorBuilder.cs:150` | The `[EmitsAs]` pairing rule — the library's whole correctness argument for receiver checking — is written out **three times** with three message texts. |
| R3-61 | `MethodBuilder.cs:38` | `ReturnsVoid` duplicates a fact `ReturnType` already carries, across four write sites, and `Returns("void")` **desyncs them**: `Return()` then throws "has a return type" about a void method. Latent (no call site today). Also: `SourceFile.Types` and `InGlobalNamespace()` have zero call sites anywhere, leaving the whole global-namespace branch unexercised; `TypeNameBuilder.ForEmittedName`'s comment claims two callers and has one. |

### Verified sound — probed and not broken

Recorded so the next review does not re-spend the budget: the raw-text escape hatches
(`SyntaxParse` consumes full text; attribute injection is rejected); the parameter
freeze itself; `Static()`/`Abstract()`/`Async()` after a handle on the *method* side;
`nameof` over member paths and `CanNameOf`'s element-access refusal; static-context
guards for methods, constructors and operators; the setter's implicit `value`
seeding; every Review-1 fix surviving the CRTP split on **both** the typed and raw
arms; every non-float literal shape including `int.MinValue`, `decimal.MaxValue`,
`'\0'`, surrogates and embedded quotes; doc-comment markup escaping; enum
underlying-type and duplicate-member checks; `NamedBuilder`'s validator not being
retained in a field. The `Generatr` → `FluentRoslyn` rename dropped no guard.

## Review 2 — 2026-08-08 — operator and conversion declarations

- **Range:** `34cb904..0163b82` (`v0.1.0-preview.7` → HEAD)
- **Subject:** roadmap #39 and its follow-up — `OperatorBuilder`, `OperatorKind`, the
  `TypeBuilder` validation block, the value-objects generator's operator members, and
  the lockstep/rename documentation added alongside.
- **Not covered:** everything in the gap row above.
- **Findings:** 24 — **all fixed, 2026-08-08**, across three commits:
  - `6dcbe63` — R2-01 (null-safe value objects; details on the finding below).
  - `7f9a7ee` — R2-02..R2-14 and R2-18..R2-24: the operator validation rebuilt around a
    canonical signature model in a shared `OperatorSet`. Worth singling out: **R2-11's
    counterexample was already in the test suite** — a test returned an instance field
    from a static method and asserted the CS0120-broken emission; **R2-07** resolved via
    a per-operator `PartnerDeclaredElsewhere()` waiver, since the pairing rules are
    per-type and a partial type may legally split them; **R2-12** now throws at *Define*
    time (`ArgumentException` naming the value) — a deliberate divergence from
    `EnumBuilder`'s build-time deferral, because an undefined enum value gains no
    context by waiting; **R2-13** additionally removed zero from `ConversionKind`, so a
    computed default can no longer silently mean implicit; **R2-19** gave records
    operators but refuses `==`/`!=`, which a record synthesizes; **R2-20** was pinned
    rather than changed — the glued `operator>` is valid C# and a NormalizeWhitespace
    quirk in the same family as `int[, ]`.
  - `5e0d925` — R2-15..R2-17: a deliberately-void template now build-guards the lifter's
    void branch, and the rename-hazard record states the true guard (the example, not
    "nothing compiles"). **R2-16's shipped preview.8 text is immutable**; the csproj is
    corrected forward, so the fix lands on the next release's notes.

Ranked most severe first. The last ten were below the reporting tool's cap and are
recorded here at the same level of detail as the rest, because a finding dropped for
formatting is still a finding.

### Emitted source that does not compile

| id | file | finding |
|---|---|---|
| R2-01 | `examples/…ValueObjects.Generator/ValueObjectGenerator.cs:118` | Generated `==`/`!=` route to `Equals(T)` → `Value.Equals(…)` unguarded, so comparing default-constructed value objects over a reference type throws `NullReferenceException`. Verified: `a == b`, `a != b` and `arr[0] == arr[1]` over `new CustomerCode[2]` all threw. Before operators this needed an explicit `.Equals()`; now it is reachable through an operator nobody expects to throw, in the pattern the README and CI hold up as canonical. `GetHashCode` has the same hole. **Fixed 2026-08-08.** Reproduced live first (NRE through `op_Equality`, as described). Equality and hashing now route through `EqualityComparer<T>` — the lowering the compiler gives records — hoisted into a static field because `EqualityComparer<T>.Default` is a static *property* no built call can root on (recorded as roadmap #40). `ToString` had the identical hole one member over and now formats through interpolation. The app exercises default instances and CI asserts the output, so the crash cannot return silently. |
| R2-02 | `src/FluentRoslyn/Builders/OperatorBuilder.cs:209` | Operator **arity is never validated**. Verified: `==` with no parameters → CS1534; `+` with three → CS1534; `++` with two → CS1020; `==` with one → CS1019; a conversion with two → CS1019. `ApplyBody` refuses a missing *body* fifteen lines away, and `ValidateChecked` already reads `Parameters.Count`, so arity is load-bearing and simply unchecked. |
| R2-03 | `src/FluentRoslyn/Builders/TypeBuilder.cs:367` | The CS0216 pairing check keys on `OperatorKind` alone, discarding parameter types, so a partner with a *different signature* satisfies it. Verified: `==(OrderId, OrderId)` + `!=(OrderId, int)` both emit and the consumer gets CS0216 on both. Same for mismatched ordering pairs. This is the feature's headline guarantee. |
| R2-04 | `src/FluentRoslyn/Builders/OperatorBuilder.cs:97` | `SignatureKey` omits parameter types for operators and implicit-vs-explicit for conversions, so the CS9025 checked/unchecked check has false negatives. Verified: unchecked `+(OrderId, OrderId)` beside checked `+(OrderId, int)` passes → CS9025. Conversions are worse: `DefineConversion<int>(Implicit)` + `DefineConversion<int>(Explicit).Checked()` share the key → CS9025 **and** CS0557. Contradicts the `Checked()` XML doc. |
| R2-05 | `src/FluentRoslyn/Builders/TypeBuilder.cs:363` | No duplicate-signature guard at all: an implicit and an explicit conversion to the same target both emit → CS0557. Fires with no `Checked()` involved — the most natural mistake when a generator offers both directions. |
| R2-06 | `src/FluentRoslyn/Builders/TypeBuilder.cs:337` | Nothing refuses operators on a **static class**, though `ClassBuilder.Static()` is public and C# forbids it. Verified: CS0715 ×2 plus CS0721 ×4. The analogous member/type-kind check (`AllowsAbstractMembers`) sits three lines above. |
| R2-10 | `src/FluentRoslyn/Builders/OperatorBuilder.cs:197` | `operator true`/`false` can be declared with a non-`bool` result type → CS0215. Verified with `DefineOperator<int>(True)`. Compounding: `OperatorKind.False` appears nowhere outside its enum, and `True` only inside a test that throws during validation before emission — **no test ever reaches the true/false emission path**. |
| R2-11 | `src/FluentRoslyn/Builders/OperatorBuilder.cs:83` | `IsStaticContext => true` only drives the *shadowing* rule; an ordinary unqualified instance-member reference inside an operator emits silently → CS0120. Pre-existing `MethodBuilderBase` behaviour, but there it needs an explicit `.Static()`; operators are unconditionally static, so a whole member family inherits the hazard by default. The shipped example dodges it only by happening to root the reference at a parameter. |

### False rejection — refuses legal code

| id | file | finding |
|---|---|---|
| R2-07 | `src/FluentRoslyn/Builders/TypeBuilder.cs:370` | The pairing check is scoped to one builder's `_operators`, but CS0216 is a **per-type** rule and C# accepts a pair split across partial parts (verified). So a generator emitting into the generated half while the consumer hand-writes the other cannot declare `==` alone — and a reflection sweep found no suppression knob and no raw member-injection route. `.Readonly().Partial()` is exactly what the flagship value-objects generator uses. |
| R2-08 | `src/FluentRoslyn/Builders/OperatorBuilder.cs:98` | The conversion `SignatureKey` interpolates a raw `TypeSyntax`, so two spellings of one type produce different keys. `DefineConversion<int>` (`conversion int`) vs `DefineConversion(Explicit, "System.Int32")` throws on source that compiles. Interior whitespace alone triggers it — and `ISymbol.ToDisplayString()` emits the spaced form, so mixing symbol-derived and hand-written names is the realistic trigger. Outer whitespace *is* tolerated, making it look arbitrary. |

### Validation reachable only through the type

| id | file | finding |
|---|---|---|
| R2-09 | `src/FluentRoslyn/Builders/OperatorBuilder.cs:188` | `ValidateChecked` is called only from `TypeBuilder`, so the member's own `ToString()`/`BuildSyntax()` emits CS9023/CS9024-invalid C# in silence. Member `ToString()` is a documented public path and the suite relies on it triggering member-level validation (`AsyncMethodTests.cs:81,92`). `MethodBuilderBase`, `PropertyBuilder` and `ConstructorBuilder` all validate through `BuildSyntax`; `OperatorBuilder` is the only member builder that does not. Fix is to move the call into `BuildOperator()`. |

### Poor diagnostics and coverage gaps

| id | file | finding |
|---|---|---|
| R2-12 | `src/FluentRoslyn/Builders/OperatorKind.cs:137` | `Tokens[kind]` is an unguarded dictionary indexer reached from public `DefineOperator`, so an out-of-range `OperatorKind` gives a bare `KeyNotFoundException` with no type name, member name or mention of operators. Both the code comment and the shipped release notes claim parity with `EnumBuilder`'s stance, which actually throws a contextual `InvalidOperationException` at build time. |
| R2-13 | `src/FluentRoslyn/Builders/OperatorBuilder.cs:200` | "Not `Implicit`" is treated as "`Explicit`", so an out-of-range `ConversionKind` silently emits `explicit` and walks past the CS9024 guard. Its `Name` becomes `"99 operator int"`. And because `ConversionKind.Implicit = 0`, a computed argument that defaults silently declares an **implicit** conversion — precisely what the value-objects generator's comments say they avoid. Asymmetric with the `OperatorKind` path, which throws (badly). |
| R2-14 | `src/FluentRoslyn/Builders/OperatorKind.cs:110` | `>>>` (C# 11 unsigned right shift) is missing, contradicting the shipped "every overloadable operator is covered" in both the release notes and ROADMAP #39. `SyntaxKind.GreaterThanGreaterThanGreaterThanToken` exists in the pinned Roslyn 4.9.2 and `OperatorDeclaration` accepts it. No escape hatch: `DefineMethod` rejects `"operator >>>"` as a non-identifier, which is the gap this feature exists to close. The `Checked()` work already adopted C# 11, so the version floor is not the obstacle. |

### Documentation that is wrong

| id | file | finding |
|---|---|---|
| R2-15 | `src/FluentRoslyn.Templates/TemplateLifter.cs:209` | The rename-hazard comment added 2026-08-06 asserts "nothing in this repository fails to compile if one of them is renamed" — **empirically false for five of the six names**. `examples/…Templates.Generator` is in the solution and compiles the lifted output: renaming `AsExpressionBody` gives 4 × CS1061 building that project alone, 58 × CS1061 for the solution. The real gap is exactly two names on the `template.ReturnType is null` branch (non-generic `MethodBuilder`, void `DefineMethod(string)`), because all four `[Template]` methods return a value. Fix is one line — add a `void` `[Template]` to the example. Same false premise in `docs/ROADMAP.md` and `FluentRoslyn.Templates.csproj`; **the lockstep conclusion itself is unaffected**. |
| R2-16 | `src/FluentRoslyn.Templates/FluentRoslyn.Templates.csproj` | The preview.8 release notes ("nothing to gain by upgrading this package on its own") contradict the lockstep reasoning committed 19 minutes later, which argues the matching version *is* the compatibility signal. This is the only one of the three texts that consumers see on nuget.org. |
| R2-17 | `docs/ROADMAP.md` | "one substantive release in five" — it is four. |

### Design and maintenance

| id | file | finding |
|---|---|---|
| R2-18 | `src/FluentRoslyn/Builders/TypeBuilder.cs` | `_operators` is typed `List<IMemberSyntaxBuilder>` and validation runs via `OfType<IOperatorMember>()`, so any future member added to that list is silently skipped by every operator check. |
| R2-19 | `src/FluentRoslyn/Builders/RecordBuilder.cs` | `RecordBuilder` cannot declare operators at all, though C# allows them on records. |
| R2-20 | `src/FluentRoslyn/Builders/OperatorKind.cs` | `operator >` emits without a space, unlike all 21 other kinds, and no test pins it. |
| R2-21 | `tests/…/OperatorTests.cs:351` | Asserts only the checked half, where its operator counterpart asserts both. |
| R2-22 | `src/FluentRoslyn/Builders/OperatorBuilder.cs` | `Modifiers()` re-implements `SyntaxFormatting.Modifiers` (used by 12 builders), and `ApplyBody` is a third copy of the expression-body routine, already drifted in wording. |
| R2-23 | `src/FluentRoslyn/Builders/TypeBuilder.cs` | No zero-operator early-out in `ValidateOperatorPairs`: measured 512 B / 8 objects per class or struct built, against a non-allocating sibling check on the line above. |
| R2-24 | `src/FluentRoslyn/Builders/OperatorBuilder.cs` | `TypeNameBuilder.New<TReturn>()` is built twice in a constructor, with `NormalizeWhitespace` running on the discarded path. Also: the orphaned `_methods` comment now sits above `_operators`. |

## Review 1 — 2026-07-22 — the library as it then stood

- **Range:** start → `2977d02`
- **Subject:** the whole library at the time — type builders, member builders, the
  `Syntax*` helpers, generics and constraints.
- **Outcome:** 14 findings, **all fixed**, including 8 correctness bugs: constraint
  ordering, `Static()` + `InitOnly()`, enum non-integral underlying type / out-of-range
  value / duplicate members, write-only bodied properties, and malformed raw fragments.
  Three claims were investigated and **refuted** — verbatim `@` handling was correct, no
  `.WithX()` result was discarded, and `AccessModifier` reference-equality was sound.

## Notes for whoever fixes Review 3

61 findings is a body of work, not a sitting. Some grouping that holds:

- **One override closes R3-01**, the widest finding in the set, and the durable fix is
  to construct accessor scopes from the owning *member* rather than a bare name, so
  the next body-bearing scope (an event accessor, an indexer) cannot repeat it.
- **R3-13 through R3-16, R3-26 and R3-29 are one missing concept**: `BuildMembers` has
  no member-versus-type validation. `IsStaticType` already exists and is consulted for
  exactly one member kind. Doing them together is one guard with six cases.
- **R3-17 through R3-21 are one concept too**: type identity and the generic guard are
  each implemented twice, once correctly (`TypeNameBuilder.For`) and once not. **Fixed
  2026-08-09**, and the fix was to delete the correct copy rather than duplicate it
  again: the generic guard moved onto `TypeDeclarationBuilder.BuildTypeSyntax`, the
  method that *produces* a name, so every caller is covered by construction instead of
  by remembering to ask. R3-60's three copies of the pairing rule and R3-59's two
  copies of the handle check went with it, into `HandleRules` — fixing R3-19 without
  merging them would have left the next hole to be found separately, which is exactly
  how the method side gained an order-proof re-check and the constructor side did not.
  R3-51's *asymmetry* on these three lines is gone too (both sides now render the same
  way); its allocation half, on the per-parameter comparison, is untouched.
- **R3-02, R3-31 through R3-34 and R3-43 are the simplifier**, and several are
  locally decidable despite the syntax-only ceiling. **Fixed 2026-08-09**, and one
  measurement is worth keeping: **a using-directive does not import nested
  namespaces.** `using System;` does not make `Text` mean `System.Text` — verified by
  compiling both, and it is why the visibility rule keys on the namespaces that
  *lexically enclose* the file rather than on the ones it imports. R3-31's "namespaces
  the simplifier itself just imported" is true only of their root segments, which are
  visible from the global namespace like any other. The residual ceiling is stated in
  the `SimplifyTypeNames` doc: a `WithUsing` of a namespace the file never names a type
  from contributes nothing the pass can check.
- **R3-56 through R3-61 are the duplication** the correctness findings keep landing
  in — the same rule implemented twice, one copy guarded. Fixing a correctness
  finding without merging its duplicate leaves the next one to be found again.
  **Fixed 2026-08-09**, R3-59 and R3-60 alongside the correctness findings they
  duplicate, the rest after. Two corrections to the record fell out of doing it:
  **R3-61's `Returns("void")` desync is unreachable, not latent** — `ParseTypeName`
  reports diagnostics for a bare `void`, so `SyntaxParse` refuses it a level earlier,
  and the finding is right that the duplicated fact should go but wrong about why it
  never fired. And **`TypeNameBuilder.ForEmittedName`'s two-callers comment** was
  already rewritten by the simplifier fix. `SourceFile.Types` and
  `InGlobalNamespace()` now have call sites, in tests that emit and compile a
  global-namespace file. Only two of the slice's tests fail against the previous
  source — the rest are a refactor's characterization tests, which is what a
  duplication slice should look like.
- **R3-48 and R3-49 are the two that matter for a generator's inner loop**, and R3-49
  is also a correctness trap (N identical copies of one file).

## Notes for whoever runs the next one

- **Point it at the gap row first.** The operator feature has now been reviewed twice as
  hard as the type-safety stack that everything else rests on.
- **Give it `docs/ROADMAP.md`'s "Deliberate decisions (not gaps)" section up front.** A
  large amount here is unchecked or verbose *on purpose*; without that a reviewer spends
  its budget re-litigating fully-qualified names, raw escape hatches, invariant
  `IReference<T>`, and the distinct-names rule.
- **Ask it to compile emitted output, not just assert on strings.** Only the five
  examples compile what they generate. Source that is syntactically valid but does not
  compile is the exact failure this library exists to prevent, and unit tests asserting
  on `.ToString()` cannot see it. R2-01 through R2-06 are all of that kind.
- **Check the scope it actually chose.** Review 2 scoped itself to the latest feature
  rather than the codebase, which is reasonable default behaviour and was not what was
  wanted. Confirm the range before trusting the coverage.
