using System;
using FluentRoslyn.Abstractions;
using FluentRoslyn.Builders;

namespace FluentRoslyn.UnitTests.Builders;

/// <summary>
/// Covers reaching a member of a type the generator only discovered — <c>MemberRaw</c>,
/// <c>CallRaw</c>, <c>InvokeRaw</c>, and the raw accessor scopes. Nothing here is
/// checked, because there is no signature to check against; what these pin is that the
/// syntax is built rather than concatenated, and that shadow qualification still
/// applies.
/// </summary>
[TestClass]
public class RawMemberAccessTests
{
    [TestMethod]
    public void MemberRaw_ReadsADiscoveredMember()
    {
        var decorator = NamespaceBuilder.Get("MyApp").Class("LoggingGreeter");
        var inner = decorator.DefineField("_inner", "global::Consumer.IGreeter");

        decorator.DefineMethod("Peek")
            .Returns("int")
            .Return(inner.MemberRaw("Count"));

        decorator.ToString().Should().Contain("return _inner.Count;");
    }

    [TestMethod]
    public void MemberRaw_ChainsAndCanBeAssignedTo()
    {
        var decorator = NamespaceBuilder.Get("MyApp").Class("LoggingGreeter");
        var inner = decorator.DefineField("_inner", "global::Consumer.IGreeter");

        decorator.DefineMethod("Set")
            .WithParameter("name", "string", out var name)
            .AssignRaw(inner.MemberRaw("Options").MemberRaw("Name"), name);

        decorator.ToString().Should().Contain("_inner.Options.Name = name;");
    }

    [TestMethod]
    public void CallRaw_ForwardsAVoidMethod()
    {
        var decorator = NamespaceBuilder.Get("MyApp").Class("LoggingGreeter");
        var inner = decorator.DefineField("_inner", "global::Consumer.IGreeter");

        decorator.DefineMethod("Reset").CallRaw(inner, "Reset");

        decorator.ToString().Should().Contain("_inner.Reset();");
    }

    // The handle-based families stop at three arguments because each arity needs its own
    // type parameters. With nothing to check there is nothing to bound, which is what a
    // forwarding generator needs.
    [TestMethod]
    public void CallRaw_TakesAnyArity()
    {
        var decorator = NamespaceBuilder.Get("MyApp").Class("LoggingGreeter");
        var inner = decorator.DefineField("_inner", "global::Consumer.IGreeter");

        var method = decorator.DefineMethod("Forward");
        var arguments = new IValue[5];
        for (var i = 0; i < 5; i++)
        {
            method.WithParameter($"a{i}", "int", out var argument);
            arguments[i] = argument;
        }

        method.CallRaw(inner, "Wide", arguments);

        decorator.ToString().Should().Contain("_inner.Wide(a0, a1, a2, a3, a4);");
    }

    [TestMethod]
    public void InvokeRaw_ForwardsAValueReturningMethod()
    {
        var decorator = NamespaceBuilder.Get("MyApp").Class("LoggingGreeter");
        var inner = decorator.DefineField("_inner", "global::Consumer.IGreeter");

        decorator.DefineMethod("Greet")
            .WithParameter("name", "string", out var name)
            .Returns("string")
            .Return(Invocations.InvokeRaw(inner, "Greet", name));

        decorator.ToString().Should().Contain("return _inner.Greet(name);");
    }

    // The receiver is a member, so a parameter of the same name shadows it -- the same
    // rule every other reference position follows.
    [TestMethod]
    public void RawAccess_QualifiesAShadowedReceiver()
    {
        var decorator = NamespaceBuilder.Get("MyApp").Class("LoggingGreeter");
        var inner = decorator.DefineField("inner", "global::Consumer.IGreeter");

        decorator.DefineMethod("Forward")
            .WithParameter("inner", "global::Consumer.IGreeter", out _)
            .CallRaw(inner, "Reset");

        decorator.ToString().Should().Contain("this.inner.Reset();");
    }

