using FluentRoslyn.Example.Builders;

namespace FluentRoslyn.Example.Builders.App;

/// <summary>A consumer type. The generator only ever sees it as an ISymbol.</summary>
[GenerateBuilder]
public class Order
{
    public Order(string customer, int quantity, Address shipTo)
    {
        Customer = customer;
        Quantity = quantity;
        ShipTo = shipTo;
    }

    public string Customer { get; }

    public int Quantity { get; }

    public Address ShipTo { get; }

    public override string ToString() => $"{Quantity} x for {Customer} to {ShipTo}";
}

/// <summary>A second consumer type, used as a constructor parameter of the first.</summary>
[GenerateBuilder]
public class Address
{
    public Address(string city)
    {
        City = city;
    }

    public string City { get; }

    public override string ToString() => City;
}
