using FluentAssertions;
using Generatr.Builders;

namespace Generatr.UnitTests;

[TestClass]
public class NamespaceBuilderTests
{
    private const string TestNamespace = "TestNamespace";
    private const string TestNamespace1 = "TestNamespace1";
    private const string TestNamespace2 = "TestNamespace2";
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
    [ExpectedException(typeof(ArgumentException))]
    public void WhenNewNamespaceBuilderCalled_InvalidNamespace_ArgumentExceptionThrown()
    {
        const string namespaceWithSpaces = "Test Namespace With Spaces";
        NamespaceBuilder.New(namespaceWithSpaces);
    }

    [TestMethod]
    public void WhenChildNamespaceBuilt_NamespaceStringBuiltCorrectly()
    {
        var parentNamespace = NamespaceBuilder.New(TestNamespace1);
        var childNamespace = parentNamespace.Child(TestNamespace2);
        childNamespace.ToString().Should().Be($"{TestNamespace1}.{TestNamespace2}");
    }


}