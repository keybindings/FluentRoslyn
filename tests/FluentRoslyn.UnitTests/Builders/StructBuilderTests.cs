using FluentRoslyn.Builders;

namespace FluentRoslyn.UnitTests.Builders;

[TestClass]
public class StructBuilderTests
{
    [TestMethod]
    public void EmptyStruct_EmitsFileScopedByDefault()
    {
        var s = NewStruct();

        s.ToString().Should().Be(string.Join("\n",
            "namespace TestNamespace;",
            "public struct Point",
            "{",
            "}"));
    }

    [TestMethod]
    public void ReadOnlyPartial_EmitsBothModifiersInOrder()
    {
        var s = NewStruct().Readonly().Partial();

        s.ToString().Should().Contain("public readonly partial struct Point");
    }

    [TestMethod]
    public void WithAccessModifier_FromSharedBase_ReturnsStructBuilder()
    {
        var s = NewStruct().WithAccessModifier(AccessModifier.Internal);

        s.Should().BeOfType<StructBuilder>();
        s.ToString().Should().Contain("internal struct Point");
    }

    [TestMethod]
    public void SharedMembers_FieldsPropertiesConstructorsMethods_Emit()
    {
        var s = NewStruct();
        s.DefineField<int>("_x");
        s.DefineProperty<int>("X");
        s.DefineConstructor(AccessModifier.Public).WithParameter<int>("x").AddStatement("_x = x;");
        s.DefineMethod<int>("Get").AsExpressionBody("_x");

        var value = s.ToString();

        value.Should().Contain("private int _x;");
        value.Should().Contain("public int X { get; set; }");
        value.Should().Contain("public Point(int x)");
        value.Should().Contain("public int Get() => _x;");
    }

    [TestMethod]
    public void WithAttribute_EmitsAboveStruct()
    {
        var s = NewStruct().WithAttribute("StructLayout(LayoutKind.Sequential)");

        s.ToString().Should().Contain("[StructLayout(LayoutKind.Sequential)]")
            .And.Contain("public struct Point");
    }

    [TestMethod]
    public void MemberOrdering_MatchesClass_FieldsConstructorsPropertiesMethods()
    {
        var s = NewStruct();
        s.DefineMethod("Run");
        s.DefineProperty<int>("X");
        // Assign all fields so the emitted struct actually compiles (CS0171) rather than
        // asserting member order on invalid source.
        s.DefineConstructor().AddStatement("_x = 0;").AddStatement("X = 0;");
        s.DefineField<int>("_x");

        var value = s.ToString();
        var field = value.IndexOf("_x;", StringComparison.Ordinal);
        var ctor = value.IndexOf("public Point()", StringComparison.Ordinal);
        var prop = value.IndexOf("X {", StringComparison.Ordinal);
        var method = value.IndexOf("Run", StringComparison.Ordinal);

        field.Should().BeLessThan(ctor);
        ctor.Should().BeLessThan(prop);
        prop.Should().BeLessThan(method);
    }

    [TestMethod]
    public void BlockScopedNamespace_WrapsStruct()
    {
        var s = NewStruct().BlockScopedNamespace();

        s.ToString().Should().Be(string.Join("\n",
            "namespace TestNamespace",
            "{",
            "    public struct Point",
            "    {",
            "    }",
            "}"));
    }

    private static StructBuilder NewStruct()
        => NamespaceBuilder.Get("TestNamespace").Struct("Point");
}
