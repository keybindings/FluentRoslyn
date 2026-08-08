using System;
using FluentRoslyn.Builders;

namespace FluentRoslyn.UnitTests.Builders;

/// <summary>
/// Covers the rules a member must satisfy because of the type it is declared in
/// (review findings R3-13 through R3-16, R3-26 and R3-29) and the static-context rule
/// inside property accessor bodies (R3-01). Every case below previously emitted source
/// that failed in the consumer's build; each now refuses when the generator runs.
/// </summary>
[TestClass]
public class MemberRuleTests
{
    // === R3-01: the four accessor scopes never reported a static context, so every
    // static-context guard was dead inside a property body. Six independent review
    // passes found this. ===

    [TestMethod]
    public void AStaticProperty_CannotReturnAnInstanceMemberFromItsGetter()
    {
        var widget = NamespaceBuilder.Get("MyApp").Class("Widget");
        var count = widget.DefineField<int>("_count");

        var declare = () => widget.DefineProperty<int>("Count").Static()
            .WithGetter(g => g.Return(count));

        declare.Should().Throw<InvalidOperationException>()
            .WithMessage("*cannot reference '_count', an instance member*");
    }

    [TestMethod]
    public void AStaticProperty_CannotAssignAnInstanceMemberFromItsSetter()
    {
        var widget = NamespaceBuilder.Get("MyApp").Class("Widget");
        var count = widget.DefineField<int>("_count");

        var declare = () => widget.DefineProperty<int>("Count").Static()
            .WithSetter(s => s.Assign(count, s.Value));

        declare.Should().Throw<InvalidOperationException>()
            .WithMessage("*cannot reference '_count', an instance member*");
    }

    [TestMethod]
    public void AStaticProperty_CannotReturnThis()
    {
        var widget = NamespaceBuilder.Get("MyApp").Class("Widget");

        var declare = () => widget.DefineProperty("Self", "MyApp.Widget").Static()
            .WithGetter(g => g.Return(widget.This()));

        declare.Should().Throw<InvalidOperationException>().WithMessage("*has no 'this'*");
    }

    [TestMethod]
    public void TheRawAccessorScopes_ReportStaticnessToo()
    {
        var widget = NamespaceBuilder.Get("MyApp").Class("Widget");
        var inner = widget.DefineField("_inner", "global::Consumer.Thing");

        var getter = () => widget.DefineProperty("Current", "int").Static()
            .WithGetter(g => g.Return(inner.MemberRaw("Count")));
        var setter = () => widget.DefineProperty("Other", "global::Consumer.Thing").Static()
            .WithSetter(s => s.AssignRaw(inner, s.Value));

        getter.Should().Throw<InvalidOperationException>().WithMessage("*instance member*");
        setter.Should().Throw<InvalidOperationException>().WithMessage("*instance member*");
    }

    // An instance property is unaffected -- the guard must fire on staticness, not on
    // accessor bodies in general.
    [TestMethod]
    public void AnInstanceProperty_StillReachesInstanceMembers()
    {
        var widget = NamespaceBuilder.Get("MyApp").Class("Widget");
        var count = widget.DefineField<int>("_count");

        widget.DefineProperty<int>("Count").GetOnly().WithGetter(g => g.Return(count));

        widget.ToString().Should().Contain("return _count;");
    }

    // === R3-13: a static class is a bag of static members and nothing else. ===

    [TestMethod]
    public void AStaticClass_RefusesEveryInstanceMemberKind()
    {
        static ClassBuilder Helpers() => NamespaceBuilder.Get("MyApp").Class("Helpers").Static();

        var field = () => { var h = Helpers(); h.DefineField<int>("_count"); return h.ToString(); };
        var property = () => { var h = Helpers(); h.DefineProperty<int>("Count"); return h.ToString(); };
        var method = () => { var h = Helpers(); h.DefineMethod("Go"); return h.ToString(); };
        var constructor = () => { var h = Helpers(); h.DefineConstructor(AccessModifier.Public); return h.ToString(); };
        var iface = () => { var h = Helpers(); h.WithInterface<IDisposable>(); return h.ToString(); };

        field.Should().Throw<InvalidOperationException>().WithMessage("*instance field '_count'*");
        property.Should().Throw<InvalidOperationException>().WithMessage("*instance property 'Count'*");
        method.Should().Throw<InvalidOperationException>().WithMessage("*instance method 'Go'*");
        constructor.Should().Throw<InvalidOperationException>().WithMessage("*instance constructor*");
        iface.Should().Throw<InvalidOperationException>().WithMessage("*cannot implement an interface*");
    }

    [TestMethod]
    public void AStaticClass_AcceptsStaticMembers()
    {
        var helpers = NamespaceBuilder.Get("MyApp").Class("Helpers").Static();
        helpers.DefineField<int>("_count").Static();
        helpers.DefineMethod("Go").Static();

        helpers.ToString().Should().Contain("public static class Helpers");
    }

    // === R3-14: a readonly struct is broken by the *default* member shapes. ===

