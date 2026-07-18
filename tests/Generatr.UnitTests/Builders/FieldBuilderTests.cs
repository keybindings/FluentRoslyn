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


    private static ClassBuilder NewClassBuilder()
    {
        var namespaceBuilder = NamespaceBuilder.Get("Test");
        var classBuilder = namespaceBuilder.Class("Test1");
        return classBuilder;
    }
}
