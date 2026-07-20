using Generatr.Builders;

namespace Generatr.UnitTests.Builders;

[TestClass]
public class PropertyBuilderTests
{
    [TestMethod]
    public void DefineProperty_DefaultsToPublicGetSetAutoProperty()
    {
        var pb = NewClassBuilder().DefineProperty<int>("Count");

        pb.ToString().Should().Be("public int Count { get; set; }");
    }

    [TestMethod]
    public void Static_EmitsStaticKeyword()
    {
        var pb = NewClassBuilder().DefineProperty<string>("Name").Static();

        pb.ToString().Should().Be("public static string Name { get; set; }");
    }

    [TestMethod]
    public void WithAccessModifier_OverridesTheAccessModifier()
    {
        var pb = NewClassBuilder().DefineProperty<int>("Count").WithAccessModifier(AccessModifier.Private);

        pb.ToString().Should().Be("private int Count { get; set; }");
    }

    [TestMethod]
    public void GetOnly_DropsTheSetter()
    {
        var pb = NewClassBuilder().DefineProperty<int>("Count").GetOnly();

        pb.ToString().Should().Be("public int Count { get; }");
    }

    [TestMethod]
    public void FluentMethods_Chain_AndPreserveGenericType()
    {
        var pb = NewClassBuilder()
            .DefineProperty<string>("Name")
            .WithAccessModifier(AccessModifier.Internal)
            .Static()
            .GetOnly();

        pb.Should().BeOfType<PropertyBuilder<string>>();
        pb.ToString().Should().Be("internal static string Name { get; }");
    }

    [TestMethod]
    public void FluentMethods_MutateInPlace_ReturningTheSameInstance()
    {
        var pb = NewClassBuilder().DefineProperty<int>("Count");

        pb.Static().Should().BeSameAs(pb);
    }

    [TestMethod]
    public void WithInitializer_IntLiteral_EmitsDefaultValueAndSemicolon()
    {
        var pb = NewClassBuilder().DefineProperty<int>("Count").WithInitializer(5);

        pb.ToString().Should().Be("public int Count { get; set; } = 5;");
    }

    [TestMethod]
    public void WithInitializer_StringLiteral_EmitsQuotedValue()
    {
        var pb = NewClassBuilder().DefineProperty<string>("Name").WithInitializer("hello");

        pb.ToString().Should().Be("public string Name { get; set; } = \"hello\";");
    }

    [TestMethod]
    public void WithInitializer_BoolLiteral_EmitsKeyword()
    {
        var pb = NewClassBuilder().DefineProperty<bool>("Enabled").WithInitializer(true);

        pb.ToString().Should().Be("public bool Enabled { get; set; } = true;");
    }

    [TestMethod]
    public void WithInitializer_OnGetOnlyProperty_EmitsInitializedReadonlyAutoProperty()
    {
        var pb = NewClassBuilder().DefineProperty<int>("Count").GetOnly().WithInitializer(42);

        pb.ToString().Should().Be("public int Count { get; } = 42;");
    }

    [TestMethod]
    public void WithInitializerExpression_EmitsRawExpression()
    {
        var pb = NewClassBuilder().DefineProperty<System.TimeSpan>("Timeout")
            .WithInitializerExpression("TimeSpan.Zero");

        pb.ToString().Should().Be("public System.TimeSpan Timeout { get; set; } = TimeSpan.Zero;");
    }

    [TestMethod]
    public void WithInitializer_UnsupportedType_Throws()
    {
        var pb = NewClassBuilder().DefineProperty<object>("Thing");

        var act = () => pb.WithInitializer(new object());

        act.Should().Throw<NotSupportedException>();
    }

    [TestMethod]
    public void InitOnly_EmitsInitAccessor()
    {
        var pb = NewClassBuilder().DefineProperty<int>("Count").InitOnly();

        pb.ToString().Should().Be("public int Count { get; init; }");
    }

    [TestMethod]
    public void WithSetterAccessModifier_EmitsRestrictedSetter()
    {
        var pb = NewClassBuilder().DefineProperty<int>("Count").WithSetterAccessModifier(AccessModifier.Private);

        pb.ToString().Should().Be("public int Count { get; private set; }");
    }

    [TestMethod]
    public void SetterAccessModifier_AppliesToInitAccessor()
    {
        var pb = NewClassBuilder().DefineProperty<int>("Count")
            .InitOnly()
            .WithSetterAccessModifier(AccessModifier.Protected);

        pb.ToString().Should().Be("public int Count { get; protected init; }");
    }

    [TestMethod]
    public void InitOnly_WithInitializer_Composes()
    {
        var pb = NewClassBuilder().DefineProperty<int>("Count").InitOnly().WithInitializer(7);

        pb.ToString().Should().Be("public int Count { get; init; } = 7;");
    }

    private static ClassBuilder NewClassBuilder()
        => NamespaceBuilder.Get("Test").Class("Test1");
}