    [TestMethod]
    public void AReadonlyStruct_RefusesAMutableFieldAndASettableAutoProperty()
    {
        var field = () =>
        {
            var m = NamespaceBuilder.Get("MyApp").Struct("Money").Readonly();
            m.DefineField<int>("_amount");
            return m.ToString();
        };
        var property = () =>
        {
            var m = NamespaceBuilder.Get("MyApp").Struct("Money").Readonly();
            m.DefineProperty<int>("Amount");
            return m.ToString();
        };

        field.Should().Throw<InvalidOperationException>().WithMessage("*mutable instance field '_amount'*");
        property.Should().Throw<InvalidOperationException>().WithMessage("*settable auto-property 'Amount'*");
    }

    [TestMethod]
    public void AReadonlyStruct_AcceptsTheCorrectedShapes()
    {
        var money = NamespaceBuilder.Get("MyApp").Struct("Money").Readonly();
        money.DefineField<int>("_amount").Readonly();
        money.DefineProperty<int>("Amount").GetOnly();

        money.ToString().Should().Contain("public readonly struct Money");
    }

    // === R3-26: CS8983 -- a struct field initializer needs an explicit constructor. ===

    [TestMethod]
    public void AStructFieldInitializer_NeedsAConstructor()
    {
        var money = NamespaceBuilder.Get("MyApp").Struct("Money");
        money.DefineField<int>("_amount").WithInitializer(5);

        var build = () => money.ToString();

        build.Should().Throw<InvalidOperationException>().WithMessage("*must also declare a constructor*");
    }

    [TestMethod]
    public void AStructFieldInitializer_IsFineWithAConstructor()
    {
        var money = NamespaceBuilder.Get("MyApp").Struct("Money");
        money.DefineField<int>("_amount").WithInitializer(5);
        money.DefineConstructor(AccessModifier.Public).WithParameter<int>("seed");

        money.ToString().Should().Contain("private int _amount = 5;");
    }

    // === R3-16: virtual needs a derivable type; partial needs a partial type. ===

    [TestMethod]
    public void Virtual_NeedsATypeThatCanBeDerivedFrom()
    {
        var sealedClass = () =>
        {
            var c = NamespaceBuilder.Get("MyApp").Class("Widget").Sealed();
            c.DefineMethod("Go").Virtual();
            return c.ToString();
        };
        var structKind = () =>
        {
            var s = NamespaceBuilder.Get("MyApp").Struct("Point");
            s.DefineMethod("Go").Virtual();
            return s.ToString();
        };

        sealedClass.Should().Throw<InvalidOperationException>().WithMessage("*virtual method 'Go'*");
        structKind.Should().Throw<InvalidOperationException>().WithMessage("*virtual method 'Go'*");
    }

    [TestMethod]
    public void APartialMember_NeedsAPartialType()
    {
        var widget = NamespaceBuilder.Get("MyApp").Class("Widget");
        widget.DefineMethod("Go").Partial();

        var build = () => widget.ToString();

        build.Should().Throw<InvalidOperationException>().WithMessage("*partial method 'Go' but is not partial*");
    }

    [TestMethod]
    public void APartialMember_IsFineInAPartialType()
    {
        var widget = NamespaceBuilder.Get("MyApp").Class("Widget").Partial();
        widget.DefineMethod("Go", AccessModifier.None).Partial();

        widget.ToString().Should().Contain("partial void Go()");
    }

    // === R3-15: duplicate and colliding names. EnumBuilder has checked its own members
    // since Review 1; type members never got it. ===

    [TestMethod]
    public void TwoMembersOfOneName_AreRefused()
    {
        var widget = NamespaceBuilder.Get("MyApp").Class("Widget");
        widget.DefineField<int>("Value");
        widget.DefineProperty<int>("Value");

        var build = () => widget.ToString();

        build.Should().Throw<InvalidOperationException>().WithMessage("*more than one member named 'Value'*");
    }

    [TestMethod]
    public void TwoMethodsOfOneSignature_AreRefusedButOverloadsAreNot()
    {
        var duplicate = () =>
        {
            var w = NamespaceBuilder.Get("MyApp").Class("Widget");
            w.DefineMethod("Go").WithParameter<int>("a");
            w.DefineMethod("Go").WithParameter<int>("b");
            return w.ToString();
        };

        var overloads = NamespaceBuilder.Get("MyApp").Class("Widget");
        overloads.DefineMethod("Go").WithParameter<int>("a");
        overloads.DefineMethod("Go").WithParameter<string>("a");

        duplicate.Should().Throw<InvalidOperationException>().WithMessage("*more than once*");
        overloads.ToString().Should().Contain("void Go(int a)").And.Contain("void Go(string a)");
    }

    // CS0542 -- the easy one to hit by accident, naming a member after its type.
    [TestMethod]
    public void AMemberNamedAfterItsType_IsRefused()
    {
        var widget = NamespaceBuilder.Get("MyApp").Class("Widget");
        widget.DefineProperty<int>("Widget");

        var build = () => widget.ToString();

        build.Should().Throw<InvalidOperationException>().WithMessage("*its own name*constructor*");
    }
}
