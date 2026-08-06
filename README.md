<!-- The banner replaces the h1: GitHub already prints the repo name above the
     README, and nuget.org prints the package name above it. Absolute URL on
     purpose - this README is packed into the nupkg, and nuget.org renders it
     standalone, where a repo-relative path cannot resolve.
     raw.githubusercontent.com is on nuget.org's trusted image domain list. -->
<p align="center">
  <img src="https://raw.githubusercontent.com/keybindings/FluentRoslyn/main/assets/readme-banner.png" alt="FluentRoslyn — readable source generators" width="820" />
</p>

[![CI](https://github.com/keybindings/FluentRoslyn/actions/workflows/ci.yml/badge.svg)](https://github.com/keybindings/FluentRoslyn/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/keybindings/FluentRoslyn/blob/main/LICENSE)

A fluent C# API for generating C# source code — a readable facade over Roslyn's
`SyntaxFactory`.

You describe the code you want with a builder chain; FluentRoslyn produces a
well-formed syntax tree and formats it. Because it builds real syntax nodes
rather than concatenating strings, whole classes of bugs — misplaced braces,
missing commas, bad spacing — are structurally impossible.

```csharp
var user = NamespaceBuilder.Get("MyApp.Models").Class("User");
var id   = user.DefineProperty<int>("Id").GetOnly();
var name = user.DefineProperty<string>("Name");

user.DefineConstructor(AccessModifier.Public)
    .WithParameter<int>("id",      out var idParam)
    .WithParameter<string>("name", out var nameParam)
    .Assign(id, idParam)
    .Assign(name, nameParam);

var code = user.ToString();
```

```csharp
namespace MyApp.Models;
public class User
{
    public User(int id, string name)
    {
        Id = id;
        Name = name;
    }

    public int Id { get; }
    public string Name { get; set; }
}
```

## Why

Roslyn already ships a fluent code-building layer — `SyntaxGenerator` — but it
lives in `Microsoft.CodeAnalysis.Workspaces`, and **the compiler does not ship
that assembly**. The SDK's compiler directory contains only:

```
Microsoft.CodeAnalysis.dll
Microsoft.CodeAnalysis.CSharp.dll
Microsoft.CodeAnalysis.VisualBasic.dll
```

Source generators run inside that process, so they can only bind against what it
provides. The failure is worse than a compile error, because nothing stops you
trying: referencing Workspaces from a generator compiles cleanly — no warning,
even with `EnforceExtendedAnalyzerRules` — and the consuming build still reports
success. The generator just dies at generation time and contributes nothing:

```
warning CS8785: Generator 'MyGenerator' failed to generate source. Exception was
of type 'FileNotFoundException' with message 'Could not load file or assembly
'Microsoft.CodeAnalysis.Workspaces, Version=4.9.0.0 ...'
```

A warning, easily scrolled past, with your generated file silently missing.

So generator authors are left hand-writing verbose `SyntaxFactory` calls or, more
often, concatenating strings and fighting formatting bugs. FluentRoslyn fills that
gap: an intention-revealing builder API targeting `netstandard2.0`, so it works
where `SyntaxGenerator` cannot. The goal is generator code that reads like the
code it produces.

## Requirements

- Target framework: `netstandard2.0` (usable from source generators)
- Roslyn: `Microsoft.CodeAnalysis.CSharp` **4.9.2+** on the consuming side
- Output: 4-space indentation, `\n` line endings (byte-identical across
  operating systems)

> **Status:** published as [`FluentRoslyn`](https://www.nuget.org/packages/FluentRoslyn)
> on nuget.org, currently `0.1.0-preview.5`, with the optional companion package
> [`FluentRoslyn.Templates`](https://www.nuget.org/packages/FluentRoslyn.Templates)
> versioned alongside it. Breaking changes are still on the table
> while the version says preview. See [What's next](#whats-next).

## Building blocks

Everything starts from a `NamespaceBuilder` and flows into a type builder, then
member builders:

```
NamespaceBuilder ──▶ Class / Struct / Record / Enum / Interface
                         └──▶ DefineField / DefineConstructor / DefineProperty / DefineMethod
```

Every builder is fluent (each `With…`/`Define…` returns a builder for chaining)
and renders three ways:

| Call | Returns |
| --- | --- |
| `.ToString()` | the formatted source as a `string` |
| `.ToSourceText()` | a Roslyn `SourceText` (UTF-8) for `context.AddSource(...)` |
| `.BuildCompilationUnit()` | the raw `CompilationUnitSyntax` (escape hatch) |

## Examples

### Positional record

```csharp
NamespaceBuilder.Get("MyApp").Record("Point")
    .WithParameter<int>("X")
    .WithParameter<int>("Y");
```

```csharp
namespace MyApp;
public record Point(int X, int Y);
```

Use `.AsStruct()` for a `record struct`.

### Enum

```csharp
NamespaceBuilder.Get("MyApp").Enum("Access")
    .WithAttribute("Flags")
    .WithUnderlyingType<byte>()
    .AddMember("None", 0)
    .AddMember("Read", 1)
    .AddMember("Write", 2);
```

```csharp
namespace MyApp;
[Flags]
public enum Access : byte
{
    None = 0,
    Read = 1,
    Write = 2
}
```

Member values are validated against the underlying type, and member names must
be unique — invalid input throws rather than emitting code that won't compile.

### Interface with generics

```csharp
var repo = NamespaceBuilder.Get("MyApp").Interface("IRepository")
    .WithTypeParameter("T")
    .WithConstraint("T", "class");
repo.DefineMethod<int>("Count");
repo.DefineMethod("Add").WithParameter<int>("id");
```

```csharp
namespace MyApp;
public interface IRepository<T>
    where T : class
{
    int Count();
    void Add(int id);
}
```

Constraints are emitted in the order C# requires (`class`/`struct` first,
`new()` last) regardless of the order you add them.

### Method bodies

`void` methods default to an empty block; value-returning methods need a body:

```csharp
var calc = NamespaceBuilder.Get("MyApp").Class("Calc");
calc.DefineMethod<int>("Add")
    .WithParameter<int>("a")
    .WithParameter<int>("b")
    .AsExpressionBody("a + b");
```

```csharp
namespace MyApp;
public class Calc
{
    public int Add(int a, int b) => a + b;
}
```

Statement bodies use `.AddStatement("…")` / `.WithBody("…", "…")`. Properties
support the same forms plus initializers, `init`, get-only, and per-accessor
access modifiers.

### Typed references

The opening example's `Assign` calls are the typed alternative to
`AddStatement("Id = id;")` — raw text that nothing checks. Property and field
builders *are* references; a parameter hands one back through an `out` argument,
which keeps the fluent chain intact.

`Assign` takes two `IReference<T>` sharing one `T`, so `Assign(name, idParam)` is
a compile error in *your* generator rather than generated code that won't build.
And when a parameter shadows the member it targets, the member is qualified
automatically:

```csharp
var shadow = NamespaceBuilder.Get("MyApp").Class("Shadow");
var value = shadow.DefineProperty<string>("value");

shadow.DefineConstructor(AccessModifier.Public)
    .WithParameter<string>("value", out var valueParam)
    .Assign(value, valueParam);
```

```csharp
namespace MyApp;
public class Shadow
{
    public Shadow(string value)
    {
        this.value = value;
    }

    public string value { get; set; }
}
```

Without the qualifier that statement would be `value = value;` — legal C# that
silently assigns the parameter to itself, which is exactly the class of bug this
library exists to rule out.

`T` is invariant, so widening (`object` ← `string`, `long` ← `int`) is rejected:
C# generic constraints can't express "implicitly convertible to", and a looser
rule would let the mismatch it exists to catch slip through. Use `AddStatement`
for those.

### Reference paths

A reference need not be a simple name. `Member` and `Item` build one reference out
of another, so `this.a.b` and `arr[i]` can be assigned to as well:

```csharp
var widget = NamespaceBuilder.Get("MyApp").Class("Widget");
var config = widget.DefineField<Uri>("_config");
var items  = widget.DefineField<string[]>("_items");
var byName = widget.DefineField<Dictionary<string, string>>("_byName");

widget.DefineMethod("Configure")
    .WithParameter<string>("host", out var host)
    .WithParameter<int>("index",   out var index)
    .Assign(config.MemberNamed<string>("Host"), host)
    .Assign(items.Item(index), host)
    .Assign(byName.Item("default"), host);
```

```csharp
public void Configure(string host, int index)
{
    _config.Host = host;
    _items[index] = host;
    _byName["default"] = host;
}
```

The result is an ordinary `IReference<T>`, so every position that already took one
accepts a path with no new overload — assignment on either side, `Call` receivers
and arguments, `Return`, `ThrowIfNull`. Paths chain, and when a parameter shadows
the leading name only *that* is qualified — `this.config.Host` — because
everything after the first dot binds in the target's type and can't be shadowed.

`Item` is typed by the container — `IReference<T[]>`, `IReference<List<T>>` and
`IReference<Dictionary<TKey, TValue>>` — so the element type can't be asserted
wrongly and a dictionary key of the wrong type is a compile error. Members come in
two forms, and the distinction is the same one `Assign`/`AssignLiteral` draws:
`Member(labelProperty)` takes the name *and* the type from the member's own
definition, while `MemberNamed<string>("Host")` asserts both, for a member of a
type the generator has no handle to.

This extends *references*, not expressions. A path names a location; it computes
nothing, so there is still no operator and no evaluation to model. One thing it
cannot do: `ThrowIfNull` refuses an element access, because `nameof(items[0])` is
not legal C# — so the guard would emit source the consumer's build rejects.

### Referencing the consumer's types

The typed surface above needs a `T`. A generator driven by the *consumer's* code has
no `T` — it holds an `ISymbol` discovered when the generator runs. So fields and
parameters can also be typed by name:

```csharp
var builder = SourceFile.InNamespace(ns).Class($"{type.Name}Builder");

builder.DefineField("_shipTo", "global::MyApp.Address");

builder.DefineMethod("WithShipTo")
    .WithParameter("shipTo", "global::MyApp.Address")
    .Returns(builder)
    .AddStatement("_shipTo = shipTo;")
    .AddStatement("return this;");
```

`DefineField(name, typeName)` hands back a `RawFieldBuilder`, which is deliberately
**not** an `IReference<T>` — there is no `T` to check against, and a phantom type that
lied would be worse than none. So `Assign` and friends can't reach these members, and
bodies touching them use `AddStatement`. Everything structural is still built rather
than concatenated: modifiers, attributes, docs, and the declaration itself.

The two string arguments could be transposed, which no compiler can catch — so the
name is validated as a C# identifier, and a qualified type name isn't one.

`this` and construction work here too, so a fluent setter and a `Build()` need no raw
statements:

```csharp
builder.DefineMethod("Build")
    .Returns("global::MyApp.Order")
    .Return(Value.NewOfType("global::MyApp.Order", customerField, shipToField));
```

```csharp
public global::MyApp.Order Build()
{
    return new global::MyApp.Order(_customer, _shipTo);
}
```

Both are deliberately untyped — a consumer's constructor has no signature the generator
can check against, and `This()` has no `T` unless a placeholder names it (`This<T>()`
does, and rejoins the typed surface). `This()` picks up the guards for free: using it
from a static member is refused, since there is no `this` to emit.

Assignment between two such members is `AssignRaw`, which *is* checked — not by `T`,
but by comparing both sides' declared type text, the same rule `AsCallable` validates
handles by:

```csharp
builder.DefineMethod("WithShipTo")
    .WithParameter("shipTo", "global::MyApp.Address", out var shipTo)
    .Returns(builder)
    .AssignRaw(shipToField, shipTo)     // throws if the two declared types differ
    .Return(builder.This());
```

It's named apart from `Assign` rather than overloading it. Overloading is provably
safe — the two parameter sets are disjoint — but it wrecks the *other* method's
diagnostics: a mismatched typed `Assign` drops the generic candidate when inference
fails, and the raw overload survives to report "cannot convert … to `IRawReference`",
an interface you never mentioned.

Between them these cover a symbol-driven generator without emitting a single raw
statement. Be clear on what that does and doesn't mean: malformed syntax becomes
impossible and names come from the builders that declared them, but the checks are by
type *text* and happen when the generator runs. That is strictly less than `<T>` — it
is simply the most available when the type exists only as an `ISymbol`.

A complete symbol-driven generator is in
[`examples/`](https://github.com/keybindings/FluentRoslyn/tree/main/examples): it reads
the consumer's constructors and emits a fluent builder for each marked type.

### Referencing generated types

A generated type has no CLR type, so `<T>` cannot name it. Two complements close
the gap. A **builder reference** passes the type's builder where a type name
goes, so the name is spelled once and only a type actually being built can be
referenced:

```csharp
var order = NamespaceBuilder.Get("MyApp.Models").Class("Order");

var svc = NamespaceBuilder.Get("MyApp.Services").Class("OrderService");
svc.DefineMethod("Save").WithParameter(order, "order");
```

An **`[EmitsAs]` placeholder** is a stand-in type declared in the generator's
own assembly; wherever it appears as a type argument, the *emitted* name is
written instead. That lights up the entire typed surface — including
`IReference<T>` and `Assign` — for generated types:

```csharp
[EmitsAs("MyApp.Models.Order")]
internal sealed class OrderPh;

var current = owner.DefineProperty<OrderPh>("Current");  // emits MyApp.Models.Order
```

The placeholder never ships; it exists so the C# compiler holds the definition
and every reference to the same name.

### Typed calls

Raw statements can also misspell a *method* — `AddStatement("x.SetLabl(n);")`
parses fine. `AsCallable` hands back a handle whose asserted signature is
validated against the declared parameters (a handle that exists matches its
method, and the signature freezes afterwards); `Call` then type-checks the
arguments in your generator:

```csharp
var widget = NamespaceBuilder.Get("MyApp.Models").Class("Widget");
widget.DefineMethod("SetLabel").WithParameter<string>("label", out _)
    .AsCallable<string>(out var setLabel);

var owner = NamespaceBuilder.Get("MyApp").Class("Owner");
var current = owner.DefineProperty<OrderPh>("Current");
owner.DefineConstructor(AccessModifier.Public)
    .WithParameter<string>("label", out var labelParam)
    .Call(current, setLabel, labelParam);
```

```csharp
public Owner(string label)
{
    Current.SetLabel(label);
}
```

Shadowed members are `this.`-qualified in every position — receivers, arguments,
assignment targets and values, returns, and guards.

### Returns, literals and guards

A value-returning method carries its return type, so `Return` is checked:

```csharp
var calc = NamespaceBuilder.Get("MyApp").Class("Calc");
var total = calc.DefineField<int>("_total");

calc.DefineMethod<int>("Total").Return(total);        // returning a string field: compile error
calc.DefineMethod<bool>("IsEmpty").ReturnLiteral(true);
```

Compound assignment takes an operator, and `??=` has its own pair since it needs
a target that can be null:

```csharp
run.Assign(total, AssignmentOperator.Add, delta)   // _total += delta;
   .AssignLiteral(count, AssignmentOperator.Subtract, 1)
   .AssignIfNullLiteral(name, "unnamed");          // Name ??= "unnamed";
```

Constants use `AssignLiteral` / `ReturnLiteral`, and a null guard emits the
classic form — deliberately not `ArgumentNullException.ThrowIfNull`, which is
.NET 6+, because the generated code compiles in the *consumer's* framework:

```csharp
user.DefineConstructor(AccessModifier.Public)
    .WithParameter<string>("name", out var nameParam)
    .ThrowIfNull(nameParam)
    .Assign(name, nameParam)
    .AssignLiteral(count, 0);
```

```csharp
public User(string name)
{
    if (name is null)
        throw new System.ArgumentNullException(nameof(name));
    Name = name;
    Count = 0;
}
```

### Property accessors

Accessors take the same statement API through a scope. A getter gets a `Return`
typed to the property; a setter gets the incoming `value` as a typed reference:

```csharp
var backing = widget.DefineField<string>("_name");
widget.DefineProperty<string>("Name")
    .WithGetter(g => g.Return(backing))
    .WithSetter(s => s.ThrowIfNull(s.Value).Assign(backing, s.Value));
```

`value` sits in the setter's scope as a real name, so a member also called
`value` qualifies to `this.value` rather than emitting `value = value;`.

`AsCallableOn` goes one further and types the receiver, so pointing a handle at
the wrong object is also a compile error. Its calls go through `CallOn`:

```csharp
widget.DefineMethod("SetLabel").WithParameter<string>("label", out _)
    .AsCallableOn<WidgetPh, string>(out var setLabel);

owner.DefineConstructor(AccessModifier.Public)
    .WithParameter<string>("label", out var labelParam)
    .CallOn(current, setLabel, labelParam);   // current must be an IReference<WidgetPh>
```

The separate name is deliberate. Sharing `Call` between the two families made a
mismatched receiver report as *"cannot convert `IMethodOn<T>` to `IMethod`"* —
the receiver-typed overload fails type inference and is dropped from the
candidate list before it can complain, so the error came from the untyped
overload that survived, blaming the handle rather than the disagreement. With
distinct names there is one candidate, and you get `CS0411: the type arguments
for method 'CallOn' cannot be inferred` — the same diagnostic a mismatched
`Assign` gives.

No registry pairs the two: a placeholder's emitted name and the declaring type's
qualified name are the same string, because that is what both become in the
generated source. The plain `AsCallable` family remains for receivers with no
placeholder — a type from a shared library, say.

One limit stated rather than papered over: static calls are not modelled, and
`AsCallable` says so instead of emitting instance syntax on a static method.

### Computed values

A value need not be a name or a constant. `Value.New` and `Invoke` produce one from
a constructor or a method that returns something:

```csharp
var file = SourceFile.InNamespace("MyApp");

var widget = file.Class("Widget");
widget.DefineConstructor(AccessModifier.Public)
    .WithParameter<string>("label", out _)
    .AsConstructable<WidgetPh, string>(out var newWidget);
widget.DefineMethod<int>("Measure")
    .WithParameter<string>("text", out _)
    .AsFunction<string>(out var measure);

var owner = file.Class("Owner");
var current = owner.DefineProperty<WidgetPh>("Current");
var size = owner.DefineField<int>("_size");

owner.DefineConstructor(AccessModifier.Public)
    .WithParameter<string>("label", out var label)
    .Assign(current, Value.New(newWidget, label))
    .Assign(size, current.Invoke(measure, label));
```

```csharp
public Owner(string label)
{
    Current = new MyApp.Widget(label);
    _size = Current.Measure(label);
}
```

The constructed type is a type reference like any other, so it is fully qualified by
default. `SimplifyTypeNames()` shortens it — except here, where the file declares its
own `Widget`, so the short name would bind to that declaration instead.

`AsConstructable` is `AsCallable` for constructors — it validates the asserted
signature against the declared parameters and pairs the type argument with the
declaring type, so `Value.New` gives back an `IValue<WidgetPh>` and assigning it to
the wrong property is a compile error. `AsFunction` is a separate handle family
because `IMethod<T1…>` asserts *argument* types and so cannot say what a call
produces; `TResult` comes from `DefineMethod<T>` rather than being asserted, so it
can't disagree with the declared return type. `AsFunctionOn` also checks the
receiver, through `InvokeOn`.

Values compose by nesting — `Call(current, take, current.Invoke(measure, name))` —
and anything nested inside still gets shadow-qualified.

The line this stops at is deliberate and stated as a rule, because the next feature
always looks cheap too: **values are produced, never combined.** Four producers — a
reference, a constant, `new T(…)`, a call's result — and nothing that joins two
values. No `a + b`, no `a == b`, no conditional. A constructor and a method each have
a declaration to check an asserted shape against, which is machinery this library
already has; `a + b` has none, so checking it would mean reimplementing C#'s
conversion rules. Branching and arithmetic stay raw text.

Two things that follow from the split, rather than being extra rules: `Assign`'s
*target* and `ThrowIfNull` still take an `IReference<T>`, because you cannot assign
to a call's result and `nameof` cannot see one. And a call's receiver is a reference
too, so `Factory.Create().Configure()` isn't expressible — that shape wants a named
local, which costs one statement and keeps generated code flat.

### Files, and using directives

`NamespaceBuilder.Get(ns).Class(name)` gives you a type in a file of its own. To
put several types in one file — and to control its usings, namespace style, and
formatting — start from a `SourceFile`:

```csharp
var file = SourceFile.InNamespace("MyApp").SimplifyTypeNames();

file.Class("Repo").DefineField<List<int>>("_items");
file.Record("Box").WithParameter<List<string>>("Values");

context.AddSource("Storage.g.cs", file.ToSourceText());
```

```csharp
using System.Collections.Generic;

namespace MyApp;
public class Repo
{
    private List<int> _items;
}

public record Box(List<string> Values);
```

Usings, `SimplifyTypeNames()`, `BlockScopedNamespace()`, `WithIndentation` and
`WithLineEndings` live on the file rather than on a type, because they describe a
file — two types sharing one cannot disagree about them.

Type references are fully qualified by default, which is always correct.
`SimplifyTypeNames()` shortens them and adds the imports they need. A name offered
by two different namespaces stays fully qualified rather than becoming ambiguous,
and so does one that *any* type in the file declares — the check is per file, not
per type, which is the only way it can be right when types share one.
`WithUsing("System.Linq")` adds a directive explicitly, which is also how you
shorten names inside raw expression strings.

## Compile-checked templates

Every escape hatch above is the same shape: a body the compiler never sees. The
companion package [`FluentRoslyn.Templates`](https://www.nuget.org/packages/FluentRoslyn.Templates)
closes that by inverting it — stop describing the body, and write it:

```csharp
using FluentRoslyn.Templates;

internal static partial class Templates
{
    [Template]
    public static int Add(int a, int b) => a + b;
}
```

That is real C# in your generator project: the compiler checks it, IntelliSense
completes it, and Rename refactors it. A *meta-generator* — a source generator that
runs on your generator project — lifts it into the calls that reproduce it:

```csharp
public static MethodBuilder<int> EmitAdd(TypeBuilder target)
    => target.DefineMethod<int>("Add")
        .WithParameter<int>("a")
        .WithParameter<int>("b")
        .AsExpressionBody("a + b");
```

so your generator just says `Templates.EmitAdd(calc);` and gets an ordinary
`MethodBuilder<int>` that composes with everything else.

This is legal because a generator project is an ordinary library at *its own* build
time. The `netstandard2.0` rule constrains what a generator **is** — it loads into the
compiler process — not what it runs **on**; and generators not seeing each other's
output applies within *one* compilation, while a meta-generator and its target are two.

**What it buys, measured.** Rename `a` and you get `error CS0103` with a **failed
build, in your own project**. The string form it replaces fails as CS8785 — a
*warning*, in your *consumer's* build, which succeeds with the generated code silently
missing. Types in the body are bound and emitted fully qualified, so a template using
`StringBuilder` still compiles once it lands in a file with different usings.

**The ceiling, stated plainly.** Templates today are fixed — no holes for varying
parts yet. When holes arrive they will stay unchecked at the seams, because a hole is
filled when the generator runs, with an expression naming the *consumer's* types, which
do not exist when the template compiles. "Type safe, almost" is the honest maximum.
A runnable three-project chain is in
[`examples/`](https://github.com/keybindings/FluentRoslyn/tree/main/examples), and the
reasoning is in
[`docs/DESIGN-templates.md`](https://github.com/keybindings/FluentRoslyn/blob/main/docs/DESIGN-templates.md).

## Escape hatches

Statement- and expression-bearing members take raw C# text, parsed into the
tree. Malformed fragments are rejected (they don't silently produce broken
source). Assignment is the exception — it has a checked form, see
[Typed references](#typed-references):

- `.AsExpressionBody("a + b")`, `.AddStatement("return x;")`
- `.WithInitializerExpression("new()")`
- `.WithConstraint("T", "IComparable<T>")`

For anything the fluent API can't yet express, `.BuildCompilationUnit()` hands
back the raw `CompilationUnitSyntax` to manipulate directly.

## Using it in a source generator

```csharp
private void Execute(SourceProductionContext context, ...)
{
    var program = NamespaceBuilder.Get("MyApp")
        .Class("Program").Static().Partial();

    program.DefineMethod("HelloFrom", AccessModifier.None)
        .WithParameter<string>("name")
        .Static().Partial()
        .AsExpressionBody("""System.Console.WriteLine($"Hi from '{name}'")""");

    context.AddSource("Program.g.cs", program.ToSourceText());
}
```

A complete, runnable example lives in
[`examples/`](https://github.com/keybindings/FluentRoslyn/tree/main/examples) — a generator
that emits a partial method and a constructor-assigned class (via typed
references) entirely through the fluent API, plus the app that consumes them.

## What's next

Feature-complete for common generator scenarios and published; the remaining work
is long-tail language features and the larger items sketched at the end of the
roadmap. See
[`docs/ROADMAP.md`](https://github.com/keybindings/FluentRoslyn/blob/main/docs/ROADMAP.md)
for the prioritised list, plus the deliberate design decisions behind things
that look like gaps (fully-qualified names, fixed formatting, raw-string
escape hatches).
