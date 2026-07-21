using Generatr.Builders;

namespace Generatr.UnitTests.Builders;

[TestClass]
public class InterfaceBuilderTests
{
    [TestMethod]
    public void Interface_WithPropertyAndMethod_EmitsSignatures()
    {
        var i = NewInterface();
        i.DefineProperty<string>("Name");
        i.DefineMethod<int>("Add").WithParameter<int>("x").WithParameter<int>("y");

        i.ToString().Should().Be(string.Join("\n",
            "namespace TestNamespace;",
            "public interface IThing",
            "{",
            "    string Name { get; set; }",
            "",
            "    int Add(int x, int y);",
            "}"));
    }

    [TestMethod]
    public void VoidMethodSignature_HasNoBody()
    {
        var i = NewInterface();
        i.DefineMethod("DoThing");

        i.ToString().Should().Contain("void DoThing();").And.NotContain("{ }");
    }

    [TestMethod]
    public void GetOnlyProperty_DropsSetter()
    {
        var i = NewInterface();
        i.DefineProperty<int>("Count").GetOnly();

        i.ToString().Should().Contain("int Count { get; }");
    }

    [TestMethod]
    public void Property_WithNeitherAccessor_Throws()
    {
        var i = NewInterface();
        var pb = i.DefineProperty<int>("Count");
        pb.HasGet = false;
        pb.HasSet = false;

        var act = () => i.ToString();

        act.Should().Throw<InvalidOperationException>().WithMessage("*must have a getter or a setter*");
    }

    [TestMethod]
    public void WithAccessModifier_And_Attribute_Emit()
    {
        var i = NewInterface()
            .WithAccessModifier(AccessModifier.Internal)
            .WithAttribute("Obsolete");
        i.DefineMethod("DoThing");

        i.ToString().Should().Be(string.Join("\n",
            "namespace TestNamespace;",
            "[Obsolete]",
            "internal interface IThing",
            "{",
            "    void DoThing();",
            "}"));
    }

    [TestMethod]
    public void MethodSignature_ToString_EmitsStandalone()
    {
        var mb = NewInterface().DefineMethod<string>("Describe").WithParameter<int>("id");

        mb.ToString().Should().Be("string Describe(int id);");
    }

    [TestMethod]
    public void EmptyInterface_EmitsBraces()
    {
        NewInterface().ToString().Should().Be(string.Join("\n",
            "namespace TestNamespace;",
            "public interface IThing",
            "{",
            "}"));
    }

    private static InterfaceBuilder NewInterface()
        => NamespaceBuilder.Get("TestNamespace").Interface("IThing");
}
