using FluentRoslyn.Builders;

namespace FluentRoslyn.UnitTests.Builders;

[TestClass]
public class RequiredMemberTests
{
    [TestMethod]
    public void RequiredProperty_EmitsRequiredKeyword()
    {
        var pb = NewClass().DefineProperty<string>("Name").Required();

        pb.ToString().Should().Be("public required string Name { get; set; }");
    }

    [TestMethod]
    public void RequiredInitProperty_Composes()
    {
        var pb = NewClass().DefineProperty<string>("Name").Required().InitOnly();

        pb.ToString().Should().Be("public required string Name { get; init; }");
    }

    [TestMethod]
    public void RequiredField_EmitsRequiredKeyword()
    {
        var fb = NewClass().DefineField<string>("Name", AccessModifier.Public).Required();

        fb.ToString().Should().Be("public required string Name;");
    }

    [TestMethod]
    public void RequiredStaticProperty_Throws()
    {
        var pb = NewClass().DefineProperty<int>("Count").Required().Static();

        var act = () => pb.ToString();

        act.Should().Throw<InvalidOperationException>().WithMessage("*static property cannot be required*");
    }

    [TestMethod]
    public void RequiredGetOnlyProperty_Throws()
    {
        // Nothing could ever satisfy the requirement without a settable accessor.
        var pb = NewClass().DefineProperty<int>("Count").Required().GetOnly();

        var act = () => pb.ToString();

        act.Should().Throw<InvalidOperationException>().WithMessage("*needs a set or init accessor*");
    }

    [TestMethod]
    public void RequiredStaticField_Throws()
    {
        var fb = NewClass().DefineField<int>("Count").Required().Static();

        var act = () => fb.ToString();

        act.Should().Throw<InvalidOperationException>().WithMessage("*static or const*");
    }

    [TestMethod]
    public void RequiredConstField_Throws()
    {
        var fb = NewClass().DefineField<int>("Count").Required().Const().WithInitializer(1);

        var act = () => fb.ToString();

        act.Should().Throw<InvalidOperationException>().WithMessage("*static or const*");
    }

    [TestMethod]
    public void RequiredMembers_ReachClassOutput()
    {
        var cb = NewClass();
        cb.DefineProperty<string>("Name").Required().InitOnly();

        cb.ToString().Should().Contain("public required string Name { get; init; }");
    }

    private static ClassBuilder NewClass()
        => NamespaceBuilder.Get("TestNamespace").Class("Dto");
}
