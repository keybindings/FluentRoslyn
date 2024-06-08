using Generatr.Builders;
using Generatr.Builders.KeywordBuilders;

namespace Generatr.UnitTests.Constants;

[TestClass]
public class AccessModifierTests
{
    [TestMethod]
    public void AccessModifier_Public_BuildsAsPublic()
    {
        // Arrange
        var am = AccessModifier.Public;
        var tb = new TabbedBuilder();

        // Act
        am.Build(tb);

        // Assert
        tb.ToString().Should().Be("public");
    }

    [TestMethod]
    public void AccessModifier_Internal_BuildsAsInternal()
    {
        // Arrange
        var am = AccessModifier.Internal;
        var tb = new TabbedBuilder();

        // Act
        am.Build(tb);

        // Assert
        tb.ToString().Should().Be("internal");
    }

    [TestMethod]
    public void AccessModifier_Protected_BuildsAsProtected()
    {
        // Arrange
        var am = AccessModifier.Protected;
        var tb = new TabbedBuilder();

        // Act
        am.Build(tb);

        // Assert
        tb.ToString().Should().Be("protected");
    }

    [TestMethod]
    public void AccessModifier_ProtectedInternal_BuildsAsProtectedInternal()
    {
        // Arrange
        var am = AccessModifier.ProtectedInternal;
        var tb = new TabbedBuilder();

        // Act
        am.Build(tb);

        // Assert
        tb.ToString().Should().Be("protected internal");
    }

    [TestMethod]
    public void AccessModifier_PrivateProtected_BuildsAsPrivateProtected()
    {
        // Arrange
        var am = AccessModifier.PrivateProtected;
        var tb = new TabbedBuilder();

        // Act
        am.Build(tb);

        // Assert
        tb.ToString().Should().Be("private protected");
    }

    [TestMethod]
    public void AccessModifier_Private_BuildsAsPrivate()
    {
        // Arrange
        var am = AccessModifier.Private;
        var tb = new TabbedBuilder();

        // Act
        am.Build(tb);

        // Assert
        tb.ToString().Should().Be("private");
    }
}