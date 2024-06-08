using Generatr.Builders;

namespace Generatr.UnitTests.Builders;

[TestClass]
public class NamespaceBuilderTests
{
    private const string TestNamespace = "TestNamespace";
    private const string TestNamespace1 = "TestNamespace1";
    private const string TestNamespace2 = "TestNamespace2";

    [TestMethod]
    public void NewNamespaceBuilderCalled_NullNameUsed_ArgumentNullExceptionThrown()
    {
        Action newAct = () => NamespaceBuilder.Get(null!);
        newAct.Invoking(x => x()).Should().Throw<ArgumentNullException>();
    }
    [TestMethod]
    public void WhenNewNamespaceBuilderCalled_BaseNamespaceValidString_NoParentName()
    {
        var expected = NamespaceBuilder.Get(TestNamespace);
        expected.Parent.Should().Be(NamespaceBuilder.None);
    }
    
    [TestMethod]
    public void WhenNewNamespaceBuilderCalled_BaseNamespaceValidString_CorrectParentName()
    {
        var expected = NamespaceBuilder.Get($"{TestNamespace}.{TestNamespace1}");
        expected.Parent.ToString().Should().Be(TestNamespace);
    }

    [TestMethod]
    public void WhenNewNamespaceBuilderCalled_BaseNamespaceValidString_NameSetCorrectly()
    {
        var expected = NamespaceBuilder.Get(TestNamespace);
        expected.ToString().Should().Be(TestNamespace);
    }

    [TestMethod]
    public void WhenChildNamespaceBuilt_NamespaceStringBuiltCorrectly()
    {
        var parentNamespace = NamespaceBuilder.Get(TestNamespace1);
        var childNamespace = parentNamespace.Child(TestNamespace2);
        childNamespace.ToString().Should().Be($"{TestNamespace1}.{TestNamespace2}");
    }

    [TestMethod]
    public void WhenMultiNamespaceBuild_NamespaceShouldBuildHierarchy()
    {
        const string multiNamespace = $"{TestNamespace1}.{TestNamespace2}.TestNamespace3";
        var nsBuilder = NamespaceBuilder.Get(multiNamespace);
        nsBuilder.ToString().Should().Be(multiNamespace);
    }

    [TestMethod]
    public void WhenMultiNamespaceBuild_ShouldBeAbleToNavigateHierarchy()
    {
        const string multiNamespace = $"{TestNamespace1}.{TestNamespace2}.TestNamespace3";
        var nsBuilder = NamespaceBuilder.Get(multiNamespace);
        nsBuilder.ToString().Should().Be(multiNamespace);
    }


    [TestMethod]
    public void NewBuilderCreated_ClassCreated_ClassNamespaceMatches()
    {
        var namespaceBuilder = NamespaceBuilder.Get(TestNamespace);
        var classBuilder = namespaceBuilder.Class("TestClassName");
        classBuilder.Namespace.Should().Be(namespaceBuilder);
    }
}