using System.Linq.Expressions;
using Generatr.Builders;
using Generatr.Builders.KeywordBuilders;

namespace Generatr.UnitTests.Builders;

[TestClass]
public class FieldBuilderTests
{
    [TestMethod]
    public void BasicStringFieldShouldBePrivate()
    {
        // Setup
        var tb = new TabbedBuilder();
        var fb = NewClassBuilder().DefineField<string>("_test");

        // Act
        fb.Build(tb);

        // Assert
        tb.ToString().Should().Be("private string _test;");
    }

    [TestMethod]
    public void WhenPrivateSetFieldBasicFieldShouldBePrivate()
    {
        // Setup
        var tb = new TabbedBuilder();
        var fb = NewClassBuilder().DefineField<int>("_test", AccessModifier.Private);

        // Act
        fb.Build(tb);

        // Assert
        tb.ToString().Should().Be("private int _test;");
    }

    [TestMethod]
    public void WhenInternalSetFieldBasicFieldShouldBeInternal()
    {
        // Setup
        var tb = new TabbedBuilder();
        var fb = NewClassBuilder().DefineField<double>("_test", AccessModifier.Internal);

        // Act
        fb.Build(tb);

        // Assert
        tb.ToString().Should().Be("internal double _test;");
    }

    [TestMethod]
    public void WhenPublicSetFieldBasicFieldShouldBePublic()
    {
        // Setup
        var tb = new TabbedBuilder();
        var fb = NewClassBuilder().DefineField<float>("_test", AccessModifier.Public);

        // Act
        fb.Build(tb);

        // Assert
        tb.ToString().Should().Be("public float _test;");
    }

    [TestMethod]
    public void WhenProtectedSetFieldBasicFieldShouldBeProtected()
    {
        // Setup
        var tb = new TabbedBuilder();
        var fb = NewClassBuilder().DefineField<bool>("_test", AccessModifier.Protected);

        // Act
        fb.Build(tb);

        // Assert
        tb.ToString().Should().Be("protected bool _test;");
    }

    [TestMethod]
    public void WhenReadonlySetFieldBasicFieldShouldBeReadonly()
    {
        // Setup
        var tb = new TabbedBuilder();
        var fb = NewClassBuilder().DefineField<string>("_test");
        fb.IsReadonly = true;

        // Act
        fb.Build(tb);

        // Assert
        tb.ToString().Should().Be("private readonly string _test;");
    }


    private static ClassBuilder NewClassBuilder()
    {
        var namespaceBuilder = NamespaceBuilder.Get("Test");
        var classBuilder = namespaceBuilder.Class("Test1");
        return classBuilder;
    }
}