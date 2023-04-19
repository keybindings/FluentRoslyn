using FluentAssertions;
using Generatr.Builders;

namespace Generatr.UnitTests;

[TestClass]
public class NamespaceBuilderTests
{
    private const string TestNamespace = "TestNamespace";
    private const string TestNamespace1 = "TestNamespace1";
    private const string TestNamespace2 = "TestNamespace2";
    private const string TestNamespace3 = "TestNamespace3";

    [TestMethod]
    public void NewNamespaceBuilderCalled_NullNameUsed_ArgumentNullExceptionThrown()
    {
        Action newAct = () => NamespaceBuilder.New(null);
        newAct.Invoking(x => x()).Should().Throw<ArgumentNullException>();
    }
    [TestMethod]
    public void WhenNewNamespaceBuilderCalled_BaseNamespaceValidString_NoParentName()
    {
        var expected = NamespaceBuilder.New(TestNamespace);
        expected.Parent.Should().BeNull();
    }

    [TestMethod]
    public void WhenNewNamespaceBuilderCalled_BaseNamespaceValidString_NameSetCorrectly()
    {
        var expected = NamespaceBuilder.New(TestNamespace);
        expected.ToString().Should().Be(TestNamespace);
    }

    [TestMethod]
    public void WhenChildNamespaceBuilt_NamespaceStringBuiltCorrectly()
    {
        var parentNamespace = NamespaceBuilder.New(TestNamespace1);
        var childNamespace = parentNamespace.Child(TestNamespace2);
        childNamespace.ToString().Should().Be($"{TestNamespace1}.{TestNamespace2}");
    }

    [TestMethod]
    public void WhenMultiNamespaceBuild_NamespaceShouldBuildHierarchy()
    {
        const string multiNamespace = $"{TestNamespace1}.{TestNamespace2}.{TestNamespace3}";
        var nsBuilder = NamespaceBuilder.New(multiNamespace);
        nsBuilder.ToString().Should().Be(multiNamespace);
    }
}