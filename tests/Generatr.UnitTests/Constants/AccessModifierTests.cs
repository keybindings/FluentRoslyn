using Generatr.Builders;

namespace Generatr.UnitTests.Constants;

[TestClass]
public class AccessModifierTests
{
    [TestMethod]
    public void AccessModifier_Public_BuildsAsPublic()
    {
        AccessModifier.Public.ToString().Should().Be("public");
    }

    [TestMethod]
    public void AccessModifier_Internal_BuildsAsInternal()
    {
        AccessModifier.Internal.ToString().Should().Be("internal");
    }

    [TestMethod]
    public void AccessModifier_Protected_BuildsAsProtected()
    {
        AccessModifier.Protected.ToString().Should().Be("protected");
    }

    [TestMethod]
    public void AccessModifier_ProtectedInternal_BuildsAsProtectedInternal()
    {
        AccessModifier.ProtectedInternal.ToString().Should().Be("protected internal");
    }

    [TestMethod]
    public void AccessModifier_PrivateProtected_BuildsAsPrivateProtected()
    {
        AccessModifier.PrivateProtected.ToString().Should().Be("private protected");
    }

    [TestMethod]
    public void AccessModifier_Private_BuildsAsPrivate()
    {
        AccessModifier.Private.ToString().Should().Be("private");
    }
}
