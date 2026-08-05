# Compile-checked templates — design

**Status: proposed, 2026-08-06. Nothing is built in this repo.** The feasibility
claims below are measured, not recalled — see [Measured](#measured), which records
exactly what was run so it can be re-run.

This is the largest item on the roadmap and the one the reference story has been
leading toward. It is also the one most able to go wrong by being designed
optimistically, so this document leads with what was measured and states the ceiling
before the API.

## The problem

Every escape hatch in this library is the same shape: a body the compiler never
sees.

```csharp
calc.DefineMethod<int>("Add")
    .WithParameter<int>("a")
    .WithParameter<int>("b")
    .AsExpressionBody("a + b");        // <- text. Nothing checks it.
```

Rename `a`, and nothing here changes. There is no red squiggle, no failing build in
the generator project, and no refactoring support. The consequence lands in the
*consumer's* build, and — per the measurement this project keeps returning to — often
as a warning nobody reads.

Items #14–#27 shrank this to genuinely computed values, which is as far as typed
builders can go. Statements with real logic will always be text, because a builder
API that could express arbitrary C# *is* a C# compiler.

**The inversion:** stop trying to describe the body, and write it. In C#. Compiled.

```csharp
[Template]
public static int Add(int a, int b) => a + b;
```

A **meta-generator** — a source generator that runs on the *generator project* —
reads that method and lifts it into the emission calls that reproduce it. The author
gets a body the compiler checks, IntelliSense completes, and Rename refactors.

## Measured

Three-level chain: meta-generator → generator → app. All netstandard2.0 where
required. Reproduced 2026-08-06; earlier runs are consistent with it.

**① The chain works, and the middle level really is generated code.** The
meta-generator used `RegisterSourceOutput` — the ordinary generation pass, not the
privileged post-initialization hook — and read the *generator project's own syntax
trees*, finding a `[Template]` method and lifting its expression body. The generator
then consumed that generated constant to emit into the app. The app ran:

```
2 + 3 = 5
```

Both layers landed on disk. Into the generator project, from the meta-generator:

```csharp
// obj/…/generated/MetaGen/MetaGen.TemplateLifter/Lifted.g.cs
internal static class Lifted
{
    public const string AddName = "Add";
    public const string AddBody = @"a + b";
}
```

and into the app, from the generator that consumed it:

```csharp
// obj/…/generated/Gen/Gen.AddGenerator/Calc.g.cs
public static class Calc
{
    public static int Add(int a, int b) => a + b;
}
```

So a generator can genuinely *analyse* a generator. The `netstandard2.0` rule
constrains what a generator **is** — it loads into the compiler process — not what it
runs **on**. And generators not seeing each other's output applies *within one
compilation*; a meta-generator and its target are two compilations, which is exactly
why this works.

**② The check is the strong kind.** This is the measurement that justifies the
feature, and it was worth running rather than assuming. Breaking the template — a
parameter renamed so the body no longer resolves — gives:

```
Templates.cs(10,48): error CS0103: The name 'a' does not exist in the current context
Build FAILED.
```

An **error**, that **stops the build**, in the **generator author's** own compile.
Contrast the failure it replaces: a typo in `AsExpressionBody("a + b")` produces
CS8785 — a *warning*, in the *consumer's* build, which succeeds with the generated
code silently missing.

That is the entire argument for this feature, and it lands on the right side of all
three asymmetries the statement design named: coverage, severity, and whose build.

**③ Holes type-check.** A hole is an ordinary generic method call, so a template
containing one compiles:

```csharp
internal static class Hole
{
    public static T Value<T>(string name) => default!;   // never actually invoked
}

[Template]
public static string Greet(string name) => $"Hello, {name}! You are {Hole.Value<int>("age")}.";
```

The hole's type is available from **syntax alone** — `Hole.Value<int>` carries its
type argument in the tree — so the common case needs no semantic model, though one is
available and is the more robust read.

## The ceiling, stated before the API

**Template holes stay unchecked at the seams, and no amount of design fixes this.**

A hole is filled when the *generator runs*, with an expression that refers to the
*consumer's* types — types that do not exist when the template is compiled. So:

- **Checked:** the template body itself, and the hole's declared type. `Hole.Value<int>`
  is an `int` in the template, and the emitted builder function takes an
  `IValue<int>`, so passing a `string` is a compile error in the generator.
- **Unchecked:** whether the expression finally substituted is valid *in the
  consumer's compilation*. The generator can be handed a perfectly good
  `IValue<int>` naming a member that the consumer's type does not have.

"Type safe, almost" is the honest maximum, and the seam is precisely at the holes.
The correct response is to make holes *narrow and few* — a template whose body is 90%
fixed has 90% of its surface genuinely checked, and that is a large improvement over
100% text.

**A second limit, from the measurement:** the template is compiled as ordinary code in
the generator project, so it can only reference what that project references. A
template for consumer code that uses `System.Text.Json` needs the generator project to
reference `System.Text.Json` — for compile-checking only, never flowed to the
consumer. That is a real constraint on what can be templated, and it should be said
out loud rather than discovered.

## Shape

The meta-generator lifts a `[Template]` method into a function that reproduces it
through the ordinary builder API:

```csharp
// written by the author, in the generator project
[Template]
public static int Add(int a, int b) => a + b;
```

```csharp
// emitted by the meta-generator, into the generator project
internal static partial class Templates
{
    public static MethodBuilder<int> Add(TypeBuilder target)
        => target.DefineMethod<int>("Add")
            .WithParameter<int>("a")
            .WithParameter<int>("b")
            .AsExpressionBody("a + b");
}
```

```csharp
// the generator author's call site
Templates.Add(calc);
```

Three properties worth naming:

- **The output is ordinary builder calls**, not a new emission path. The template
  machinery produces the same `MethodBuilder<int>` everything else produces, so it
  composes with the rest of the library and needs no new emission seam.
- **The signature is lifted too**, not just the body — parameters, return type, name.
  Those are the parts the builder API already checks, so they come free.
- **`AsExpressionBody` still takes text at the bottom.** The text is now *derived from
  compiled C#* rather than typed by hand. The escape hatch did not go away; it stopped
  being hand-written, which is the whole point.

### Holes

A hole becomes a parameter of the emitted function, typed by the hole's type argument:

```csharp
[Template]
public static string Greet() => $"Hello, {Hole.Value<string>("who")}!";
```

```csharp
public static MethodBuilder<string> Greet(TypeBuilder target, IValue<string> who)
    => target.DefineMethod<string>("Greet")
        .AsExpressionBody($"$\"Hello, {{{who-as-text}}}!\"");
```

**The messy part, named rather than hidden:** substitution is textual. The template's
body is text by the time it reaches `AsExpressionBody`, so a hole is spliced into that
text. Inside a string interpolation — the example above — that means splicing into an
interpolated string literal, with its own escaping rules. This is the single ugliest
corner of the feature and the place a first implementation will get bugs.

Two ways to reduce the blast radius, and the second is recommended:

- **(a) Splice text, escape carefully, test heavily.** Works everywhere, and every
  escaping bug is silent until it isn't.
- **(b) Reject holes in positions that need escaping** — inside string and
  interpolated-string literals, inside comments — and require the template to take
  them as a *parameter* instead, which needs no splicing:

  ```csharp
  [Template]
  public static string Greet(string who) => $"Hello, {who}!";   // `who` is a real parameter
  ```

  The emitted function then supplies `who` as a normal argument. Holes remain for the
  positions a parameter cannot reach — a *type*, a *member name*, a *literal constant*.

**Recommended: (b).** It converts the worst class of bug into a diagnostic, and the
cases it rejects have a better spelling anyway. Revisit only if a real template needs
a hole inside a literal.

## What to build, in order

| Order | Step | Notes |
|---|---|---|
| 1 | The three-project chain in `examples/`, built and run by CI | Turns the measurements above into a regression test. Do this first: it is the thing that decays silently |
| 2 | `[Template]` + lifting a *fixed* method — signature and body, no holes | Delivers the whole compile-checking benefit on its own. Everything after this is ergonomics |
| 3 | Value holes as template *parameters* (option b) | No splicing, so no escaping bugs |
| 4 | Name and type holes, which do need splicing, in non-literal positions only | The narrow, escapable subset |
| 5 | Statement bodies, not just expression bodies | Straightforward once the lifting exists |

Step 2 is the honest MVP: a template with no holes is already a body the compiler
checks, which is the whole thesis. Steps 3–5 should each wait for a real generator to
ask, per the roadmap's standing rule.

## Open decisions

1. **Where does the meta-generator ship?** A second package (`FluentRoslyn.Templates`)
   keeps `FluentRoslyn` free of analyzer packaging concerns and lets a consumer take
   the builder API without the meta-generator. *Recommended: a separate package.*
2. **`[Template]` on methods only, or whole classes too?** Whole classes are the
   obvious next ask — lift a type wholesale. *Recommended: methods first;* a class is
   a loop over its members once members work.
3. **Does the emitted function take a `TypeBuilder`, or attach itself?** Taking the
   target reads better at the call site and keeps the function pure.
   *Recommended: take the target.*
4. **Hole spelling:** `Hole.Value<T>("name")` versus an attribute on a parameter. The
   method call is checked by the compiler and visible in the tree without semantics;
   an attribute is tidier but only reaches parameters. *Recommended: keep both — the
   parameter form for values (step 3), the call form for the rest (step 4).*
