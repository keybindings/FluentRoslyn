using Generatr.Builders;

namespace Generatr.UnitTests.Builders;

[TestClass]
public class MethodBuilderTests
{
    [TestMethod]
    public void DefineMethod_VoidNoBody_EmitsEmptyBlock()
    {
        var mb = NewClass().DefineMethod("DoThing");

        mb.ToString().Should().Be(string.Join("\n",
            "public void DoThing()",
            "{",
            "}"));
    }

    [TestMethod]
    public void Static_EmitsStaticKeyword()
    {
        var mb = NewClass().DefineMethod("DoThing").Static();

        mb.ToString().Should().StartWith("public static void DoThing()");
    }

    [TestMethod]
    public void WithAccessModifier_OverridesTheAccessModifier()
    {
        var mb = NewClass().DefineMethod("DoThing").WithAccessModifier(AccessModifier.Private);

        mb.ToString().Should().StartWith("private void DoThing()");
    }

    [TestMethod]
    public void WithParameter_AppendsParameters()
    {
        var mb = NewClass().DefineMethod("DoThing")
            .WithParameter<int>("count")
            .WithParameter<string>("name");

        mb.ToString().Should().StartWith("public void DoThing(int count, string name)");
    }

    [TestMethod]
    public void WithParameter_ComposesWithDefineMethodParameters()
    {
        var mb = NewClass().DefineMethod("DoThing", AccessModifier.Public, Parameter<int>.New("count"))
            .WithParameter<string>("name");

        mb.ToString().Should().StartWith("public void DoThing(int count, string name)");
    }

    [TestMethod]
    public void VoidExpressionBody_EmitsArrowWithSemicolon()
    {
        var mb = NewClass().DefineMethod("DoThing").AsExpressionBody("System.Console.WriteLine(\"x\")");

        mb.ToString().Should().Be("public void DoThing() => System.Console.WriteLine(\"x\");");
    }

    [TestMethod]
    public void ReturningMethod_WithExpressionBody_EmitsReturnTypeAndArrow()
    {
        var mb = NewClass().DefineMethod<int>("Add")
            .WithParameter<int>("a")
            .WithParameter<int>("b")
            .AsExpressionBody("a + b");

        mb.ToString().Should().Be("public int Add(int a, int b) => a + b;");
    }

    [TestMethod]
    public void ReturningMethod_GenericReturnType_QualifiesTheType()
    {
        var mb = NewClass().DefineMethod<System.Collections.Generic.List<int>>("Make")
            .AsExpressionBody("new()");

        mb.ToString().Should().Be("public System.Collections.Generic.List<int> Make() => new();");
    }

    [TestMethod]
    public void ReturningMethod_WithoutBody_Throws()
    {
        var mb = NewClass().DefineMethod<int>("Add");

        var act = () => mb.ToString();

        act.Should().Throw<System.NotImplementedException>().WithMessage("*needs a body*");
    }

    [TestMethod]
    public void FluentMethods_MutateInPlace_ReturningTheSameInstance()
    {
        var mb = NewClass().DefineMethod("DoThing");

        mb.Static().Should().BeSameAs(mb);
    }

    [TestMethod]
    public void DefineMethodReturning_ReachesClassOutput()
    {
        var cb = NewClass();
        cb.DefineMethod<int>("Add").WithParameter<int>("a").AsExpressionBody("a + 1");

        cb.ToString().Should().Be(string.Join("\n",
            "namespace TestNamespace;",
            "public class TestClass",
            "{",
            "    public int Add(int a) => a + 1;",
            "}"));
    }

    private static ClassBuilder NewClass(string name = "TestClass")
        => NamespaceBuilder.Get("TestNamespace").Class(name);
}
