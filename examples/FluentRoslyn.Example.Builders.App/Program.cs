// OrderBuilder and AddressBuilder do not exist in this project's source. The generator
// discovered Order and Address as ISymbols, read their constructors, and emitted a
// fluent builder for each — the case most real generators are, and the one that needed
// members typed by name rather than by <T>.
using FluentRoslyn.Example.Builders.App;

var address = new AddressBuilder()
    .WithCity("Bristol")
    .Build();

var order = new OrderBuilder()
    .WithCustomer("Ada")
    .WithQuantity(3)
    .WithShipTo(address)
    .Build();

Console.WriteLine(order);
