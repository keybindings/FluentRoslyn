using System;
using FluentRoslyn.Abstractions;
using FluentRoslyn.Builders;

namespace FluentRoslyn.UnitTests.Builders;

/// <summary>
/// Covers <c>AssignRaw</c>: assignment between two references whose types were given as
/// text. Neither side has a <c>T</c>, so the compiler cannot match them — what is
/// checked instead is the declared type text, exactly, when the generator runs.
/// </summary>
[TestClass]
public class RawAssignmentTests
{
    [TestMethod]
    public void AssignRaw_BetweenAFieldAndAParameter_Emits()
    {
        var builder = NamespaceBuilder.Get("MyApp").Class("OrderBuilder");
        var field = builder.DefineField("_customer", "global::Consumer.Customer");

        builder.DefineMethod("WithCustomer")
            .WithParameter("customer", "global::Consumer.Customer", out var customer)
            .AssignRaw(field, customer);

        builder.ToString().Should().Contain("_customer = customer;");
    }

    // The check the type system cannot do: both sides' declared type text, compared
    // exactly -- the same rule AsCallable validates handles by.
    [TestMethod]
    public void AssignRaw_BetweenDisagreeingTypes_Throws()
    {
        var builder = NamespaceBuilder.Get("MyApp").Class("OrderBuilder");
        var city = builder.DefineField("_city", "global::Consumer.City");

        var assign = () => builder.DefineMethod("WithCustomer")
            .WithParameter("customer", "global::Consumer.Customer", out var customer)
            .AssignRaw(city, customer);

        assign.Should().Throw<InvalidOperationException>()
            .WithMessage("*'_city' is declared 'global::Consumer.City' but 'customer' is 'global::Consumer.Customer'*");
    }

    [TestMethod]
    public void AssignRaw_BetweenTwoFields_Emits()
    {
        var builder = NamespaceBuilder.Get("MyApp").Class("OrderBuilder");
        var from = builder.DefineField("_from", "global::Consumer.City");
        var to = builder.DefineField("_to", "global::Consumer.City");

        builder.DefineMethod("Copy").AssignRaw(to, from);

        builder.ToString().Should().Contain("_to = _from;");
    }

    // A raw field is a member, so a parameter of the same name shadows it and the target
    // qualifies -- the same rule every other reference position follows.
    [TestMethod]
    public void AssignRaw_WhenTheParameterShadowsTheField_QualifiesWithThis()
    {
        var builder = NamespaceBuilder.Get("MyApp").Class("OrderBuilder");
        var field = builder.DefineField("customer", "global::Consumer.Customer");

        builder.DefineMethod("Set")
            .WithParameter("customer", "global::Consumer.Customer", out var customer)
            .AssignRaw(field, customer);

        builder.ToString().Should().Contain("this.customer = customer;");
    }

    [TestMethod]
    public void AssignRaw_InAConstructorBody_Emits()
    {
        var builder = NamespaceBuilder.Get("MyApp").Class("OrderBuilder");
        var field = builder.DefineField("_customer", "global::Consumer.Customer");

        builder.DefineConstructor(AccessModifier.Public)
            .WithParameter("customer", "global::Consumer.Customer", out var customer)
            .AssignRaw(field, customer);

        builder.ToString().Should().Contain("_customer = customer;");
    }

    [TestMethod]
    public void AssignRaw_WithANullOperand_Throws()
    {
        var builder = NamespaceBuilder.Get("MyApp").Class("OrderBuilder");
        var field = builder.DefineField("_customer", "global::Consumer.Customer");
        var method = builder.DefineMethod("Set");

        var nullTarget = () => method.AssignRaw(null!, field);
        var nullValue = () => method.AssignRaw(field, null!);

        nullTarget.Should().Throw<ArgumentNullException>();
        nullValue.Should().Throw<ArgumentNullException>();
    }

    // The reason this is AssignRaw and not an Assign overload, pinned so the naming is
    // not "simplified" later. The two parameter sets are disjoint, so no call could ever
    // bind to the wrong one -- but a mismatched *typed* Assign drops the generic
    // candidate when inference fails, and an overload here would survive to report
    // "cannot convert ... to 'IRawReference'", an interface the caller never mentioned.
    [TestMethod]
    public void TheTwoReferenceKinds_AreDisjoint()
    {
        typeof(RawFieldBuilder).Should().BeAssignableTo<IRawReference>();
        typeof(PropertyBuilder<string>).Should().NotBeAssignableTo<IRawReference>();
        typeof(FieldBuilder<string>).Should().NotBeAssignableTo<IRawReference>();

        typeof(RawFieldBuilder).GetInterfaces()
            .Should().NotContain(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IReference<>));
    }

    [TestMethod]
    public void WithParameter_OutOverload_StillAppendsTheParameter()
    {
        var method = NamespaceBuilder.Get("MyApp").Class("OrderBuilder")
            .DefineMethod("Set")
            .WithParameter("customer", "global::Consumer.Customer", out _)
            .WithParameter<int>("id");

        method.ToString().Should().Contain("Set(global::Consumer.Customer customer, int id)");
    }

    [TestMethod]
    public void WithParameter_OutOverload_ValidatesTheName()
    {
        var method = NamespaceBuilder.Get("MyApp").Class("OrderBuilder").DefineMethod("Set");

        var invalid = () => method.WithParameter("1nvalid", "int", out _);

        invalid.Should().Throw<ArgumentException>();
    }
}
