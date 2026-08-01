using System;
using FluentRoslyn.Builders;

namespace FluentRoslyn.UnitTests.Builders;

[TestClass]
public class ParseValidationTests
{
    private static ClassBuilder NewClass() => NamespaceBuilder.Get("N").Class("C");

    [TestMethod]
    public void MalformedStatement_Throws()
    {
        // Parsing happens eagerly at the fluent call, so it fails fast.
        var act = () => NewClass().DefineMethod("M").AddStatement("return");

        act.Should().Throw<ArgumentException>().WithMessage("*not a valid C# statement*");
    }

    [TestMethod]
    public void MalformedInitializerExpression_Throws()
    {
        var act = () => NewClass().DefineField<int>("_x").WithInitializerExpression("1 +");

        act.Should().Throw<ArgumentException>().WithMessage("*not a valid C# expression*");
    }

    [TestMethod]
    public void MalformedReturnType_Throws()
    {
        var act = () => NewClass().DefineMethod("M").Returns("List<");

        act.Should().Throw<ArgumentException>().WithMessage("*not a valid C# type name*");
    }

    [TestMethod]
    public void MalformedInterface_Throws()
    {
        var act = () => NewClass().WithInterface("IThing<");

        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void MalformedAttribute_Throws()
    {
        var act = () => NewClass().WithAttribute("Obsolete(\"msg\"");

        act.Should().Throw<ArgumentException>().WithMessage("*not a valid C# attribute*");
    }

    [TestMethod]
    public void MalformedConstructorBaseArgument_Throws()
    {
        var act = () => NewClass().DefineConstructor().CallingBase("a b");

        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void ValidRawFragments_StillWork()
    {
        var mb = NewClass().DefineMethod<int>("Add").WithParameter<int>("a").AsExpressionBody("a + 1");

        mb.ToString().Should().Be("public int Add(int a) => a + 1;");
    }
}
