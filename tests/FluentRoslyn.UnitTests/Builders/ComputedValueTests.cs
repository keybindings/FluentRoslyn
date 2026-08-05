using System;
using FluentRoslyn.Abstractions;
using FluentRoslyn.Builders;

namespace FluentRoslyn.UnitTests.Builders;

[EmitsAs("MyApp.Widget")]
internal sealed class WidgetValuePh;

[EmitsAs("MyApp.Other")]
internal sealed class OtherValuePh;

[EmitsAs("Other.Ns.Thing")]
internal sealed class ThingValuePh;

/// <summary>
/// Covers computed values — <c>new T(args)</c> and a call's result. The type matching is
/// a compile-time property and cannot be asserted here; what these pin is the emission,
/// that a computed value works in every value position, that nested values still qualify
/// shadowed members, and the handle guards.
/// </summary>
[TestClass]
public class ComputedValueTests
{
    [TestMethod]
    public void New_WithAnArgument_EmitsObjectCreation()
    {
        var file = SourceFile.InNamespace("MyApp");

        var widget = file.Class("Widget");
        widget.DefineConstructor(AccessModifier.Public)
            .WithParameter<string>("label", out _)
            .AsConstructable<WidgetValuePh, string>(out var newWidget);

        var owner = file.Class("Owner");
        var current = owner.DefineProperty<WidgetValuePh>("Current");
        owner.DefineConstructor(AccessModifier.Public)
            .WithParameter<string>("label", out var labelParam)
            .Assign(current, Value.New(newWidget, labelParam));

        file.ToString().Should().Contain("Current = new MyApp.Widget(label);");
    }

    [TestMethod]
    public void New_Parameterless_Emits()
    {
        var file = SourceFile.InNamespace("MyApp");

        var widget = file.Class("Widget");
        widget.DefineConstructor(AccessModifier.Public).AsConstructable<WidgetValuePh>(out var newWidget);

        var owner = file.Class("Owner");
        var current = owner.DefineProperty<WidgetValuePh>("Current");
        owner.DefineConstructor(AccessModifier.Public).Assign(current, Value.New(newWidget));

        file.ToString().Should().Contain("Current = new MyApp.Widget();");
    }

    // The constructed type routes through TypeNameBuilder like every other type
    // reference, so it shortens and imports under SimplifyTypeNames.
    [TestMethod]
    public void New_UnderSimplifyTypeNames_ShortensTheConstructedType()
    {
        var source = SourceFile.InNamespace("Other.Ns");
        source.Class("Thing").DefineConstructor(AccessModifier.Public)
            .AsConstructable<ThingValuePh>(out var newThing);

        var file = SourceFile.InNamespace("MyApp").SimplifyTypeNames();
        var holder = file.Class("Holder");
        var held = holder.DefineProperty<ThingValuePh>("Held");
        holder.DefineConstructor(AccessModifier.Public).Assign(held, Value.New(newThing));

        file.ToString().Should()
            .Contain("using Other.Ns;").And
            .Contain("Held = new Thing();");
    }

    // ...and it obeys the simplifier's self-declared rule for free: a file that declares
    // a type of that name keeps the construction qualified, because the short name would
    // bind to the declaration instead.
    [TestMethod]
    public void New_OfATypeDeclaredInTheSameFile_StaysQualified()
    {
        var file = SourceFile.InNamespace("MyApp").SimplifyTypeNames();

        var widget = file.Class("Widget");
        widget.DefineConstructor(AccessModifier.Public).AsConstructable<WidgetValuePh>(out var newWidget);

        var owner = file.Class("Owner");
        var current = owner.DefineProperty<WidgetValuePh>("Current");
        owner.DefineConstructor(AccessModifier.Public).Assign(current, Value.New(newWidget));

        file.ToString().Should().Contain("Current = new MyApp.Widget();");
    }

    [TestMethod]
    public void Invoke_AsAnAssignedValue_EmitsTheCall()
    {
        var file = SourceFile.InNamespace("MyApp");

        var widget = file.Class("Widget");
        widget.DefineMethod<int>("Measure")
            .WithParameter<string>("text", out _)
            .AsFunction<string>(out var measure)
            .ReturnLiteral(0);

        var owner = file.Class("Owner");
        var current = owner.DefineProperty<WidgetValuePh>("Current");
        var size = owner.DefineField<int>("_size");
        owner.DefineMethod("Refresh")
            .WithParameter<string>("text", out var textParam)
            .Assign(size, current.Invoke(measure, textParam));

        file.ToString().Should().Contain("_size = Current.Measure(text);");
    }

