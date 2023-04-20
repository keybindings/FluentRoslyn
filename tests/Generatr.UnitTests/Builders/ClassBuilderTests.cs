using FluentAssertions;
using Generatr.Builders;
using Generatr.Enums;

namespace Generatr.UnitTests.Builders;

[TestClass]
public class ClassBuilderTests
{
    private static readonly NamespaceBuilder NamespaceBuilder = NamespaceBuilder.New("TestNamespace");
    private static readonly ClassBuilder ClassBuilder = NamespaceBuilder.Class("TestClass");

    private static readonly StandardAccessModifier[] StandardAccessModifiers = new[]
    {
        StandardAccessModifier.Public,
        StandardAccessModifier.Internal,
        StandardAccessModifier.Protected,
        StandardAccessModifier.Private,
    };

    [TestMethod]
    public void NewClass_UsingTestNamespace_ClassNotNull()
    {
        ClassBuilder.Should().NotBeNull();
    }

    [TestMethod]
    public void NewClass_UsingTestNamespace_NamespaceUsedMatches()
    {
        ClassBuilder.Namespace.Should().Be(NamespaceBuilder);
    }

    [TestMethod]
    public void NewClass_UsingTestNamespace_DefaultAccessModifierPublic()
    {
        ClassBuilder.AccessModifier.Should().Be(StandardAccessModifier.Public);
    }
    [TestMethod]
    public void NewClass_SetWithAccessModifier_AccessModifierSetCorrectly()
    {
        foreach (var modifier in StandardAccessModifiers)
        {
            var classBuilder = NamespaceBuilder.Class("TestClass").SetAccessModifier(modifier);
            classBuilder.AccessModifier.Should().Be(modifier);
        }
    }

    [TestMethod]
    public void NewClass_ParentTypeSetCorrectly()
    {
        var testNamespaceBuilder = NamespaceBuilder.New("TestNamespace123");
        var testClassBuilder = testNamespaceBuilder.Class("TestClass123");
        var builder = testClassBuilder.SetParent(ClassBuilder);
        builder.ParentType.Should().Be(ClassBuilder);
    }


}