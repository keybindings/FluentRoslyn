using System;
using FluentRoslyn.Abstractions;
using FluentRoslyn.Builders;

namespace FluentRoslyn.UnitTests.Builders;

[EmitsAs("MyApp.Gadget")]
internal sealed class GadgetPh;

[EmitsAs("MyApp.NotGadget")]
internal sealed class NotGadgetPh;

/// <summary>
/// Covers <c>this</c> as a reference and construction of a type named by text — the two
/// things a symbol-driven generator needs that the typed surface cannot reach, because
/// the type it is working with exists only as an <c>ISymbol</c> when the generator runs.
/// </summary>
[TestClass]
public class ThisAndRawConstructionTests
{
    [TestMethod]
    public void This_ReturnedFromAFluentSetter_Emits()
    {
        var builder = NamespaceBuilder.Get("MyApp").Class("GadgetBuilder");

        builder.DefineMethod("WithName")
            .WithParameter<string>("name")
            .Returns(builder)
            .Return(builder.This());

        builder.ToString().Should()
            .Contain("public MyApp.GadgetBuilder WithName(string name)").And
            .Contain("return this;");
    }

    // A placeholder pairs `this` with the declaring type, which puts it back on the
    // typed surface -- as a call argument here.
    [TestMethod]
    public void This_Typed_ReachesTheTypedSurface()
    {
        var file = SourceFile.InNamespace("MyApp");

        var registry = file.Class("Registry");
        registry.DefineMethod("Add").WithParameter<GadgetPh>("gadget", out _).AsCallable<GadgetPh>(out var add);

        var gadget = file.Class("Gadget");
        var owner = gadget.DefineField<string>("_owner");
        gadget.DefineMethod("Register")
            .WithParameter<GadgetPh>("registry", out var registryParam)
            .Call(registryParam, add, gadget.This<GadgetPh>());

        file.ToString().Should().Contain("registry.Add(this);");

        owner.Should().NotBeNull();
    }

    // The pairing rule AsCallableOn uses: a placeholder's emitted name and the declaring
    // type's qualified name are the same string.
    [TestMethod]
    public void This_TypedAsTheWrongType_Throws()
    {
        var gadget = NamespaceBuilder.Get("MyApp").Class("Gadget");

        var wrong = () => gadget.This<NotGadgetPh>();

        wrong.Should().Throw<InvalidOperationException>()
            .WithMessage("*cannot hand back a 'this' typed as 'MyApp.NotGadget'*");
    }

    // There is no `this` in a static member, so emitting one would produce source the
    // consumer cannot compile.
    [TestMethod]
    public void This_InAStaticMethod_Throws()
    {
        var builder = NamespaceBuilder.Get("MyApp").Class("GadgetBuilder");

        var emit = () => builder.DefineMethod("Make").Static()
            .Returns(builder)
            .Return(builder.This())
            .ToString();

        emit.Should().Throw<InvalidOperationException>().WithMessage("*is static, so it has no 'this'*");
    }

    [TestMethod]
    public void ThrowIfNull_OnThis_Throws()
    {
        var gadget = NamespaceBuilder.Get("MyApp").Class("Gadget");

        var guard = () => gadget.DefineMethod("Check").ThrowIfNull(gadget.This<GadgetPh>());

        guard.Should().Throw<InvalidOperationException>().WithMessage("*never null*");
    }

    [TestMethod]
    public void NewOfType_EmitsTheConstruction()
    {
        var builder = NamespaceBuilder.Get("MyApp").Class("OrderBuilder");
        var customer = builder.DefineField("_customer", "string");
        var city = builder.DefineField("_city", "global::Consumer.Address");

        builder.DefineMethod("Build")
            .Returns("global::Consumer.Order")
            .Return(Value.NewOfType("global::Consumer.Order", customer, city));

        builder.ToString().Should()
            .Contain("public global::Consumer.Order Build()").And
            .Contain("return new global::Consumer.Order(_customer, _city);");
    }

    [TestMethod]
    public void NewOfType_WithNoArguments_Emits()
    {
        var builder = NamespaceBuilder.Get("MyApp").Class("OrderBuilder");

        builder.DefineMethod("Build")
            .Returns("global::Consumer.Order")
            .Return(Value.NewOfType("global::Consumer.Order"));

        builder.ToString().Should().Contain("return new global::Consumer.Order();");
    }

    // A raw-typed field is a member reference, so a parameter of the same name shadows
    // it and the argument qualifies -- the same rule every other position follows.
    [TestMethod]
    public void NewOfType_QualifiesAShadowedArgument()
    {
        var builder = NamespaceBuilder.Get("MyApp").Class("OrderBuilder");
        var city = builder.DefineField("city", "global::Consumer.Address");

        builder.DefineMethod("Build")
            .WithParameter<string>("city")
            .Returns("global::Consumer.Order")
            .Return(Value.NewOfType("global::Consumer.Order", city));

        builder.ToString().Should().Contain("return new global::Consumer.Order(this.city);");
    }

    [TestMethod]
    public void NewOfType_WithAMalformedTypeName_Throws()
    {
        var construct = () => Value.NewOfType("not a type<");

        construct.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void Return_OnAVoidMethod_Throws()
    {
        var builder = NamespaceBuilder.Get("MyApp").Class("GadgetBuilder");

        var invalid = () => builder.DefineMethod("Nothing").Return(builder.This());

        invalid.Should().Throw<InvalidOperationException>().WithMessage("*returns void*");
    }

    [TestMethod]
    public void Return_WithANullValue_Throws()
    {
        var builder = NamespaceBuilder.Get("MyApp").Class("GadgetBuilder");

        var invalid = () => builder.DefineMethod("Get").Returns("string").Return((IValue)null!);

        invalid.Should().Throw<ArgumentNullException>();
    }
}