    [TestMethod]
    public void InvokeOn_ChecksTheReceiverAndEmits()
    {
        var file = SourceFile.InNamespace("MyApp");

        var widget = file.Class("Widget");
        widget.DefineMethod<string>("Describe")
            .AsFunctionOn<WidgetValuePh>(out var describe)
            .ReturnLiteral("x");

        var owner = file.Class("Owner");
        var current = owner.DefineProperty<WidgetValuePh>("Current");
        var name = owner.DefineProperty<string>("Name");
        owner.DefineMethod("Refresh").Assign(name, current.InvokeOn(describe));

        file.ToString().Should().Contain("Name = Current.Describe();");
    }

    // A value works wherever a value is taken, which is the point of widening the value
    // side rather than adding an overload per producer.
    [TestMethod]
    public void ComputedValue_WorksInEveryValuePosition()
    {
        var file = SourceFile.InNamespace("MyApp");

        var widget = file.Class("Widget");
        widget.DefineMethod<int>("Measure")
            .WithParameter<string>("text", out _)
            .AsFunction<string>(out var measure)
            .ReturnLiteral(0);

        var owner = file.Class("Owner");
        var current = owner.DefineProperty<WidgetValuePh>("Current");
        var name = owner.DefineProperty<string>("Name");
        var size = owner.DefineField<int>("_size");
        owner.DefineMethod("Take").WithParameter<int>("n", out _).AsCallable<int>(out var take);

        owner.DefineMethod("Refresh")
            .Call(current, take, current.Invoke(measure, name))
            .Assign(size, AssignmentOperator.Add, current.Invoke(measure, name));

        owner.DefineMethod<int>("Total").Return(current.Invoke(measure, name));

        owner.DefineProperty<int>("Size").WithGetter(g => g.Return(current.Invoke(measure, name)));

        var code = file.ToString();

        code.Should()
            .Contain("Current.Take(Current.Measure(Name));").And
            .Contain("_size += Current.Measure(Name);").And
            .Contain("return Current.Measure(Name);");
    }

    // A computed value has no name of its own, but the values nested inside it do, so
    // they go through the same shadow qualification as any other position.
    [TestMethod]
    public void ComputedValue_QualifiesShadowedMembersInsideIt()
    {
        var file = SourceFile.InNamespace("MyApp");

        var widget = file.Class("Widget");
        widget.DefineConstructor(AccessModifier.Public)
            .WithParameter<string>("label", out _)
            .AsConstructable<WidgetValuePh, string>(out var newWidget);

        var shadow = file.Class("Shadow");
        var label = shadow.DefineProperty<WidgetValuePh>("label");
        shadow.DefineMethod("Set")
            .WithParameter<string>("label", out var labelParam)
            .Assign(label, Value.New(newWidget, labelParam));

        file.ToString().Should().Contain("this.label = new MyApp.Widget(label);");
    }

    [TestMethod]
    public void AsConstructable_AssertingTheWrongArity_Throws()
    {
        var widget = NamespaceBuilder.Get("MyApp").Class("Widget");

        var handle = () => widget.DefineConstructor(AccessModifier.Public)
            .WithParameter<string>("label", out _)
            .AsConstructable<WidgetValuePh>(out _);

        handle.Should().Throw<InvalidOperationException>()
            .WithMessage("*declares 1 parameter(s) but the handle asserts 0*");
    }

    [TestMethod]
    public void AsConstructable_AssertingTheWrongParameterType_Throws()
    {
        var widget = NamespaceBuilder.Get("MyApp").Class("Widget");

        var handle = () => widget.DefineConstructor(AccessModifier.Public)
            .WithParameter<string>("label", out _)
            .AsConstructable<WidgetValuePh, int>(out _);

        handle.Should().Throw<InvalidOperationException>()
            .WithMessage("*is 'string', but the handle asserts 'int'*");
    }

