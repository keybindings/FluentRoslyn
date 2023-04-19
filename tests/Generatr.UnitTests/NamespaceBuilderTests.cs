using System.Xml.Serialization;
using FluentAssertions;
using Generatr.Builders;

namespace Generatr.UnitTests;

[TestClass]
public class BuilderTests
{
    private class NamedBuilderStub : NamedBuilder
    {
        public NamedBuilderStub(string name) : base(name)
        {
        }

        public int BuildInvokedCount { get; set; }
        protected override string Build()
        {
            BuildInvokedCount++;
            return string.Empty;
        }
    }

    // ReSharper disable once ObjectCreationAsStatement
    private readonly Action<string> _newBuilderAct = s => new NamedBuilderStub(s);

    [TestMethod]
    public void WhenNewNameBuilderCalledWithNullNameThenArgumentNullExceptionThrown()
    {
        _newBuilderAct.Invoking(x => x(null)).Should().Throw<ArgumentNullException>();
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("    ")]
    [DataRow("Test Name With Spaces")]
    [DataRow("1InvalidName")]
    public void WhenNewNameBuilderCalledWithInvalidNameThenArgumentOutOfRangeExceptionThrown(string name)
    {
        _newBuilderAct.Invoking(x => x(name))
            .Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage($"Name: \"{name}\" contains invalid chars.*");
    }

    [DataTestMethod]
    [DataRow("ValidName1")]
    [DataRow("Another_ValidName")]
    [DataRow("_1AnotherValidName")]
    public void WhenNewNameBuilderCalledWithValidNameThenNamePropertySet(string name)
    {
        var builder = new NamedBuilderStub(name);
        builder.Name.Should().Be(name);
    }
}

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