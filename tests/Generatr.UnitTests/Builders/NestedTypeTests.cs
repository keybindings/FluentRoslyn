using Generatr.Builders;

namespace Generatr.UnitTests.Builders;

[TestClass]
public class NestedTypeTests
{
    [TestMethod]
    public void DefineClass_EmitsNestedInsideOuter()
    {
        var outer = NewOuter();
        outer.DefineClass("Inner").DefineProperty<int>("Value");

        outer.ToString().Should().Be(string.Join("\n",
            "namespace TestNamespace;",
            "public class Outer",
            "{",
            "    public class Inner",
            "    {",
            "        public int Value { get; set; }",
            "    }",
            "}"));
    }

    [DataTestMethod]
    [DataRow("struct")]
    [DataRow("enum")]
    [DataRow("record")]
    [DataRow("interface")]
    public void EveryTypeKind_CanBeNested(string kind)
    {
        var outer = NewOuter();
        switch (kind)
        {
            case "struct": outer.DefineStruct("Nested"); break;
            case "enum": outer.DefineEnum("Nested").AddMember("A"); break;
            case "record": outer.DefineRecord("Nested").WithParameter<int>("X"); break;
            case "interface": outer.DefineInterface("Nested"); break;
        }

        outer.ToString().Should().Contain($"public {kind} Nested");
    }

    [TestMethod]
    public void NestedType_QualifiedNameIncludesDeclaringType()
    {
        // The whole point: a nested type is Ns.Outer.Inner, not Ns.Inner.
        var inner = NewOuter().DefineClass("Inner");
        var consumer = NamespaceBuilder.Get("TestNamespace").Class("Consumer").WithParent(inner);

        consumer.ToString().Should().Contain(": TestNamespace.Outer.Inner");
    }

    [TestMethod]
    public void DeeplyNestedType_QualifiedNameChainsAllLevels()
    {
        var deepest = NewOuter().DefineClass("Inner").DefineClass("Deepest");
        var consumer = NamespaceBuilder.Get("TestNamespace").Class("Consumer").WithParent(deepest);

        consumer.ToString().Should().Contain(": TestNamespace.Outer.Inner.Deepest");
    }

    [TestMethod]
    public void NestedType_EmittedStandalone_HasNoNamespaceWrapper()
    {
        var inner = NewOuter().DefineClass("Inner");

        // A nested type is not a file, so it emits as a bare declaration.
        inner.ToString().Should().Be(string.Join("\n",
            "public class Inner",
            "{",
            "}"));
    }

    [TestMethod]
    public void NestedTypes_EmitAfterOtherMembers()
    {
        var outer = NewOuter();
        outer.DefineClass("Inner");
        outer.DefineMethod("Run");
        outer.DefineField<int>("_x");

        var value = outer.ToString();
        var fieldIndex = value.IndexOf("_x", StringComparison.Ordinal);
        var methodIndex = value.IndexOf("Run", StringComparison.Ordinal);
        var nestedIndex = value.IndexOf("class Inner", StringComparison.Ordinal);

        fieldIndex.Should().BeLessThan(methodIndex);
        methodIndex.Should().BeLessThan(nestedIndex);
    }

    [TestMethod]
    public void NestedTypes_SortByAccessibilityThenName()
    {
        var outer = NewOuter();
        outer.DefineClass("ZPublic");
        outer.DefineClass("APrivate").WithAccessModifier(AccessModifier.Private);
        outer.DefineClass("MInternal").WithAccessModifier(AccessModifier.Internal);

        var value = outer.ToString();
        value.IndexOf("ZPublic", StringComparison.Ordinal)
            .Should().BeLessThan(value.IndexOf("MInternal", StringComparison.Ordinal));
        value.IndexOf("MInternal", StringComparison.Ordinal)
            .Should().BeLessThan(value.IndexOf("APrivate", StringComparison.Ordinal));
    }

    [TestMethod]
    public void NestedType_SupportsSummaryAndAttributes()
    {
        var outer = NewOuter();
        outer.DefineClass("Inner").WithSummary("A nested class.").WithAttribute("Serializable");

        outer.ToString().Should().Contain("/// A nested class.").And.Contain("[Serializable]");
    }

    [TestMethod]
    public void NestedType_CanNestInsideStruct()
    {
        var s = NamespaceBuilder.Get("TestNamespace").Struct("Outer");
        s.DefineEnum("Kind").AddMember("A");

        s.ToString().Should().Contain("public enum Kind");
    }

    [TestMethod]
    public void NestedType_IsNestedFlagSet()
    {
        var outer = NewOuter();
        var inner = outer.DefineClass("Inner");

        outer.IsNested.Should().BeFalse();
        inner.IsNested.Should().BeTrue();
        inner.DeclaringType.Should().BeSameAs(outer);
    }

    [TestMethod]
    public void NestedType_InheritsOuterNamespace()
    {
        var outer = NewOuter();
        var inner = outer.DefineClass("Inner");

        inner.Namespace.Should().BeSameAs(outer.Namespace);
        inner.Namespace.ToString().Should().Be("TestNamespace");
    }

    private static ClassBuilder NewOuter()
        => NamespaceBuilder.Get("TestNamespace").Class("Outer");
}
