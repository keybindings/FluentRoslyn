using FluentRoslyn.Builders;

namespace FluentRoslyn.UnitTests.Builders;

[TestClass]
public class RecordInheritanceTests
{
    [TestMethod]
    public void WithParent_ForwardsArgumentsToBasePrimaryConstructor()
    {
        var derived = NamespaceBuilder.Get("N").Record("Derived")
            .WithParameter<int>("Id")
            .WithParameter<string>("Name")
            .WithParent(NewBase(), "Id");

        derived.ToString().Should().Contain("public record Derived(int Id, string Name) : N.Base(Id);");
    }

    [TestMethod]
    public void WithParent_NoArguments_EmitsEmptyArgumentList()
    {
        var derived = NamespaceBuilder.Get("N").Record("Derived").WithParent(NewBase());

        derived.ToString().Should().Contain("public record Derived() : N.Base();");
    }

    [TestMethod]
    public void WithParent_BaseComesBeforeInterfaces()
    {
        var derived = NamespaceBuilder.Get("N").Record("Derived")
            .WithParameter<int>("Id")
            .WithInterface("IThing")
            .WithParent(NewBase(), "Id");

        // C# requires the base type first regardless of the order they were added.
        derived.ToString().Should().Contain(": N.Base(Id), IThing;");
    }

    [TestMethod]
    public void WithParent_RawTypeName_IsAccepted()
    {
        var derived = NamespaceBuilder.Get("N").Record("Derived")
            .WithParameter<int>("Id")
            .WithParent("Other.Base", "Id");

        derived.ToString().Should().Contain(": Other.Base(Id);");
    }

    [TestMethod]
    public void WithParent_ComposesWithSimplifyTypeNames()
    {
        var derived = NamespaceBuilder.Get("Other").Record("Derived")
            .SimplifyTypeNames()
            .WithParameter<int>("Id")
            .WithParent(NewBase(), "Id");

        derived.ToString().Should().Contain("using N;").And.Contain(": Base(Id);");
    }

    [TestMethod]
    public void WithParent_ExpressionArguments_AreParsed()
    {
        var derived = NamespaceBuilder.Get("N").Record("Derived")
            .WithParameter<int>("Id")
            .WithParent(NewBase(), "Id * 2");

        derived.ToString().Should().Contain(": N.Base(Id * 2);");
    }

    [TestMethod]
    public void RecordStruct_WithParent_Throws()
    {
        // A record struct has no base type to inherit from.
        var derived = NamespaceBuilder.Get("N").Record("Derived").AsStruct().WithParent(NewBase(), "Id");

        var act = () => derived.ToString();

        act.Should().Throw<InvalidOperationException>().WithMessage("*cannot inherit*");
    }

    [TestMethod]
    public void WithParent_NullParent_Throws()
    {
        var act = () => NamespaceBuilder.Get("N").Record("Derived").WithParent((RecordBuilder)null!);

        act.Should().Throw<ArgumentNullException>();
    }

    private static RecordBuilder NewBase()
        => NamespaceBuilder.Get("N").Record("Base").WithParameter<int>("Id");
}
