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

    private readonly Action<string> _newAct = x => NamespaceBuilder.New(x);

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
    public void WhenNewNamespaceBuilderCalled_InvalidNamespace_ArgumentExceptionThrown()
    {
        _newAct.Invoking(x => x("Test Namespace With Spaces"))
            .Should().Throw<ArgumentException>().WithMessage("Name cannot contain invalid chars: *");
    }

    [TestMethod]
    public void WhenNullNameUsed_ArgumentNullExceptionThrown()
    {
        _newAct.Invoking(x => x(null)).Should().Throw<ArgumentNullException>();
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

    [TestMethod]
    public void WhenEmptyStringBetweenPointsArgumentExceptionThrown()
    {
        const string invalidEmptyNamespace = "";
        _newAct.Invoking(x => x(invalidEmptyNamespace)).Should().Throw<ArgumentNullException>();
    }
    
    [TestMethod]
    public void WhenWhitespaceStringBetweenPointsArgumentExceptionThrown()
    {
        const string invalidEmptyNamespace = "   ";
        _newAct.Invoking(x => x(invalidEmptyNamespace)).Should().Throw<ArgumentNullException>();
    }
}