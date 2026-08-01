using Generatr.Builders;

namespace Generatr.UnitTests.Builders;

[TestClass]
public class NameValidationTests
{
    [DataTestMethod]
    [DataRow("1Invalid")]
    [DataRow("Has Space")]
    [DataRow("Bad'Char")]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("na-me")]
    public void InvalidClassName_Throws(string name)
    {
        var act = () => NamespaceBuilder.Get("N").Class(name);

        act.Should().Throw<ArgumentException>();
    }

    [DataTestMethod]
    [DataRow("Valid")]
    [DataRow("_underscore")]
    [DataRow("Name1")]
    [DataRow("@class")]
    public void ValidClassName_DoesNotThrow(string name)
    {
        var act = () => NamespaceBuilder.Get("N").Class(name);

        act.Should().NotThrow();
    }

    [TestMethod]
    public void InvalidFieldName_Throws()
    {
        var act = () => NamespaceBuilder.Get("N").Class("C").DefineField<int>("has space");

        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void InvalidPropertyName_Throws()
    {
        var act = () => NamespaceBuilder.Get("N").Class("C").DefineProperty<int>("1Bad");

        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void InvalidMethodName_Throws()
    {
        var act = () => NamespaceBuilder.Get("N").Class("C").DefineMethod("bad-name");

        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void InvalidParameterName_Throws()
    {
        var act = () => NamespaceBuilder.Get("N").Class("C").DefineMethod("M").WithParameter<int>("not valid");

        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void InvalidEnumName_Throws()
    {
        var act = () => NamespaceBuilder.Get("N").Enum("1Enum");

        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void InvalidEnumMemberName_Throws()
    {
        var act = () => NamespaceBuilder.Get("N").Enum("E").AddMember("has space");

        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void InvalidInterfaceName_Throws()
    {
        var act = () => NamespaceBuilder.Get("N").Interface("I Thing");

        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void InvalidRecordName_Throws()
    {
        var act = () => NamespaceBuilder.Get("N").Record("Re cord");

        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void InvalidStructName_Throws()
    {
        var act = () => NamespaceBuilder.Get("N").Struct("1Struct");

        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void InvalidNamespaceLevel_Throws()
    {
        var act = () => NamespaceBuilder.Get("Valid.1Bad.Also");

        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void QualifiedNamespace_WithValidLevels_DoesNotThrow()
    {
        var act = () => NamespaceBuilder.Get("A.B.C");

        act.Should().NotThrow();
    }

    [TestMethod]
    public void NullName_ThrowsArgumentNull()
    {
        var act = () => NamespaceBuilder.Get("N").Class(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