    [TestMethod]
    public void RawGetter_BuildsItsBody()
    {
        var decorator = NamespaceBuilder.Get("MyApp").Class("LoggingGreeter");
        var inner = decorator.DefineField("_inner", "global::Consumer.IGreeter");

        decorator.DefineProperty("Count", "int")
            .GetOnly()
            .WithGetter(g => g.Return(inner.MemberRaw("Count")));

        decorator.ToString().Should().Contain("return _inner.Count;");
    }

    // The setter's `value` carries the property's declared type text, so assigning it
    // into a field declared the same way is still checked.
    [TestMethod]
    public void RawSetter_ValueCarriesTheDeclaredType()
    {
        var decorator = NamespaceBuilder.Get("MyApp").Class("LoggingGreeter");
        var backing = decorator.DefineField("_name", "global::Consumer.Name");

        decorator.DefineProperty("Name", "global::Consumer.Name")
            .WithSetter(s => s.AssignRaw(backing, s.Value));

        decorator.ToString().Should().Contain("_name = value;");
    }

    [TestMethod]
    public void RawSetter_AssigningValueToADisagreeingField_Throws()
    {
        var decorator = NamespaceBuilder.Get("MyApp").Class("LoggingGreeter");
        var backing = decorator.DefineField("_count", "int");

        var build = () => decorator.DefineProperty("Name", "global::Consumer.Name")
            .WithSetter(s => s.AssignRaw(backing, s.Value));

        build.Should().Throw<InvalidOperationException>().WithMessage("*declared 'int'*");
    }

    // A member named the same as the setter's `value` must qualify, or it would silently
    // bind the incoming value instead.
    [TestMethod]
    public void RawSetter_QualifiesAMemberNamedValue()
    {
        var decorator = NamespaceBuilder.Get("MyApp").Class("Shadow");
        var backing = decorator.DefineField("value", "global::Consumer.Name");

        decorator.DefineProperty("Name", "global::Consumer.Name")
            .WithSetter(s => s.AssignRaw(backing, s.Value));

        decorator.ToString().Should().Contain("this.value = value;");
    }

    // A method whose signature is known at generator-compile time but whose body
    // forwards to something the library cannot see -- the ordinary shape in a
    // symbol-driven generator. Without ReturnRaw the only options were dropping to an
    // untyped Returns("bool"), losing the <T> signature, or AddStatement, losing the
    // built syntax as well.
    [TestMethod]
    public void ReturnRaw_ReturnsAnUntypedValueFromATypedMethod()
    {
        var valueObject = NamespaceBuilder.Get("MyApp").Struct("OrderId");
        var value = valueObject.DefineProperty("Value", "int").GetOnly();

        valueObject.DefineMethod<int>("GetHashCode")
            .Override()
            .ReturnRaw(Invocations.InvokeRaw(value, "GetHashCode"));

        var code = valueObject.ToString();

        code.Should()
            .Contain("public override int GetHashCode()").And
            .Contain("return Value.GetHashCode();");
    }

    [TestMethod]
    public void ReturnRaw_WithNull_Throws()
    {
        var method = NamespaceBuilder.Get("MyApp").Class("C").DefineMethod<int>("M");

        var nullValue = () => method.ReturnRaw(null!);

        nullValue.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void RawAccess_ValidatesMemberNames()
    {
        var decorator = NamespaceBuilder.Get("MyApp").Class("LoggingGreeter");
        var inner = decorator.DefineField("_inner", "global::Consumer.IGreeter");

        var member = () => inner.MemberRaw("1nvalid");
        var call = () => decorator.DefineMethod("M").CallRaw(inner, "1nvalid");

        member.Should().Throw<ArgumentException>();
        call.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void RawAccess_WithNullOperands_Throws()
    {
        var decorator = NamespaceBuilder.Get("MyApp").Class("LoggingGreeter");
        var inner = decorator.DefineField("_inner", "global::Consumer.IGreeter");

        var nullTarget = () => ((IReference)null!).MemberRaw("Count");
        var nullArgument = () => decorator.DefineMethod("M").CallRaw(inner, "Go", null!, null!);

        nullTarget.Should().Throw<ArgumentNullException>();
        nullArgument.Should().Throw<ArgumentNullException>();
    }
}
