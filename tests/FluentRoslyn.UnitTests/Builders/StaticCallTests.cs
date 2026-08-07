using System;
using FluentRoslyn.Builders;

namespace FluentRoslyn.UnitTests.Builders;

/// <summary>
/// Covers static calls, whose receiver is a type rather than a reference, and
/// <c>Value.Literal</c>, without which a constant could not be a call argument at all.
/// </summary>
[TestClass]
public class StaticCallTests
{
    [TestMethod]
    public void CallStatic_WithATypeArgument_Emits()
    {
        var widget = NamespaceBuilder.Get("MyApp").Class("Widget");

        widget.DefineMethod("New").CallStatic<Guid>(nameof(Guid.NewGuid));

        widget.ToString().Should().Contain("System.Guid.NewGuid();");
    }

    // Most static methods live in a static class, and C# forbids one as a type argument
    // (CS0718) -- so CallStatic<Console> does not compile and this overload exists.
    [TestMethod]
    public void CallStatic_WithATypeofOfAStaticClass_Emits()
    {
        var widget = NamespaceBuilder.Get("MyApp").Class("Widget");

        widget.DefineMethod("Log")
            .CallStatic(typeof(Console), nameof(Console.WriteLine), Value.Literal("hello"));

        widget.ToString().Should().Contain("System.Console.WriteLine(\"hello\");");
    }

    [TestMethod]
    public void CallStaticRaw_EmitsTheTypeAsWritten()
    {
        var widget = NamespaceBuilder.Get("MyApp").Class("Widget");

        widget.DefineMethod("Log")
            .CallStaticRaw("global::Consumer.Diagnostics.Log", "Write", Value.Literal(1));

        widget.ToString().Should().Contain("global::Consumer.Diagnostics.Log.Write(1);");
    }

    [TestMethod]
    public void CallStatic_OnAGeneratedType_UsesItsBuilder()
    {
        var file = SourceFile.InNamespace("MyApp");
        var helpers = file.Class("Helpers").Static();
        var widget = file.Class("Widget");

        widget.DefineMethod("Go").CallStatic(helpers, "Reset");

        file.ToString().Should().Contain("MyApp.Helpers.Reset();");
    }

    // The reason to prefer the type-argument and typeof forms over the raw one: they go
    // through TypeNameBuilder, so they shorten and pull in the import. Raw text cannot.
    [TestMethod]
    public void CallStatic_ShortensUnderSimplifyTypeNames()
    {
        var file = SourceFile.InNamespace("MyApp").SimplifyTypeNames();
        var widget = file.Class("Widget");

        widget.DefineMethod("Log")
            .CallStatic(typeof(Console), nameof(Console.WriteLine), Value.Literal("hello"));

        var code = file.ToString();

        code.Should()
            .Contain("using System;").And
            .Contain("Console.WriteLine(\"hello\");").And
            .NotContain("System.Console.WriteLine");
    }

    [TestMethod]
    public void CallStaticRaw_DoesNotShorten()
    {
        var file = SourceFile.InNamespace("MyApp").SimplifyTypeNames();
        var widget = file.Class("Widget");

        widget.DefineMethod("Log").CallStaticRaw("System.Console", "WriteLine", Value.Literal("hello"));

        file.ToString().Should().Contain("System.Console.WriteLine(\"hello\");");
    }

    [TestMethod]
    public void InvokeStatic_AsAValue_Emits()
    {
        var widget = NamespaceBuilder.Get("MyApp").Class("Widget");

        widget.DefineMethod("New")
            .Returns("System.Guid")
            .Return(Invocations.InvokeStatic<Guid>(nameof(Guid.NewGuid)));

        widget.ToString().Should().Contain("return System.Guid.NewGuid();");
    }

    [TestMethod]
    public void InvokeStaticRaw_AsAValue_Emits()
    {
        var widget = NamespaceBuilder.Get("MyApp").Class("Widget");

        widget.DefineMethod("Read")
            .Returns("string")
            .Return(Invocations.InvokeStaticRaw("global::Consumer.Config", "Read", Value.Literal("key")));

        widget.ToString().Should().Contain("return global::Consumer.Config.Read(\"key\");");
    }

    // A literal is a typed value, so it composes with the checked call families as well
    // as the raw ones.
    [TestMethod]
    public void Literal_IsAcceptedByACheckedCall()
    {
        var file = SourceFile.InNamespace("MyApp");
        var widget = file.Class("Widget");
        widget.DefineMethod("SetLabel").WithParameter<string>("label", out _).AsCallable<string>(out var setLabel);

        var owner = file.Class("Owner");
        var current = owner.DefineField<string>("_current");

        owner.DefineMethod("Go").Call(current, setLabel, Value.Literal("fixed"));

        file.ToString().Should().Contain("_current.SetLabel(\"fixed\");");
    }

    [TestMethod]
    public void Literal_CarriesTheArgumentsThroughShadowQualification()
    {
        var widget = NamespaceBuilder.Get("MyApp").Class("Widget");
        var count = widget.DefineField<int>("count");

        widget.DefineMethod("Log")
            .WithParameter<int>("count", out _)
            .CallStatic(typeof(Console), nameof(Console.WriteLine), count);

        widget.ToString().Should().Contain("System.Console.WriteLine(this.count);");
    }

    [TestMethod]
    public void StaticCall_ValidatesItsNames()
    {
        var method = NamespaceBuilder.Get("MyApp").Class("Widget").DefineMethod("M");

        var badMethod = () => method.CallStatic(typeof(Console), "1nvalid");
        var badType = () => method.CallStaticRaw("not a type<", "Write");
        var nullType = () => method.CallStatic((Type)null!, "Write");

        badMethod.Should().Throw<ArgumentException>();
        badType.Should().Throw<ArgumentException>();
        nullType.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void StaticCall_FromAStaticMethod_IsFine()
    {
        var widget = NamespaceBuilder.Get("MyApp").Class("Widget");

        widget.DefineMethod("Log").Static()
            .CallStatic(typeof(Console), nameof(Console.WriteLine), Value.Literal("hi"));

        widget.ToString().Should().Contain("System.Console.WriteLine(\"hi\");");
    }
}
