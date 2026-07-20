using Generatr.Builders;

namespace Generatr.UnitTests.Builders;

[TestClass]
public class FieldBuilderTests
{
    [TestMethod]
    public void BasicStringFieldShouldBePrivate()
    {
        // Setup
        var fb = NewClassBuilder().DefineField<string>("_test");

        // Assert
        fb.ToString().Should().Be("private string _test;");
    }

    [TestMethod]
    public void WhenPrivateSetFieldBasicFieldShouldBePrivate()
    {
        // Setup
        var fb = NewClassBuilder().DefineField<int>("_test", AccessModifier.Private);

        // Assert
        fb.ToString().Should().Be("private int _test;");
    }

    [TestMethod]
    public void WhenInternalSetFieldBasicFieldShouldBeInternal()
    {
        // Setup
        var fb = NewClassBuilder().DefineField<double>("_test", AccessModifier.Internal);

        // Assert
        fb.ToString().Should().Be("internal double _test;");
    }

    [TestMethod]
    public void WhenPublicSetFieldBasicFieldShouldBePublic()
    {
        // Setup
        var fb = NewClassBuilder().DefineField<float>("_test", AccessModifier.Public);

        // Assert
        fb.ToString().Should().Be("public float _test;");
    }

    [TestMethod]
    public void WhenProtectedSetFieldBasicFieldShouldBeProtected()
    {
        // Setup
        var fb = NewClassBuilder().DefineField<bool>("_test", AccessModifier.Protected);

        // Assert
        fb.ToString().Should().Be("protected bool _test;");
    }

    [TestMethod]
    public void WhenReadonlySetFieldBasicFieldShouldBeReadonly()
    {
        // Setup
        var fb = NewClassBuilder().DefineField<string>("_test");
        fb.IsReadonly = true;

        // Assert
        fb.ToString().Should().Be("private readonly string _test;");
    }

    [TestMethod]
    public void WhenStaticReadonlySetFieldShouldBeStaticReadonly()
    {
        // Setup
        var fb = NewClassBuilder().DefineField<string>("_test");
        fb.IsStatic = true;
        fb.IsReadonly = true;

        // Assert
        fb.ToString().Should().Be("private static readonly string _test;");
    }


    #region Fluent API

    [TestMethod]
    public void Static_EmitsStaticKeyword()
    {
        var fb = NewClassBuilder().DefineField<string>("_test").Static();

        fb.ToString().Should().Be("private static string _test;");
    }

    [TestMethod]
    public void Readonly_EmitsReadonlyKeyword()
    {
        var fb = NewClassBuilder().DefineField<string>("_test").Readonly();

        fb.ToString().Should().Be("private readonly string _test;");
    }

    [TestMethod]
    public void WithAccessModifier_OverridesTheAccessModifier()
    {
        var fb = NewClassBuilder().DefineField<int>("_test").WithAccessModifier(AccessModifier.Public);

        fb.ToString().Should().Be("public int _test;");
    }

    [TestMethod]
    public void FluentMethods_Chain_AndPreserveGenericType()
    {
        var fb = NewClassBuilder().DefineField<string>("_test")
            .WithAccessModifier(AccessModifier.Protected)
            .Static()
            .Readonly();

        fb.Should().BeOfType<FieldBuilder<string>>();
        fb.ToString().Should().Be("protected static readonly string _test;");
    }

    [TestMethod]
    public void FluentMethods_MutateInPlace_ReturningTheSameInstance()
    {
        var fb = NewClassBuilder().DefineField<int>("_test");

        fb.Static().Should().BeSameAs(fb);
    }

    #endregion

    #region Initializers

    [TestMethod]
    public void WithInitializer_IntLiteral_EmitsDefaultValue()
    {
        var fb = NewClassBuilder().DefineField<int>("_count").WithInitializer(5);

        fb.ToString().Should().Be("private int _count = 5;");
    }

    [TestMethod]
    public void WithInitializer_StringLiteral_EmitsQuotedValue()
    {
        var fb = NewClassBuilder().DefineField<string>("_name").WithInitializer("hello");

        fb.ToString().Should().Be("private string _name = \"hello\";");
    }

    [TestMethod]
    public void WithInitializerExpression_EmitsRawExpression()
    {
        var fb = NewClassBuilder().DefineField<System.Collections.Generic.List<int>>("_items")
            .WithInitializerExpression("new()");

        fb.ToString().Should().Be("private System.Collections.Generic.List<int> _items = new();");
    }

    [TestMethod]
    public void StaticReadonly_WithInitializer_Composes()
    {
        var fb = NewClassBuilder().DefineField<int>("_max").Static().Readonly().WithInitializer(100);

        fb.ToString().Should().Be("private static readonly int _max = 100;");
    }

    #endregion

    #region Const

    [TestMethod]
    public void Const_WithInitializer_EmitsConstField()
    {
        var fb = NewClassBuilder().DefineField<int>("MaxValue", AccessModifier.Public)
            .Const()
            .WithInitializer(42);

        fb.ToString().Should().Be("public const int MaxValue = 42;");
    }

    [TestMethod]
    public void Const_WithoutInitializer_Throws()
    {
        var fb = NewClassBuilder().DefineField<int>("MaxValue").Const();

        var act = () => fb.ToString();

        act.Should().Throw<InvalidOperationException>().WithMessage("*requires an initializer*");
    }

    [TestMethod]
    public void Const_WithStatic_Throws()
    {
        var fb = NewClassBuilder().DefineField<int>("MaxValue").Const().WithInitializer(1).Static();

        var act = () => fb.ToString();

        act.Should().Throw<InvalidOperationException>().WithMessage("*cannot also be static or readonly*");
    }

    [TestMethod]
    public void Const_WithReadonly_Throws()
    {
        var fb = NewClassBuilder().DefineField<int>("MaxValue").Const().WithInitializer(1).Readonly();

        var act = () => fb.ToString();

        act.Should().Throw<InvalidOperationException>();
    }

    #endregion

    private static ClassBuilder NewClassBuilder()
    {
        var namespaceBuilder = NamespaceBuilder.Get("Test");
        var classBuilder = namespaceBuilder.Class("Test1");
        return classBuilder;
    }
}
