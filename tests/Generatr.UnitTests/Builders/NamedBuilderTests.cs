using AutoFixture;
using Generatr.Abstractions;
using Generatr.Builders;

namespace Generatr.UnitTests.Builders;

[TestClass]
public class NamedBuilderTests
{
    private class BuilderStub : NamedBuilder
    {
        public BuilderStub(string name) : base(name, NameValidation)
        {
        }

        public int BuildInvokedCount { get; private set; }
        public override void Build(TabbedBuilder tb)
        {
            BuildInvokedCount++;
        }

        private static void NameValidation(string name)
        {

        }
    }

    private class OpenBuilder : NamedBuilder
    {
        public OpenBuilder(string name, Action<string> validNameCheck) : base(name, validNameCheck)
        {
        }
    }

    // ReSharper disable once ObjectCreationAsStatement
    private readonly Action<string> _newBuilderAct = s => new BuilderStub(s);

    [TestMethod]
    public void NewNameBuilderCalled_NullName_ArgumentNullExceptionThrown()
    {
        _newBuilderAct.Invoking(x => x(null)).Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void BuildThatShouldAlwaysThrow_AnyName_ExceptionThrown()
    {
        var fixture = new Fixture();
        var name = fixture.Create<string>();
        var act = () => new OpenBuilder(name, s => throw new Exception($"Throwing error with name {s}."));

        act.Should().Throw<Exception>();
    }

    //TODO: These should be removed, name validation will be done at the builder level
    //[DataTestMethod]
    //[DataRow("")]
    //[DataRow("    ")]
    //[DataRow("Test Name With Spaces")]
    //[DataRow("1InvalidName")]
    //[DataRow("'InvalidName")]
    //public void NewNameBuilderCalled_InvalidName_ArgumentOutOfRangeExceptionThrown(string name)
    //{
    //    _newBuilderAct.Invoking(x => x(name))
    //        .Should().Throw<ArgumentOutOfRangeException>()
    //        .WithMessage($"Name: \"{name}\" contains invalid chars.*");
    //}

    //[DataTestMethod]
    //[DataRow("@nametestbtw")]
    //[DataRow("ValidName1")]
    //[DataRow("Another_ValidN1ame")]
    //[DataRow("_1AnotherValidName")]
    //public void NewNameBuilderCalled_ValidName_NamePropertySet(string name)
    //{
    //    var builder = new BuilderStub(name);
    //    builder.Name.Should().Be(name);
    //}

    [TestMethod]
    public void NewBuilderCreated_ToStringCalled_BuildMethodCalledOnce()
    {
        var builder = new BuilderStub("TestName");
        _ = builder.ToString();
        builder.BuildInvokedCount.Should().Be(1);
    }

    [TestMethod]
    public void NewBuilderCreated_BuildCalled_BuildMethodCalledOnce()
    {
        var builder = new BuilderStub("TestName");
        var tb = new TabbedBuilder();
        builder.Build(tb);
        builder.BuildInvokedCount.Should().Be(1);
    }


}