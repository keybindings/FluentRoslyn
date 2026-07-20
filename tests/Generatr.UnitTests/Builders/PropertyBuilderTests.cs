using Generatr.Builders;

namespace Generatr.UnitTests.Builders;

[TestClass]
public class PropertyBuilderTests
{
    [TestMethod]
    public void DefineProperty_DefaultsToPublicGetSetAutoProperty()
    {
        var pb = NewClassBuilder().DefineProperty<int>("Count");

        pb.ToString().Should().Be("public int Count { get; set; }");
    }

    [TestMethod]
    public void Static_EmitsStaticKeyword()
    {
        var pb = NewClassBuilder().DefineProperty<string>("Name").Static();

        pb.ToString().Should().Be("public static string Name { get; set; }");
    }

    [TestMethod]
    public void WithAccessModifier_OverridesTheAccessModifier()
    {
        var pb = NewClassBuilder().DefineProperty<int>("Count").WithAccessModifier(AccessModifier.Private);

        pb.ToString().Should().Be("private int Count { get; set; }");
    }

    [TestMethod]
    public void GetOnly_DropsTheSetter()
    {
        var pb = NewClassBuilder().DefineProperty<int>("Count").GetOnly();

        pb.ToString().Should().Be("public int Count { get; }");
    }

    [TestMethod]
    public void FluentMethods_Chain_AndPreserveGenericType()
    {
        var pb = NewClassBuilder()
            .DefineProperty<string>("Name")
            .WithAccessModifier(AccessModifier.Internal)
            .Static()
            .GetOnly();

        pb.Should().BeOfType<PropertyBuilder<string>>();
        pb.ToString().Should().Be("internal static string Name { get; }");
    }

    [TestMethod]
    public void FluentMethods_MutateInPlace_ReturningTheSameInstance()
    {
        var pb = NewClassBuilder().DefineProperty<int>("Count");

        pb.Static().Should().BeSameAs(pb);
    }

    private static ClassBuilder NewClassBuilder()
        => NamespaceBuilder.Get("Test").Class("Test1");
}
