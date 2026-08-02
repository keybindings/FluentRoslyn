using System;
using FluentRoslyn.Abstractions;
using FluentRoslyn.Builders;

namespace FluentRoslyn.UnitTests.Builders;

[EmitsAs("MyApp.Models.Widget")]
internal sealed class WidgetPlaceholder;

/// <summary>
/// Covers typed method calls: <c>AsCallable</c> handles whose asserted signature is
/// validated against the declared parameters, and <c>Call</c> statements whose argument
/// types the compiler matches against the handle.
/// </summary>
[TestClass]
public class TypedCallTests
{
    private static MethodBuilder DefineSetLabel(out IMethod<string> setLabel)
        => NamespaceBuilder.Get("MyApp.Models").Class("Widget")
            .DefineMethod("SetLabel").WithParameter<string>("label", out _)
            .AsCallable(out setLabel);

    [TestMethod]
    public void Call_FromConstructor_EmitsInvocations()
    {
        var widget = NamespaceBuilder.Get("MyApp.Models").Class("Widget");
        widget.DefineMethod("Reset").AsCallable(out var reset);
        widget.DefineMethod("SetLabel").WithParameter<string>("label", out _)
            .AsCallable<string>(out var setLabel);

        var owner = NamespaceBuilder.Get("MyApp").Class("Owner");
        var current = owner.DefineProperty<WidgetPlaceholder>("Current");
        owner.DefineConstructor(AccessModifier.Public)
            .WithParameter<string>("label", out var labelParam)
            .Call(current, reset)
            .Call(current, setLabel, labelParam);

        owner.ToString().Should().Contain("Current.Reset();")
            .And.Contain("Current.SetLabel(label);");
    }

    [TestMethod]
    public void Call_FromMethod_WithTwoArguments_Emits()
    {
        var widget = NamespaceBuilder.Get("MyApp.Models").Class("Widget");
        widget.DefineMethod("Move").WithParameter<int>("x", out _).WithParameter<int>("y", out _)
            .AsCallable<int, int>(out var move);

        var owner = NamespaceBuilder.Get("MyApp").Class("Owner");
        var current = owner.DefineProperty<WidgetPlaceholder>("Current");
        owner.DefineMethod("Nudge")
            .WithParameter<int>("dx", out var dx)
            .WithParameter<int>("dy", out var dy)
            .Call(current, move, dx, dy);

        owner.ToString().Should().Contain("Current.Move(dx, dy);");
    }

    [TestMethod]
    public void Call_ReceiverShadowedByParameter_QualifiesWithThis()
    {
        var widget = NamespaceBuilder.Get("MyApp.Models").Class("Widget");
        widget.DefineMethod("Reset").AsCallable(out var reset);

        var shadow = NamespaceBuilder.Get("MyApp").Class("Shadow");
        var member = shadow.DefineProperty<WidgetPlaceholder>("widget");
        shadow.DefineMethod("Bump").WithParameter<string>("widget", out _)
            .Call(member, reset);

        shadow.ToString().Should().Contain("this.widget.Reset();");
    }

    [TestMethod]
    public void Call_ArgumentShadowedByParameter_QualifiesWithThis()
    {
        DefineSetLabel(out var setLabel);

        var owner = NamespaceBuilder.Get("MyApp").Class("Owner");
        var current = owner.DefineProperty<WidgetPlaceholder>("Current");
        var label = owner.DefineProperty<string>("label");
        owner.DefineMethod("Apply").WithParameter<int>("label")
            .Call(current, setLabel, label);

        owner.ToString().Should().Contain("Current.SetLabel(this.label);");
    }

    // The same qualification now covers Assign's value side: a member on the right of an
    // assignment, shadowed by a parameter, would otherwise silently bind the parameter.
    [TestMethod]
    public void Assign_ValueSideShadowedByParameter_QualifiesWithThis()
    {
        var owner = NamespaceBuilder.Get("MyApp").Class("Owner");
        var target = owner.DefineProperty<string>("Target");
        var source = owner.DefineProperty<string>("source");
        owner.DefineMethod("Copy").WithParameter<int>("source")
            .Assign(target, source);

        owner.ToString().Should().Contain("Target = this.source;");
    }

    [TestMethod]
    public void AsCallable_ArityMismatch_Throws()
    {
        var method = NamespaceBuilder.Get("MyApp").Class("W").DefineMethod("A").WithParameter<int>("x");

        var handle = () => method.AsCallable(out _);

        handle.Should().Throw<InvalidOperationException>()
            .WithMessage("*declares 1 parameter(s) but the handle asserts 0*");
    }

    [TestMethod]
    public void AsCallable_TypeMismatch_Throws()
    {
        var method = NamespaceBuilder.Get("MyApp").Class("W").DefineMethod("B").WithParameter<int>("x");

        var handle = () => method.AsCallable<string>(out _);

        handle.Should().Throw<InvalidOperationException>()
            .WithMessage("*is 'int', but the handle asserts 'string'*");
    }

    [TestMethod]
    public void AsCallable_ValidatesAcrossDeclarationStyles()
    {
        var model = NamespaceBuilder.Get("MyApp.Models").Class("Widget2");
        var method = NamespaceBuilder.Get("MyApp").Class("H").DefineMethod("Put");
        method.WithParameter(model, "w");

        var handle = () => method.AsCallable<WidgetPlaceholder>(out _);

        handle.Should().Throw<InvalidOperationException>()
            .WithMessage("*is 'MyApp.Models.Widget2', but the handle asserts 'MyApp.Models.Widget'*");
    }

    [TestMethod]
    public void WithParameter_AfterHandleIssued_Throws()
    {
        var method = NamespaceBuilder.Get("MyApp").Class("W").DefineMethod("C").AsCallable(out _);

        var add = () => method.WithParameter<int>("late");

        add.Should().Throw<InvalidOperationException>()
            .WithMessage("*parameters cannot change after that*");
    }

    [TestMethod]
    public void AsCallable_OnStaticMethod_Throws()
    {
        var method = NamespaceBuilder.Get("MyApp").Class("W").DefineMethod("D").Static();

        var handle = () => method.AsCallable(out _);

        handle.Should().Throw<InvalidOperationException>()
            .WithMessage("*static calls are not modelled yet*");
    }

    [TestMethod]
    public void StaticAfterHandle_ThrowsAtEmission()
    {
        var type = NamespaceBuilder.Get("MyApp").Class("W");
        type.DefineMethod("E").AsCallable(out _).Static();

        var emit = () => type.ToString();

        emit.Should().Throw<InvalidOperationException>()
            .WithMessage("*became static after issuing a callable handle*");
    }

    private sealed class ForeignMethod : IMethod
    {
    }

    [TestMethod]
    public void Call_WithForeignHandleImplementation_Throws()
    {
        var owner = NamespaceBuilder.Get("MyApp").Class("Owner");
        var current = owner.DefineProperty<WidgetPlaceholder>("Current");

        var call = () => owner.DefineMethod("Go").Call(current, new ForeignMethod());

        call.Should().Throw<ArgumentException>()
            .WithMessage("*not created by AsCallable*");
    }
}
