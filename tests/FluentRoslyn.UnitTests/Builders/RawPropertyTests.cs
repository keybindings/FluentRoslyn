using System;
using FluentRoslyn.Abstractions;
using FluentRoslyn.Builders;

namespace FluentRoslyn.UnitTests.Builders;

/// <summary>
/// Covers properties whose type is named by text, and the raw null guard. Both were
/// pulled in by the decorator example: implementing a discovered interface means
/// emitting properties whose types come from that interface, which no <c>&lt;T&gt;</c>
/// can name.
/// </summary>
[TestClass]
public class RawPropertyTests
{
    [TestMethod]
    public void DefineProperty_WithANamedType_EmitsAnAutoProperty()
    {
        var decorator = NamespaceBuilder.Get("MyApp").Class("LoggingGreeter");

        decorator.DefineProperty("Current", "global::Consumer.Models.Order");

        decorator.ToString().Should().Contain("public global::Consumer.Models.Order Current { get; set; }");
    }

    [TestMethod]
    public void DefineProperty_WithANamedType_KeepsTheFluentSurface()
    {
        var decorator = NamespaceBuilder.Get("MyApp").Class("LoggingGreeter");

        decorator.DefineProperty("Count", "int", AccessModifier.Internal)
            .GetOnly()
            .WithSummary("How many.")
            .WithAttribute("Obsolete");

        var code = decorator.ToString();

        code.Should()
            .Contain("internal int Count { get; }").And
            .Contain("How many.").And
            .Contain("[Obsolete]");
    }

    [TestMethod]
    public void DefineProperty_WithANamedType_TakesAccessorBodies()
    {
        var decorator = NamespaceBuilder.Get("MyApp").Class("LoggingGreeter");

        decorator.DefineProperty("Count", "int")
            .GetOnly()
            .WithGetterBody("return _inner.Count;");

        decorator.ToString().Should().Contain("return _inner.Count;");
    }

    [TestMethod]
    public void ARawTypedProperty_IsAnUntypedReferenceOnly()
    {
        typeof(RawPropertyBuilder).Should().BeAssignableTo<IRawReference>();
        typeof(RawPropertyBuilder).GetInterfaces()
            .Should().NotContain(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IReference<>));

        typeof(PropertyBuilder<int>).Should().BeAssignableTo<IReference<int>>();
        typeof(PropertyBuilder<int>).Should().NotBeAssignableTo<IRawReference>();
    }

    // A raw property is a member reference like any other, so it can be assigned to.
    [TestMethod]
    public void ARawTypedProperty_CanBeAssignedRaw()
    {
        var decorator = NamespaceBuilder.Get("MyApp").Class("LoggingGreeter");
        var current = decorator.DefineProperty("Current", "global::Consumer.Models.Order");

        decorator.DefineMethod("Set")
            .WithParameter("order", "global::Consumer.Models.Order", out var order)
            .AssignRaw(current, order);

        decorator.ToString().Should().Contain("Current = order;");
    }

    [TestMethod]
    public void AssignRaw_BetweenAPropertyAndAFieldOfDifferentTypes_Throws()
    {
        var decorator = NamespaceBuilder.Get("MyApp").Class("LoggingGreeter");
        var current = decorator.DefineProperty("Current", "global::Consumer.Models.Order");
        var name = decorator.DefineField("_name", "global::Consumer.Models.Name");

        var assign = () => decorator.DefineMethod("Set").AssignRaw(current, name);

        assign.Should().Throw<InvalidOperationException>()
            .WithMessage("*declared 'global::Consumer.Models.Order'*");
    }

    [TestMethod]
    public void ThrowIfNullRaw_GuardsARawParameter()
    {
        var decorator = NamespaceBuilder.Get("MyApp").Class("LoggingGreeter");

        decorator.DefineConstructor(AccessModifier.Public)
            .WithParameter("inner", "global::Consumer.IGreeter", out var inner)
            .ThrowIfNullRaw(inner);

        var code = decorator.ToString();

        code.Should()
            .Contain("if (inner is null)").And
            .Contain("nameof(inner)");
    }

    [TestMethod]
    public void ThrowIfNullRaw_WithNull_Throws()
    {
        var guard = () => NamespaceBuilder.Get("MyApp").Class("C").DefineMethod("M").ThrowIfNullRaw(null!);

        guard.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void RawAndTypedProperties_CoexistInOneType()
    {
        var decorator = NamespaceBuilder.Get("MyApp").Class("LoggingGreeter");

        decorator.DefineProperty<int>("Id");
        decorator.DefineProperty("Current", "global::Consumer.Models.Order");

        var code = decorator.ToString();

        code.Should()
            .Contain("public int Id { get; set; }").And
            .Contain("public global::Consumer.Models.Order Current { get; set; }");
    }
}
