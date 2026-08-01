using FluentRoslyn.Builders;

namespace FluentRoslyn.UnitTests.Builders;


[TestClass]
public class ClassBuilderTests
{
    private static readonly NamespaceBuilder NamespaceBuilder = NamespaceBuilder.Get("TestNamespace");
    private static readonly ClassBuilder ClassBuilder = NamespaceBuilder.Class("TestClass");

    private static readonly AccessModifier[] StandardAccessModifiers = {
        AccessModifier.Public,
        AccessModifier.Internal,
        AccessModifier.Protected,
        AccessModifier.Private,
    };

    [TestMethod]
    public void NewClass_UsingTestNamespace_ClassNotNull()
    {
        //Assert
        ClassBuilder.Should().NotBeNull();
    }

    [TestMethod]
    public void NewClass_UsingTestNamespace_NamespaceUsedMatches()
    {
        //Assert
        ClassBuilder.Namespace.Should().Be(NamespaceBuilder);
    }

    [TestMethod]
    public void NewClass_UsingTestNamespace_DefaultAccessModifierPublic()
    {
        //Assert
        ClassBuilder.AccessModifier.Should().Be(AccessModifier.Public);
    }

    [TestMethod]
    public void NewClass_SetWithAccessModifier_AccessModifierSetCorrectly()
    {
        foreach (var modifier in StandardAccessModifiers)
        {
            var classBuilder = NamespaceBuilder.Class("TestClass").WithAccessModifier(modifier);
            classBuilder.AccessModifier.Should().Be(modifier);
        }
    }

    [TestMethod]
    public void NewClass_ParentTypeSetCorrectly()
    {
        var testNamespaceBuilder = NamespaceBuilder.Get("TestNamespace123");
        var testClassBuilder = testNamespaceBuilder.Class("TestClass123");
        var builder = testClassBuilder.WithParent(ClassBuilder);
        builder.ParentType.Should().Be(ClassBuilder);
    }

    private static readonly string FileScopedExpectedEmptyClassOutput = string.Join("\n",
        "namespace TestNamespace;",
        "public class TestClass1",
        "{",
        "}");

    [TestMethod]
    public void NewEmptyClass_WithFileScopedTestNamespace_ShouldMatchEmptyExpectedOutput()
    {
        var emptyClass = NamespaceBuilder.Class("TestClass1");
        var value = emptyClass.ToString();
        value.Should().Be(FileScopedExpectedEmptyClassOutput);
    }

    private static readonly string ExpectedEmptyClassOutput = string.Join("\n",
        "namespace TestNamespace",
        "{",
        "    public class TestClass2",
        "    {",
        "    }",
        "}");

    [TestMethod]
    public void NewEmptyClass_WithTestNamespace_ShouldMatchExpectedOutput()
    {
        var emptyClass = NamespaceBuilder.Class("TestClass2");
        emptyClass.IsFileScopedNamespace = false;
        var value = emptyClass.ToString();
        value.Should().Be(ExpectedEmptyClassOutput);
    }

    private static readonly string ExpectedOneFieldClassOutput = string.Join("\n",
        "namespace TestNamespace",
        "{",
        "    public class TestClass2",
        "    {",
        "        public System.Collections.Generic.List<System.Collections.Generic.List<string>> TestField;",
        "    }",
        "}");

    [TestMethod]
    public void NewEmptyClass_WithOneField_ShouldMatchExpectedOutput()
    {
        var oneFieldClass = NamespaceBuilder.Class("TestClass2");
        oneFieldClass.IsFileScopedNamespace = false;
        oneFieldClass.DefineField<List<List<string>>>("TestField", AccessModifier.Public);
        var value = oneFieldClass.ToString();
        value.Should().Be(ExpectedOneFieldClassOutput);
    }

    private static readonly string ExpectedStaticClassOutput = string.Join("\n",
        "namespace TestNamespace;",
        "public static partial class TestClass2",
        "{",
        "}");

    [TestMethod]
    public void NewStaticClass_WithTestNamespace_ShouldMatchExpectedOutput()
    {
        var emptyClass = NamespaceBuilder.Class("TestClass2").Static().Partial();
        var value = emptyClass.ToString();
        value.Should().Be(ExpectedStaticClassOutput);
    }

}
