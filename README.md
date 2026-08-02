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
user.DefineProperty<int>("Id").GetOnly();
user.DefineProperty<string>("Name");
user.DefineConstructor(AccessModifier.Public)
    .WithParameter<int>("id")
    .WithParameter<string>("name")
    .AddStatement("Id = id;")
    .AddStatement("Name = name;");

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

`AddStatement("Name = name;")` is raw text — nothing checks that `Name` exists or
that the two sides have the same type. Assignment is common enough in a generated
constructor to be worth checking, so it has a typed form.

Property and field builders *are* references; a parameter hands one back through
an `out` argument, which keeps the fluent chain intact:

```csharp
var user = NamespaceBuilder.Get("MyApp.Models").Class("User");
var id   = user.DefineProperty<int>("Id").GetOnly();
var name = user.DefineProperty<string>("Name");

user.DefineConstructor(AccessModifier.Public)
    .WithParameter<int>("id",      out var idParam)
    .WithParameter<string>("name", out var nameParam)
    .Assign(id, idParam)
    .Assign(name, nameParam);
```

```csharp
public User(int id, string name)
{
    Id = id;
    Name = name;
}
```

`Assign` takes two `IReference<T>` sharing one `T`, so `Assign(name, idParam)` is
a compile error in *your* generator rather than generated code that won't build.
When a parameter shadows the member it targets, the member is qualified
automatically — you get `this.value = value;` instead of a silent self-assignment.

`T` is invariant, so widening (`object` ← `string`, `long` ← `int`) is rejected:
C# generic constraints can't express "implicitly convertible to", and a looser
rule would let the mismatch it exists to catch slip through. Use `AddStatement`
for those.

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
that implements a partial method entirely through the fluent API, plus the app
that consumes it.

## What's next

Feature-complete for common generator scenarios; the remaining work is
packaging and long-tail language features. See [`docs/ROADMAP.md`](docs/ROADMAP.md)
for the prioritised list, plus the deliberate design decisions behind things
that look like gaps (fully-qualified names, fixed formatting, raw-string
escape hatches).