    // The same pairing rule AsCallableOn uses: the placeholder's emitted name and the
    // declaring type's qualified name have to be the same string.
    [TestMethod]
    public void AsConstructable_AssertingTheWrongDeclaringType_Throws()
    {
        var widget = NamespaceBuilder.Get("MyApp").Class("Widget");

        var handle = () => widget.DefineConstructor(AccessModifier.Public)
            .AsConstructable<OtherValuePh>(out _);

        handle.Should().Throw<InvalidOperationException>()
            .WithMessage("*is declared on 'MyApp.Widget', but the handle asserts 'MyApp.Other'*");
    }

    [TestMethod]
    public void AsConstructable_OnAStaticConstructor_Throws()
    {
        var widget = NamespaceBuilder.Get("MyApp").Class("Widget");

        var handle = () => widget.DefineConstructor(AccessModifier.Public).Static()
            .AsConstructable<WidgetValuePh>(out _);

        handle.Should().Throw<InvalidOperationException>()
            .WithMessage("*static constructor cannot be called*");
    }

    // A handle asserts a shape; letting the parameters move afterwards would make the
    // assertion a lie, so the signature freezes.
    [TestMethod]
    public void AsConstructable_ThenAddingAParameter_Throws()
    {
        var widget = NamespaceBuilder.Get("MyApp").Class("Widget");
        var ctor = widget.DefineConstructor(AccessModifier.Public);
        ctor.AsConstructable<WidgetValuePh>(out _);

        var mutate = () => ctor.WithParameter<string>("late");

        mutate.Should().Throw<InvalidOperationException>()
            .WithMessage("*parameters cannot change after that*");
    }

    [TestMethod]
    public void AsFunction_AssertingTheWrongParameterType_Throws()
    {
        var widget = NamespaceBuilder.Get("MyApp").Class("Widget");

        var handle = () => widget.DefineMethod<int>("Measure")
            .WithParameter<string>("text", out _)
            .AsFunction<int>(out _);

        handle.Should().Throw<InvalidOperationException>()
            .WithMessage("*is 'string', but the handle asserts 'int'*");
    }

    [TestMethod]
    public void AsFunctionOn_AssertingTheWrongDeclaringType_Throws()
    {
        var widget = NamespaceBuilder.Get("MyApp").Class("Widget");

        var handle = () => widget.DefineMethod<string>("Describe").AsFunctionOn<OtherValuePh>(out _);

        handle.Should().Throw<InvalidOperationException>()
            .WithMessage("*declared on 'MyApp.Widget', but the handle asserts 'MyApp.Other'*");
    }

    [TestMethod]
    public void AsFunction_OnAStaticMethod_Throws()
    {
        var widget = NamespaceBuilder.Get("MyApp").Class("Widget");

        var handle = () => widget.DefineMethod<int>("Measure").Static().AsFunction(out _);

        handle.Should().Throw<InvalidOperationException>()
            .WithMessage("*static calls are not modelled*");
    }

    [TestMethod]
    public void Value_WithANullArgument_Throws()
    {
        var file = SourceFile.InNamespace("MyApp");
        var widget = file.Class("Widget");
        widget.DefineConstructor(AccessModifier.Public)
            .WithParameter<string>("label", out _)
            .AsConstructable<WidgetValuePh, string>(out var newWidget);

        var construct = () => Value.New(newWidget, null!);

        construct.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void New_WithAHandleNotFromAsConstructable_Throws()
    {
        var construct = () => Value.New(new ForeignConstructor());

        construct.Should().Throw<ArgumentException>()
            .WithMessage("*not created by AsConstructable*");
    }

    // An outside IValue carries no expression, so emission refuses rather than guessing.
    [TestMethod]
    public void Assign_AnOutsideValueImplementation_Throws()
    {
        var widget = NamespaceBuilder.Get("MyApp").Class("Widget");
        var name = widget.DefineProperty<string>("Name");

        var assign = () => widget.DefineMethod("Set").Assign(name, new ForeignValue());

        assign.Should().Throw<ArgumentException>()
            .WithMessage("*IValue that this library did not create*");
    }

    private sealed class ForeignConstructor : IConstructor<WidgetValuePh>;

    private sealed class ForeignValue : IValue<string>;
}
