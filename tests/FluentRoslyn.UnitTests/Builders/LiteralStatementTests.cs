using System;
using FluentRoslyn.Builders;

namespace FluentRoslyn.UnitTests.Builders;

/// <summary>
/// Covers literal values in statements — <c>AssignLiteral</c> and <c>ReturnLiteral</c>.
/// The type match is a compile-time property (<c>AssignLiteral(intProperty, "text")</c>
/// does not compile), so what these tests pin is the emission and the guards.
/// </summary>
[TestClass]
public class LiteralStatementTests
{
    [TestMethod]
    public void AssignLiteral_CoversThePrimitiveForms()
    {
        var defaults = NamespaceBuilder.Get("MyApp").Class("Defaults");
        var count = defaults.DefineProperty<int>("Count");
        var name = defaults.DefineProperty<string>("Name");
        var ok = defaults.DefineProperty<bool>("Ok");
        var ratio = defaults.DefineProperty<double>("Ratio");
        var small = defaults.DefineProperty<byte>("Small");

        defaults.DefineConstructor(AccessModifier.Public)
            .AssignLiteral(count, 0)
            .AssignLiteral(name, "unnamed")
            .AssignLiteral(ok, true)
            .AssignLiteral(ratio, 1.5)
            .AssignLiteral(small, (byte)7);

        defaults.ToString().Should()
            .Contain("Count = 0;")
            .And.Contain("Name = \"unnamed\";")
            .And.Contain("Ok = true;")
            .And.Contain("Ratio = 1.5;")
            .And.Contain("Small = 7;");
    }

    // null needs no separate method: the target fixes TValue, so it converts when the
    // target is a reference type and is rejected when it is not.
    [TestMethod]
    public void AssignLiteral_Null_Emits()
    {
        var c = NamespaceBuilder.Get("MyApp").Class("C");
        var name = c.DefineProperty<string>("Name");
        c.DefineConstructor().AssignLiteral(name, null);

        c.ToString().Should().Contain("Name = null;");
    }

    [TestMethod]
    public void AssignLiteral_ShadowedTarget_QualifiesWithThis()
    {
        var s = NamespaceBuilder.Get("MyApp").Class("S");
        var value = s.DefineProperty<int>("value");
        s.DefineMethod("Reset").WithParameter<int>("value").AssignLiteral(value, 0);

        s.ToString().Should().Contain("this.value = 0;");
    }

    [TestMethod]
    public void AssignLiteral_WorksOnConstructorsAndMethodsAlike()
    {
        var t = NamespaceBuilder.Get("MyApp").Class("T");
        var count = t.DefineProperty<int>("Count");
        t.DefineConstructor().AssignLiteral(count, 1);
        t.DefineMethod("Reset").AssignLiteral(count, 0);

        t.ToString().Should().Contain("Count = 1;").And.Contain("Count = 0;");
    }

    [TestMethod]
    public void AssignLiteral_UnsupportedType_ThrowsNamingBothEscapeHatches()
    {
        var g = NamespaceBuilder.Get("MyApp").Class("G");
        var id = g.DefineProperty<Guid>("Id");

        var assign = () => g.DefineConstructor().AssignLiteral(id, Guid.Empty);

        assign.Should().Throw<NotSupportedException>()
            .WithMessage("*No literal form for 'System.Guid'*")
            .WithMessage("*AddStatement for a statement*");
    }

    [TestMethod]
    public void ReturnLiteral_Emits()
    {
        var r = NamespaceBuilder.Get("MyApp").Class("R");
        r.DefineMethod<bool>("IsValid").ReturnLiteral(true);
        r.DefineMethod<string>("Tag").ReturnLiteral("fixed");
        r.DefineMethod<int>("Zero").ReturnLiteral(0);

        r.ToString().Should()
            .Contain("return true;")
            .And.Contain("return \"fixed\";")
            .And.Contain("return 0;");
    }

    [TestMethod]
    public void ReturnLiteral_SatisfiesTheBodyRequirement()
    {
        var r = NamespaceBuilder.Get("MyApp").Class("R");
        r.DefineMethod<int>("Answer").ReturnLiteral(42);

        // A value-returning method with no body throws; this one has one.
        r.ToString().Should().Contain("public int Answer()").And.Contain("return 42;");
    }
}
