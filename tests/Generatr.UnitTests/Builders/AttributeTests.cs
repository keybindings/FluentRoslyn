using Generatr.Builders;

namespace Generatr.UnitTests.Builders;

[TestClass]
public class AttributeTests
{
    [TestMethod]
    public void ClassAttribute_EmitsAboveDeclaration()
    {
        var cb = NewClass().WithAttribute("Serializable");

        cb.ToString().Should().Be(string.Join("\n",
            "namespace TestNamespace;",
            "[Serializable]",
            "public class TestClass",
            "{",
            "}"));
    }

    [TestMethod]
    public void MultipleAttributes_EmitOnePerLineInOrder()
    {
        var cb = NewClass()
            .WithAttribute("Serializable")
            .WithAttribute("Obsolete(\"gone\")");

        cb.ToString().Should().Contain(string.Join("\n",
            "[Serializable]",
            "[Obsolete(\"gone\")]",
            "public class TestClass"));
    }

    [TestMethod]
    public void FieldAttribute_WithArgument_EmitsAboveField()
    {
        var fb = NewClass().DefineField<int>("_count").WithAttribute("JsonProperty(\"count\")");

        fb.ToString().Should().Be(string.Join("\n",
            "[JsonProperty(\"count\")]",
            "private int _count;"));
    }

    [TestMethod]
    public void PropertyAttribute_EmitsAboveProperty()
    {
        var pb = NewClass().DefineProperty<int>("Count").WithAttribute("JsonIgnore");

        pb.ToString().Should().Be(string.Join("\n",
            "[JsonIgnore]",
            "public int Count { get; set; }"));
    }

    [TestMethod]
    public void PropertyAttribute_AppliesToExpressionBodiedProperty()
    {
        var pb = NewClass().DefineProperty<int>("Count")
            .WithAttribute("JsonIgnore")
            .AsExpressionBody("_count");

        pb.ToString().Should().Be(string.Join("\n",
            "[JsonIgnore]",
            "public int Count => _count;"));
    }

    [TestMethod]
    public void MethodAttribute_EmitsAboveMethod()
    {
        var mb = NewClass().DefineMethod("DoThing").WithAttribute("Obsolete");

        mb.ToString().Should().Be(string.Join("\n",
            "[Obsolete]",
            "public void DoThing()",
            "{",
            "}"));
    }

    [TestMethod]
    public void Attribute_BracketsAreOptional()
    {
        var withBrackets = NewClass().DefineMethod("A").WithAttribute("[Obsolete]");
        var withoutBrackets = NewClass().DefineMethod("A").WithAttribute("Obsolete");

        withBrackets.ToString().Should().Be(withoutBrackets.ToString());
    }

    [TestMethod]
    public void WithAttribute_Unparseable_Throws()
    {
        var cb = NewClass();

        var act = () => cb.WithAttribute("not a valid attribute!!");

        act.Should().Throw<System.ArgumentException>();
    }

    private static ClassBuilder NewClass(string name = "TestClass")
        => NamespaceBuilder.Get("TestNamespace").Class(name);
}
