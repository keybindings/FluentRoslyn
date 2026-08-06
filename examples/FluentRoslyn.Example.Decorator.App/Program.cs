// LoggingGreeter does not exist in this project's source. The generator read IGreeter
// as an ISymbol, enumerated its members, and emitted a sealed forwarding implementation
// that logs before each call.
using FluentRoslyn.Example.Decorator.App;

IGreeter greeter = new LoggingGreeter(new Greeter());

Console.WriteLine(greeter.Greet("Ada"));
Console.WriteLine($"Count: {greeter.Count}");
greeter.Reset();
Console.WriteLine($"Count: {greeter.Count}");
