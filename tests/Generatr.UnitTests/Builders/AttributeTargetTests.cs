using Generatr.Builders;

namespace Generatr.UnitTests.Builders;

[TestClass]
public class AttributeTargetTests
{
    [TestMethod]
    public void ReturnTarget_EmitsOnMethod()
    {
        var mb = NewClass().DefineMethod<string>("Get")
            .WithAttribute("return: NotNull")
            .AsExpressionBody("\"\"");

        mb.ToString().Should().StartWith("[return: NotNull]");
    }

    [TestMethod]
    public void FieldTarget_EmitsOnProperty()
    {
        var pb = NewClass().DefineProperty<int>("Count").WithAttribute("field: NonSerialized");

        pb.ToString().Should().StartWith("[field: NonSerialized]");
    }

    [DataTestMethod]
    [DataRow("assembly")]
    [DataRow("module")]
    [DataRow("method")]
    [DataRow("param")]
    [DataRow("property")]
    [DataRow("type")]
    [DataRow("event")]
    public void EveryTarget_IsRecognised(string target)
    {
        var pb = NewClass().DefineProperty<int>("Count").WithAttribute($"{target}: Marker");

        pb.ToString().Should().StartWith($"[{target}: Marker]");
    }

    [TestMethod]
    public void BracketedForm_IsAccepted()
    {
        var pb = NewClass().DefineProperty<int>("Count").WithAttribute("[return: NotNull]");

        pb.ToString().Should().StartWith("[return: NotNull]");
    }

    [TestMethod]
    public void NamedArgument_IsNotMistakenForATarget()
    {
        // `message:` looks like a target but is a named argument.
        var pb = NewClass().DefineProperty<int>("Count").WithAttribute("Obsolete(message: \"gone\")");

        pb.ToString().Should().StartWith("[Obsolete(message: \"gone\")]");
    }

    [TestMethod]
    public void UnrecognisedTarget_ThrowsRatherThanDroppingIt()
    {
        // Roslyn parses an unknown target happily; lifting the bare attribute out would
        // silently discard it, so this is rejected instead.
        var act = () => NewClass().DefineProperty<int>("Count").WithAttribute("nonsense: Foo");

        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void PlainAttribute_StillHasNoTarget()
    {
        var pb = NewClass().DefineProperty<int>("Count").WithAttribute("JsonIgnore");

        pb.ToString().Should().StartWith("[JsonIgnore]").And.NotContain(":");
    }

    [TestMethod]
    public void TargetsWorkOnTypesToo()
    {
        var cb = NewClass().WithAttribute("type: Serializable");

        cb.ToString().Should().Contain("[type: Serializable]");
    }

    private static ClassBuilder NewClass()
        => NamespaceBuilder.Get("TestNamespace").Class("Widget");
}
