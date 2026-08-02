using System;
using FluentRoslyn.Builders;

namespace FluentRoslyn.UnitTests.Builders;

/// <summary>
/// Covers <c>Return</c>. The type check itself is a compile-time property —
/// <c>DefineMethod&lt;int&gt;(…).Return(stringRef)</c> does not compile, so it cannot be
/// asserted here; what these tests pin is the emission, the guards, and that chaining
/// through inherited fluent methods keeps the return type.
/// </summary>
[TestClass]
public class ReturnStatementTests
{
    [TestMethod]
    public void Return_Field_Emits()
    {
        var calc = NamespaceBuilder.Get("MyApp").Class("Calc");
        var total = calc.DefineField<int>("_total");
        calc.DefineMethod<int>("Total").Return(total);

        calc.ToString().Should().Contain("public int Total()")
            .And.Contain("return _total;");
    }

    [TestMethod]
    public void Return_Parameter_Emits()
    {
        var echo = NamespaceBuilder.Get("MyApp").Class("Echo");
        echo.DefineMethod<string>("Say").WithParameter<string>("text", out var text).Return(text);

        echo.ToString().Should().Contain("public string Say(string text)")
            .And.Contain("return text;");
    }

    [TestMethod]
    public void Return_Bare_OnVoidMethod_Emits()
    {
        var v = NamespaceBuilder.Get("MyApp").Class("V");
        v.DefineMethod("Go").Return();

        v.ToString().Should().Contain("public void Go()")
            .And.Contain("return;");
    }

    // A bare return in a method that owes a value would not compile, and the raw-string
    // return type means Return(value) cannot be offered either.
    [TestMethod]
    public void Return_Bare_OnRawTypedMethod_Throws()
    {
        var method = NamespaceBuilder.Get("MyApp").Class("R").DefineMethod("X").Returns("T");

        var bare = () => method.Return();

        bare.Should().Throw<InvalidOperationException>()
            .WithMessage("*bare 'return;' would not compile*");
    }

    [TestMethod]
    public void Return_ShadowedMember_QualifiesWithThis()
    {
        var s = NamespaceBuilder.Get("MyApp").Class("S");
        var name = s.DefineProperty<string>("name");
        s.DefineMethod<string>("Get").WithParameter<int>("name").Return(name);

        s.ToString().Should().Contain("return this.name;");
    }

    [TestMethod]
    public void Return_Null_Throws()
    {
        var method = NamespaceBuilder.Get("MyApp").Class("C").DefineMethod<int>("V");

        var nullValue = () => method.Return(null!);

        nullValue.Should().Throw<ArgumentNullException>();
    }

    // The CRTP shape is what keeps Return reachable after inherited fluent calls; if the
    // hierarchy regressed to returning the base type, this would not compile.
    [TestMethod]
    public void Return_RemainsAvailableAfterInheritedFluentMethods()
    {
        var ch = NamespaceBuilder.Get("MyApp").Class("Ch");
        var v = ch.DefineField<int>("_v");
        ch.DefineMethod<int>("Val").Static().WithSummary("doc").WithAttribute("Obsolete").Return(v);

        ch.ToString().Should().Contain("public static int Val()")
            .And.Contain("return _v;");
    }

    [TestMethod]
    public void Returns_BuilderReference_StillWorksOnVoidBuilder()
    {
        var order = NamespaceBuilder.Get("MyApp.Models").Class("Order");
        var svc = NamespaceBuilder.Get("MyApp").Class("Svc");
        svc.DefineMethod("Load").Returns(order).AddStatement("return null;");

        svc.ToString().Should().Contain("public MyApp.Models.Order Load()");
    }

    // The refactor moved parameters and statements to a shared base; this pins that a
    // constructor and a method still apply the shadowing rule identically through it.
    [TestMethod]
    public void SharedStatementSurface_AppliesShadowingIdenticallyOnBothBuilders()
    {
        var type = NamespaceBuilder.Get("MyApp").Class("T");
        var prop = type.DefineProperty<string>("value");

        type.DefineConstructor(AccessModifier.Public)
            .WithParameter<string>("value", out var ctorParam)
            .Assign(prop, ctorParam);

        type.DefineMethod("Set")
            .WithParameter<string>("value", out var methodParam)
            .Assign(prop, methodParam);

        var source = type.ToString();

        source.Should().Contain("public T(string value)")
            .And.Contain("public void Set(string value)");

        // One occurrence per body: the rule now lives in one place, so both qualify.
        source.Split(["this.value = value;"], StringSplitOptions.None).Length.Should().Be(3);
    }
}
