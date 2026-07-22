using Generatr.Builders;

namespace Generatr.UnitTests.Builders;

[TestClass]
public class EnumBuilderTests
{
    [TestMethod]
    public void Enum_WithMembers_EmitsFileScopedByDefault()
    {
        var e = NamespaceBuilder.Get("TestNamespace").Enum("Color")
            .AddMember("Red")
            .AddMember("Green")
            .AddMember("Blue");

        e.ToString().Should().Be(string.Join("\n",
            "namespace TestNamespace;",
            "public enum Color",
            "{",
            "    Red,",
            "    Green,",
            "    Blue",
            "}"));
    }

    [TestMethod]
    public void Enum_WithExplicitValues_EmitsSuffixFreeLiterals()
    {
        var e = NamespaceBuilder.Get("TestNamespace").Enum("Level")
            .AddMember("Low", 0)
            .AddMember("High", 100);

        e.ToString().Should().Contain("Low = 0,").And.Contain("High = 100");
        e.ToString().Should().NotContain("0L").And.NotContain("100L");
    }

    [TestMethod]
    public void WithUnderlyingType_NonIntegral_Throws()
    {
        var act = () => NamespaceBuilder.Get("N").Enum("E").WithUnderlyingType<string>();

        act.Should().Throw<ArgumentException>().WithMessage("*integral type*");
    }

    [TestMethod]
    public void MemberValue_OutOfRangeForUnderlyingType_Throws()
    {
        var e = NamespaceBuilder.Get("N").Enum("E").WithUnderlyingType<byte>().AddMember("Big", 300);

        var act = () => e.ToString();

        act.Should().Throw<InvalidOperationException>().WithMessage("*out of range*");
    }

    [TestMethod]
    public void MemberValue_OutOfRangeForUnderlyingType_OrderIndependent()
    {
        // Underlying type set AFTER the member — validation must still catch it.
        var e = NamespaceBuilder.Get("N").Enum("E").AddMember("Big", 300).WithUnderlyingType<byte>();

        var act = () => e.ToString();

        act.Should().Throw<InvalidOperationException>();
    }

    [TestMethod]
    public void MemberValue_InRangeForUnderlyingType_DoesNotThrow()
    {
        var e = NamespaceBuilder.Get("N").Enum("E").WithUnderlyingType<byte>().AddMember("Ok", 200);

        e.ToString().Should().Contain("Ok = 200");
    }

    [TestMethod]
    public void DuplicateMemberNames_Throws()
    {
        var e = NamespaceBuilder.Get("N").Enum("E").AddMember("A").AddMember("A");

        var act = () => e.ToString();

        act.Should().Throw<InvalidOperationException>().WithMessage("*duplicate member*");
    }

    [TestMethod]
    public void ValueTooLargeForDefaultIntUnderlying_Throws()
    {
        var e = NamespaceBuilder.Get("N").Enum("E").AddMember("Huge", 5_000_000_000L);

        var act = () => e.ToString();

        act.Should().Throw<InvalidOperationException>().WithMessage("*out of range*");
    }

    [TestMethod]
    public void Enum_WithUnderlyingType_EmitsBaseList()
    {
        var e = NamespaceBuilder.Get("TestNamespace").Enum("Small")
            .WithUnderlyingType<byte>()
            .AddMember("A");

        e.ToString().Should().Contain("public enum Small : byte");
    }

    [TestMethod]
    public void Enum_WithAttributeAndAccessModifier_Emit()
    {
        var e = NamespaceBuilder.Get("TestNamespace").Enum("Perm")
            .WithAccessModifier(AccessModifier.Internal)
            .WithAttribute("Flags")
            .AddMember("None", 0)
            .AddMember("Read", 1);

        e.ToString().Should().Be(string.Join("\n",
            "namespace TestNamespace;",
            "[Flags]",
            "internal enum Perm",
            "{",
            "    None = 0,",
            "    Read = 1",
            "}"));
    }

    [TestMethod]
    public void Enum_BlockScopedNamespace_WrapsInBraces()
    {
        var e = NamespaceBuilder.Get("TestNamespace").Enum("Color")
            .BlockScopedNamespace()
            .AddMember("Red");

        e.ToString().Should().Be(string.Join("\n",
            "namespace TestNamespace",
            "{",
            "    public enum Color",
            "    {",
            "        Red",
            "    }",
            "}"));
    }

    [TestMethod]
    public void Enum_GlobalNamespace_EmitsWithoutNamespace()
    {
        var e = NamespaceBuilder.None.Enum("Color").AddMember("Red");

        e.ToString().Should().Be(string.Join("\n",
            "public enum Color",
            "{",
            "    Red",
            "}"));
    }
}
