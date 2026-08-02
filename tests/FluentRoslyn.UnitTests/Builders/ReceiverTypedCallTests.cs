using System;
using FluentRoslyn.Abstractions;
using FluentRoslyn.Builders;

namespace FluentRoslyn.UnitTests.Builders;

[EmitsAs("MyApp.Models.Gadget")]
internal sealed class GadgetPlaceholder;

[EmitsAs("MyApp.Models.Other")]
internal sealed class OtherPlaceholder;

/// <summary>
/// Covers receiver-typed call handles. The receiver check itself is a compile-time
/// property — <c>Call(otherRef, gadgetHandle)</c> does not compile, so it cannot be
/// asserted here; what these tests pin is the pairing rule that makes it possible, and
/// the emission.
/// </summary>
[TestClass]
public class ReceiverTypedCallTests
{
    [TestMethod]
    public void CallOn_WithMatchingReceiver_Emits()
    {
        var gadget = NamespaceBuilder.Get("MyApp.Models").Class("Gadget");
        gadget.DefineMethod("Reset").AsCallableOn<GadgetPlaceholder>(out var reset);
        gadget.DefineMethod("SetLabel").WithParameter<string>("label", out _)
            .AsCallableOn<GadgetPlaceholder, string>(out var setLabel);

        var owner = NamespaceBuilder.Get("MyApp").Class("Owner");
        var current = owner.DefineProperty<GadgetPlaceholder>("Current");
        owner.DefineConstructor(AccessModifier.Public)
            .WithParameter<string>("label", out var labelParam)
            .Call(current, reset)
            .Call(current, setLabel, labelParam);

        owner.ToString().Should().Contain("Current.Reset();")
            .And.Contain("Current.SetLabel(label);");
    }

    [TestMethod]
    public void CallOn_FromMethodBody_Emits()
    {
        var gadget = NamespaceBuilder.Get("MyApp.Models").Class("Gadget");
        gadget.DefineMethod("Move")
            .WithParameter<int>("x", out _).WithParameter<int>("y", out _)
            .AsCallableOn<GadgetPlaceholder, int, int>(out var move);

        var owner = NamespaceBuilder.Get("MyApp").Class("Owner");
        var current = owner.DefineProperty<GadgetPlaceholder>("Current");
        owner.DefineMethod("Nudge")
            .WithParameter<int>("dx", out var dx)
            .WithParameter<int>("dy", out var dy)
            .Call(current, move, dx, dy);

        owner.ToString().Should().Contain("Current.Move(dx, dy);");
    }

    [TestMethod]
    public void CallOn_ReceiverShadowedByParameter_QualifiesWithThis()
    {
        var gadget = NamespaceBuilder.Get("MyApp.Models").Class("Gadget");
        gadget.DefineMethod("Reset").AsCallableOn<GadgetPlaceholder>(out var reset);

        var shadow = NamespaceBuilder.Get("MyApp").Class("Shadow");
        var member = shadow.DefineProperty<GadgetPlaceholder>("gadget");
        shadow.DefineMethod("Bump").WithParameter<string>("gadget", out _)
            .Call(member, reset);

        shadow.ToString().Should().Contain("this.gadget.Reset();");
    }

    // The pairing rule: the placeholder's emitted name and the declaring type's
    // qualified name have to be the same string, because that is what both become in
    // the generated source.
    [TestMethod]
    public void AsCallableOn_WrongDeclaringType_Throws()
    {
        var method = NamespaceBuilder.Get("MyApp.Models").Class("Gadget").DefineMethod("X");

        var handle = () => method.AsCallableOn<OtherPlaceholder>(out _);

        handle.Should().Throw<InvalidOperationException>()
            .WithMessage("*declared on 'MyApp.Models.Gadget', but the handle asserts 'MyApp.Models.Other'*");
    }

    [TestMethod]
    public void AsCallableOn_NestedDeclaringType_PairsOnQualifiedName()
    {
        var inner = NamespaceBuilder.Get("MyApp").Class("Outer").DefineClass("Inner");

        var handle = () => inner.DefineMethod("Ping").AsCallableOn<GadgetPlaceholder>(out _);

        handle.Should().Throw<InvalidOperationException>()
            .WithMessage("*declared on 'MyApp.Outer.Inner'*");
    }

    [TestMethod]
    public void AsCallableOn_StillValidatesParameters()
    {
        var method = NamespaceBuilder.Get("MyApp.Models").Class("Gadget")
            .DefineMethod("Y").WithParameter<int>("n");

        var handle = () => method.AsCallableOn<GadgetPlaceholder, string>(out _);

        handle.Should().Throw<InvalidOperationException>()
            .WithMessage("*is 'int', but the handle asserts 'string'*");
    }

    [TestMethod]
    public void AsCallableOn_FreezesParametersToo()
    {
        var method = NamespaceBuilder.Get("MyApp.Models").Class("Gadget")
            .DefineMethod("Z").AsCallableOn<GadgetPlaceholder>(out _);

        var add = () => method.WithParameter<int>("late");

        add.Should().Throw<InvalidOperationException>()
            .WithMessage("*parameters cannot change after that*");
    }
}
