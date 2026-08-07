// The constructor, Value property, Equals, GetHashCode and ToString below are all
// generated. OrderId and CustomerCode are declared in this project as empty partial
// structs; the generator supplies the identical member set to each.
using FluentRoslyn.Example.ValueObjects.App;

var first = new OrderId(42);
var same = new OrderId(42);
var other = new OrderId(7);

Console.WriteLine($"OrderId: {first}");
Console.WriteLine($"Equal: {first.Equals(same)}");
Console.WriteLine($"Differ: {first.Equals(other)}");
Console.WriteLine($"Hashes match: {first.GetHashCode() == same.GetHashCode()}");

// == and != are generated too, and the explicit conversion unwraps.
Console.WriteLine($"Operator equal: {first == same}");
Console.WriteLine($"Operator differ: {first != other}");
Console.WriteLine($"Unwrapped: {(int)first}");

var code = new CustomerCode("ADA");
Console.WriteLine($"CustomerCode: {code}");
Console.WriteLine($"Boxed equals: {code.Equals((object)new CustomerCode("ADA"))}");
