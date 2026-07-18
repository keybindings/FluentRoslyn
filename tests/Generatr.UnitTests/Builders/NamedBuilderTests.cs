using AutoFixture;
using Generatr.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

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

        internal override SyntaxNode BuildSyntax()
        {
            BuildInvokedCount++;
            return SyntaxFactory.IdentifierName(Name);
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

        internal override SyntaxNode BuildSyntax() => SyntaxFactory.IdentifierName(Name);
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

    [TestMethod]
    public void NewBuilderCreated_ToStringCalled_BuildSyntaxCalledOnce()
    {
        var builder = new BuilderStub("TestName");
        var value = builder.ToString();
        builder.BuildInvokedCount.Should().Be(1);
        value.Should().Be("TestName");
    }

    [TestMethod]
    public void NewBuilderCreated_BuildSyntaxCalled_BuildSyntaxCalledOnce()
    {
        var builder = new BuilderStub("TestName");
        _ = builder.BuildSyntax();
        builder.BuildInvokedCount.Should().Be(1);
    }


}
