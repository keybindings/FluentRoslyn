# Generatr

[![CI](https://github.com/Cameron097/Generatr/actions/workflows/ci.yml/badge.svg)](https://github.com/Cameron097/Generatr/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

A fluent C# API for generating C# source code — a readable facade over Roslyn's
`SyntaxFactory`.

You describe the code you want with a builder chain; Generatr produces a
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
lives in `Microsoft.CodeAnalysis.Workspaces`, which **source generators cannot
reference**. That leaves generator authors hand-writing verbose `SyntaxFactory`
calls or, more often, concatenating strings and fighting formatting bugs.

Generatr fills that gap: an intention-revealing builder API that targets
`netstandard2.0`, so it works inside incremental source generators. The goal
is generator code that reads like the code it produces.

## Requirements

- Target framework: `netstandard2.0` (usable from source generators)
- Roslyn: `Microsoft.CodeAnalysis.CSharp` **4.9.2+** on the consuming side
- Output: 4-space indentation, `\n` line endings (byte-identical across
  operating systems)

> **Status:** not yet published to NuGet — reference the project directly for
> now. See [What's next](#whats-next).

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
source):

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
