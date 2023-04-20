using FluentAssertions;
using Generatr.Builders;

namespace Generatr.UnitTests.Builders;

[TestClass]
public class NamedBuilderTests
{
    private class NamedBuilderStub : NamedBuilder
    {
        public NamedBuilderStub(string name) : base(name)
        {
        }

        public int BuildInvokedCount { get; private set; }
        protected override string Build()
        {
            BuildInvokedCount++;
            return string.Empty;
        }
    }

    // ReSharper disable once ObjectCreationAsStatement
    private readonly Action<string> _newBuilderAct = s => new NamedBuilderStub(s);

    [TestMethod]
    public void NewNameBuilderCalled_NullName_ArgumentNullExceptionThrown()
    {
        _newBuilderAct.Invoking(x => x(null)).Should().Throw<ArgumentNullException>();
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("    ")]
    [DataRow("Test Name With Spaces")]
    [DataRow("1InvalidName")]
    public void NewNameBuilderCalled_InvalidName_ArgumentOutOfRangeExceptionThrown(string name)
    {
        _newBuilderAct.Invoking(x => x(name))
            .Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage($"Name: \"{name}\" contains invalid chars.*");
    }

    [DataTestMethod]
    [DataRow("ValidName1")]
    [DataRow("Another_ValidName")]
    [DataRow("_1AnotherValidName")]
    public void NewNameBuilderCalled_ValidName_NamePropertySet(string name)
    {
        var builder = new NamedBuilderStub(name);
        builder.Name.Should().Be(name);
    }

    [TestMethod]
    public void NewBuilderCreated_ToStringCalled_BuildMethodCalledOnce()
    {
        var builder = new NamedBuilderStub("TestName");
        _ = builder.ToString();
        builder.BuildInvokedCount.Should().Be(1);
    }
}