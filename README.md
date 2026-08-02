<!-- The banner replaces the h1: GitHub already prints the repo name above the
     README, and nuget.org prints the package name above it. Absolute URL on
     purpose - this README is packed into the nupkg, and nuget.org renders it
     standalone, where a repo-relative path cannot resolve.
     raw.githubusercontent.com is on nuget.org's trusted image domain list. -->
<p align="center">
  <img src="https://raw.githubusercontent.com/keybindings/FluentRoslyn/main/assets/readme-banner.png" alt="FluentRoslyn — readable source generators" width="820" />
</p>

[![CI](https://github.com/keybindings/FluentRoslyn/actions/workflows/ci.yml/badge.svg)](https://github.com/keybindings/FluentRoslyn/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

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

> **Status:** packaged as `FluentRoslyn` but not yet pushed to nuget.org —
> reference the project directly for now. See [What's next](#whats-next).

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

### Using directives

Type references are fully qualified by default, which is always correct.
`SimplifyTypeNames()` shortens them and adds the imports they need:

```csharp
var repo = NamespaceBuilder.Get("MyApp").Class("Repo").SimplifyTypeNames();
repo.DefineField<List<int>>("_items");
```

```csharp
using System.Collections.Generic;

namespace MyApp;
public class Repo
{
    private List<int> _items;
}
```

A name offered by two different namespaces — or one the file declares itself —
stays fully qualified rather than becoming ambiguous. `WithUsing("System.Linq")`
adds a directive explicitly, which is also how you shorten names inside raw
expression strings.

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

A complete, runnable example lives in [`examples/`](examples/) — a generator
that emits a partial method and a constructor-assigned class (via typed
references) entirely through the fluent API, plus the app that consumes them.

## What's next

Feature-complete for common generator scenarios; the remaining work is
packaging and long-tail language features. See [`docs/ROADMAP.md`](docs/ROADMAP.md)
for the prioritised list, plus the deliberate design decisions behind things
that look like gaps (fully-qualified names, fixed formatting, raw-string
escape hatches).
