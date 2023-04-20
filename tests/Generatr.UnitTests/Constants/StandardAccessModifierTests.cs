using FluentAssertions;
using Generatr.Enums;

namespace Generatr.UnitTests.Constants;

[TestClass]
public class StandardAccessModifierTests
{
    [TestMethod]
    public void StandardAccessModifier_Public_NameIsPublic()
    {
        var am = StandardAccessModifier.Public;
        am.Name.Should().Be("public");
    }
    [TestMethod]
    public void StandardAccessModifier_Public_NameIsInternal()
    {
        var am = StandardAccessModifier.Public;
        am.Name.Should().Be("public");
    }
    [TestMethod]
    public void StandardAccessModifier_Public_NameIsProtected()
    {
        var am = StandardAccessModifier.Public;
        am.Name.Should().Be("public");
    }
    [TestMethod]
    public void StandardAccessModifier_Public_NameIsPrivate()
    {
        var am = StandardAccessModifier.Public;
        am.Name.Should().Be("public");
    }

}