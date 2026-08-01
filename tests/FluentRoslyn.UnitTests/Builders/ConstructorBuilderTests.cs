using FluentRoslyn.Builders;

namespace FluentRoslyn.UnitTests.Builders;

[TestClass]
public class ConstructorBuilderTests
{
    [TestMethod]
    public void DefineConstructor_Parameterless_EmitsEmptyBody()
    {
        var ctor = NewClass().DefineConstructor();

        ctor.ToString().Should().Be(string.Join("\n",
            "public Widget()",
            "{",
            "}"));
    }

    [TestMethod]
    public void Constructor_WithParametersAndBody_Emits()
    {
        var ctor = NewClass()
            .DefineConstructor(AccessModifier.Public).WithParameter<int>("id")
            .AddStatement("_id = id;");

        ctor.ToString().Should().Be(string.Join("\n",
            "public Widget(int id)",
            "{",
            "    _id = id;",
            "}"));
    }

    [TestMethod]
    public void WithParameter_AppendsParameters()
    {
        var ctor = NewClass().DefineConstructor()
            .WithParameter<int>("id")
            .WithParameter<string>("name");

        ctor.ToString().Should().StartWith("public Widget(int id, string name)");
    }

    [TestMethod]
    public void WithAccessModifier_OverridesAccess()
    {
        var ctor = NewClass().DefineConstructor().WithAccessModifier(AccessModifier.Private);

        ctor.ToString().Should().StartWith("private Widget()");
    }

    [TestMethod]
    public void CallingBase_EmitsBaseInitializer()
    {
        var ctor = NewClass()
            .DefineConstructor(AccessModifier.Public).WithParameter<int>("id")
            .CallingBase("id");

        ctor.ToString().Should().StartWith("public Widget(int id) : base(id)");
    }

    [TestMethod]
    public void CallingThis_EmitsThisInitializer()
    {
        var ctor = NewClass().DefineConstructor().CallingThis("0", "\"default\"");

        ctor.ToString().Should().StartWith("public Widget() : this(0, \"default\")");
    }

    [TestMethod]
    public void AsExpressionBody_EmitsArrowConstructor()
    {
        var ctor = NewClass()
            .DefineConstructor(AccessModifier.Public).WithParameter<int>("id")
            .AsExpressionBody("_id = id");

        ctor.ToString().Should().Be("public Widget(int id) => _id = id;");
    }

    [TestMethod]
    public void WithAttribute_EmitsAboveConstructor()
    {
        var ctor = NewClass().DefineConstructor().WithAttribute("JsonConstructor");

        ctor.ToString().Should().Be(string.Join("\n",
            "[JsonConstructor]",
            "public Widget()",
            "{",
            "}"));
    }

    [TestMethod]
    public void Static_EmitsStaticConstructorWithoutAccessModifier()
    {
        var ctor = NewClass().DefineConstructor().Static().AddStatement("Init();");

        ctor.ToString().Should().Be(string.Join("\n",
            "static Widget()",
            "{",
            "    Init();",
            "}"));
    }

    [TestMethod]
    public void StaticConstructor_WithParameters_Throws()
    {
        var ctor = NewClass().DefineConstructor(AccessModifier.Public).WithParameter<int>("id").Static();

        var act = () => ctor.ToString();

        act.Should().Throw<InvalidOperationException>().WithMessage("*cannot have parameters*");
    }

    [TestMethod]
    public void StaticConstructor_ChainingToBase_Throws()
    {
        var ctor = NewClass().DefineConstructor().Static().CallingBase();

        var act = () => ctor.ToString();

        act.Should().Throw<InvalidOperationException>().WithMessage("*cannot chain*");
    }

    [TestMethod]
    public void ExpressionBodyAndStatements_Together_Throw()
    {
        var ctor = NewClass().DefineConstructor()
            .AsExpressionBody("_x = 1")
            .AddStatement("_y = 2;");

        var act = () => ctor.ToString();

        act.Should().Throw<InvalidOperationException>().WithMessage("*both*");
    }

    [TestMethod]
    public void Constructor_ReachesClassOutput_BetweenFieldsAndMethods()
    {
        var cb = NewClass();
        cb.DefineMethod("Run");
        cb.DefineField<int>("_id");
        cb.DefineConstructor(AccessModifier.Public).WithParameter<int>("id").AddStatement("_id = id;");

        var value = cb.ToString();

        var fieldIndex = value.IndexOf("_id;", StringComparison.Ordinal);
        var ctorIndex = value.IndexOf("public Widget(", StringComparison.Ordinal);
        var methodIndex = value.IndexOf("Run", StringComparison.Ordinal);

        fieldIndex.Should().BePositive().And.BeLessThan(ctorIndex);
        ctorIndex.Should().BeLessThan(methodIndex);
    }

    private static ClassBuilder NewClass()
        => NamespaceBuilder.Get("TestNamespace").Class("Widget");
}
