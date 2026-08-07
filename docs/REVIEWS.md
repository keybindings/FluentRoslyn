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
| `2977d02` → `34cb904` (preview.7) | **6,470 across 83 files** | ❌ **never reviewed** |
| `34cb904` → `0163b82` (preview.8 + docs) | 596 across 6 files | ✅ Review 2 |

**The middle row is the gap.** It is roughly 80% of the current library and contains
every item from #14 (typed references) through #38 — the whole type-safety stack,
`SourceFile`, the raw/untyped tier, static calls, `FluentRoslyn.Templates`, and five
example generators. Review 2 was scoped by the tool to the most recent feature, not to
the codebase, so recommending a review and running one has not closed this.

## Review 2 — 2026-08-08 — operator and conversion declarations

- **Range:** `34cb904..0163b82` (`v0.1.0-preview.7` → HEAD)
- **Subject:** roadmap #39 and its follow-up — `OperatorBuilder`, `OperatorKind`, the
  `TypeBuilder` validation block, the value-objects generator's operator members, and
  the lockstep/rename documentation added alongside.
- **Not covered:** everything in the gap row above.
- **Findings:** 24, none fixed at time of writing.

Ranked most severe first. The last ten were below the reporting tool's cap and are
recorded here at the same level of detail as the rest, because a finding dropped for
formatting is still a finding.

### Emitted source that does not compile

| id | file | finding |
|---|---|---|
| R2-01 | `examples/…ValueObjects.Generator/ValueObjectGenerator.cs:118` | Generated `==`/`!=` route to `Equals(T)` → `Value.Equals(…)` unguarded, so comparing default-constructed value objects over a reference type throws `NullReferenceException`. Verified: `a == b`, `a != b` and `arr[0] == arr[1]` over `new CustomerCode[2]` all threw. Before operators this needed an explicit `.Equals()`; now it is reachable through an operator nobody expects to throw, in the pattern the README and CI hold up as canonical. `GetHashCode` has the same hole. Fix: `Value?.Equals(other.Value) ?? other.Value is null`, or `EqualityComparer<T>.Default`. |
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
