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

// default(CustomerCode).Value is null: default(T) exists for every struct no matter
// what its constructors guard, so every generated member must tolerate a null wrapped
// value. Review finding R2-01 -- these four lines threw before the fix.
var missing = default(CustomerCode);
Console.WriteLine($"Default vs value: {missing == code}");
Console.WriteLine($"Default vs default: {missing == default(CustomerCode)}");
Console.WriteLine($"Default hash: {missing.GetHashCode()}");
Console.WriteLine($"Default text: [{missing}]");
