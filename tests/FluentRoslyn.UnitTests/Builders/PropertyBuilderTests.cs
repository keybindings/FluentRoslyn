using FluentRoslyn.Builders;

namespace FluentRoslyn.UnitTests.Builders;

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

    [TestMethod]
    public void StaticInitOnly_Throws()
    {
        var pb = NewClassBuilder().DefineProperty<int>("Count").Static().InitOnly();

        var act = () => pb.ToString();

        act.Should().Throw<InvalidOperationException>().WithMessage("*static property cannot have an init accessor*");
    }

    #region Setter access modifier validation

    [TestMethod]
    public void WithSetterAccessModifier_LessRestrictiveThanProperty_Throws()
    {
        var pb = NewClassBuilder().DefineProperty<int>("Count", AccessModifier.Internal)
            .WithSetterAccessModifier(AccessModifier.Public);

        var act = () => pb.ToString();

        act.Should().Throw<InvalidOperationException>().WithMessage("*more restrictive*");
    }

    [TestMethod]
    public void WithSetterAccessModifier_EqualToProperty_Throws()
    {
        var pb = NewClassBuilder().DefineProperty<int>("Count", AccessModifier.Public)
            .WithSetterAccessModifier(AccessModifier.Public);

        var act = () => pb.ToString();

        act.Should().Throw<InvalidOperationException>();
    }

    [TestMethod]
    public void WithSetterAccessModifier_IncomparableWithProperty_Throws()
    {
        // protected and internal are incomparable: neither is more restrictive.
        var pb = NewClassBuilder().DefineProperty<int>("Count", AccessModifier.Protected)
            .WithSetterAccessModifier(AccessModifier.Internal);

        var act = () => pb.ToString();

        act.Should().Throw<InvalidOperationException>();
    }

    [TestMethod]
    public void WithSetterAccessModifier_ProtectedInternalProperty_ProtectedSetter_Allowed()
    {
        var pb = NewClassBuilder().DefineProperty<int>("Count", AccessModifier.ProtectedInternal)
            .WithSetterAccessModifier(AccessModifier.Protected);

        pb.ToString().Should().Be("protected internal int Count { get; protected set; }");
    }

    [TestMethod]
    public void WithSetterAccessModifier_OnGetOnlyProperty_Throws()
    {
        var pb = NewClassBuilder().DefineProperty<int>("Count")
            .GetOnly()
            .WithSetterAccessModifier(AccessModifier.Private);

        var act = () => pb.ToString();

        act.Should().Throw<InvalidOperationException>().WithMessage("*no setter*");
    }

    #endregion

    #region Expression bodies

    [TestMethod]
    public void AsExpressionBody_EmitsArrowProperty()
    {
        var pb = NewClassBuilder().DefineProperty<int>("Count").AsExpressionBody("_count");

        pb.ToString().Should().Be("public int Count => _count;");
    }

    [TestMethod]
    public void WithGetterExpression_EmitsExpressionBodiedGetter()
    {
        var pb = NewClassBuilder().DefineProperty<int>("Count").WithGetterExpression("_count");

        pb.ToString().Should().Be("public int Count { get => _count; }");
    }

    [TestMethod]
    public void GetterAndSetterExpressions_EmitBothExpressionBodiedAccessors()
    {
        var pb = NewClassBuilder().DefineProperty<int>("Count")
            .WithGetterExpression("_count")
            .WithSetterExpression("_count = value");

        pb.ToString().Should().Be("public int Count { get => _count; set => _count = value; }");
    }

    [TestMethod]
    public void SetterExpression_WithoutGetter_EmitsWriteOnlyProperty()
    {
        var pb = NewClassBuilder().DefineProperty<int>("Count").WithSetterExpression("_count = value");

        // A bodied property may be write-only (unlike an auto-property).
        pb.ToString().Should().Be("public int Count { set => _count = value; }");
    }

    [TestMethod]
    public void ExpressionBody_WithInitializer_Throws()
    {
        var pb = NewClassBuilder().DefineProperty<int>("Count")
            .WithInitializer(1)
            .AsExpressionBody("_count");

        var act = () => pb.ToString();

        act.Should().Throw<InvalidOperationException>().WithMessage("*initializer*");
    }

    [TestMethod]
    public void ExpressionBodiedSetter_RespectsInitAndAccessModifier()
    {
        var pb = NewClassBuilder().DefineProperty<int>("Count")
            .WithGetterExpression("_count")
            .WithSetterExpression("_count = value")
            .WithSetterAccessModifier(AccessModifier.Private);

        pb.ToString().Should().Be("public int Count { get => _count; private set => _count = value; }");
    }

    [TestMethod]
    public void WithGetterBody_EmitsStatementBodiedGetter()
    {
        var pb = NewClassBuilder().DefineProperty<int>("Count")
            .WithGetterBody("return _count;");

        pb.ToString().Should().Be(string.Join("\n",
            "public int Count",
            "{",
            "    get",
            "    {",
            "        return _count;",
            "    }",
            "}"));
    }

    [TestMethod]
    public void GetterAndSetterBodies_EmitBothStatementBodiedAccessors()
    {
        var pb = NewClassBuilder().DefineProperty<int>("Count")
            .WithGetterBody("return _count;")
            .WithSetterBody("_count = value;");

        // NormalizeWhitespace separates two statement-bodied accessors with a blank line.
        pb.ToString().Should().Be(string.Join("\n",
            "public int Count",
            "{",
            "    get",
            "    {",
            "        return _count;",
            "    }",
            "",
            "    set",
            "    {",
            "        _count = value;",
            "    }",
            "}"));
    }

    [TestMethod]
    public void MixedExpressionGetterAndStatementSetter_Composes()
    {
        var pb = NewClassBuilder().DefineProperty<int>("Count")
            .WithGetterExpression("_count")
            .WithSetterBody("_count = value;");

        pb.ToString().Should().Be(string.Join("\n",
            "public int Count",
            "{",
            "    get => _count;",
            "    set",
            "    {",
            "        _count = value;",
            "    }",
            "}"));
    }

    [TestMethod]
    public void StatementSetterBody_RespectsAccessModifier()
    {
        var pb = NewClassBuilder().DefineProperty<int>("Count")
            .WithGetterExpression("_count")
            .WithSetterBody("_count = value;")
            .WithSetterAccessModifier(AccessModifier.Private);

        pb.ToString().Should().Contain("private set");
    }

    [TestMethod]
    public void SetterBody_WithoutGetter_EmitsWriteOnlyProperty()
    {
        var pb = NewClassBuilder().DefineProperty<int>("Count").WithSetterBody("_count = value;");

        pb.ToString().Should().Be(string.Join("\n",
            "public int Count",
            "{",
            "    set",
            "    {",
            "        _count = value;",
            "    }",
            "}"));
    }

    #endregion

    private static ClassBuilder NewClassBuilder()
        => NamespaceBuilder.Get("Test").Class("Test1");
}
