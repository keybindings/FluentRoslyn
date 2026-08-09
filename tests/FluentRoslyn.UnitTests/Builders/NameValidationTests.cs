using System.Collections.Generic;
using FluentRoslyn.Builders;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace FluentRoslyn.UnitTests.Builders;

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

    // R3-12. SyntaxFacts.IsValidIdentifier is lexical only, so every reserved keyword
    // passed the check that exists to stop unparseable output. These pin the refusal on
    // each path a name can enter by, because the bug was one shared helper and so the
    // regression would be too.

    [DataTestMethod]
    [DataRow("class")]
    [DataRow("int")]
    [DataRow("return")]
    [DataRow("namespace")]
    [DataRow("static")]
    [DataRow("void")]
    [DataRow("__arglist")]
    public void KeywordName_Throws(string keyword)
    {
        var act = () => NamespaceBuilder.Get("N").Class(keyword);

        act.Should().Throw<ArgumentException>().WithMessage($"*'{keyword}' is a C# keyword*");
    }

    [TestMethod]
    public void KeywordMessage_NamesTheVerbatimEscape()
    {
        var act = () => NamespaceBuilder.Get("N").Class("class");

        act.Should().Throw<ArgumentException>().WithMessage("*'@class'*");
    }

    [TestMethod]
    public void KeywordTypeName_ThrowsForEveryTypeKind()
    {
        var ns = NamespaceBuilder.Get("N");

        ((Action)(() => ns.Class("class"))).Should().Throw<ArgumentException>();
        ((Action)(() => ns.Struct("struct"))).Should().Throw<ArgumentException>();
        ((Action)(() => ns.Interface("interface"))).Should().Throw<ArgumentException>();
        ((Action)(() => ns.Record("double"))).Should().Throw<ArgumentException>();
        ((Action)(() => ns.Enum("enum"))).Should().Throw<ArgumentException>();
        ((Action)(() => ns.Delegate("delegate"))).Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void KeywordMemberName_ThrowsForEveryMemberKind()
    {
        var c = NamespaceBuilder.Get("N").Class("C");

        ((Action)(() => c.DefineField<int>("int"))).Should().Throw<ArgumentException>();
        ((Action)(() => c.DefineProperty<int>("class"))).Should().Throw<ArgumentException>();
        ((Action)(() => c.DefineMethod("return"))).Should().Throw<ArgumentException>();
        ((Action)(() => c.DefineEvent<Action>("event"))).Should().Throw<ArgumentException>();
        ((Action)(() => c.DefineDelegate("delegate"))).Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void KeywordParameterName_Throws()
    {
        var act = () => NamespaceBuilder.Get("N").Class("C").DefineMethod("M").WithParameter<int>("params");

        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void KeywordEnumMemberName_Throws()
    {
        var act = () => NamespaceBuilder.Get("N").Enum("E").AddMember("default");

        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void KeywordNamespaceLevel_Throws()
    {
        var act = () => NamespaceBuilder.Get("Valid.namespace.Also");

        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void KeywordTypeParameterName_Throws()
    {
        // The type-parameter path had no name check at all, so `class Box<int>` emitted
        // with nothing said. It now runs the same validator as every other name.
        var act = () => NamespaceBuilder.Get("N").Class("Box").WithTypeParameter("int");

        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void InvalidTypeParameterName_Throws()
    {
        var act = () => NamespaceBuilder.Get("N").Class("Box").WithTypeParameter("not valid");

        act.Should().Throw<ArgumentException>();
    }

    [DataTestMethod]
    [DataRow("value")]
    [DataRow("var")]
    [DataRow("record")]
    [DataRow("async")]
    [DataRow("dynamic")]
    [DataRow("nameof")]
    [DataRow("when")]
    public void ContextualKeywordName_IsAllowed(string name)
    {
        // Contextual keywords are legal identifiers, which is why the check is
        // GetKeywordKind and not GetContextualKeywordKind.
        var act = () => NamespaceBuilder.Get("N").Class("C").DefineProperty<int>(name);

        act.Should().NotThrow();
    }

    [TestMethod]
    public void VerbatimKeywordNames_EmitSourceThatParses()
    {
        var file = SourceFile.InNamespace("N");
        var c = file.Class("@class");
        c.DefineProperty<int>("@int");
        c.DefineMethod("@return").WithParameter<int>("@params");

        var source = file.ToString();

        source.Should().Contain("class @class").And.Contain("int @int").And.Contain("int @params");
        ParseErrors(source).Should().BeEmpty();
    }

    [TestMethod]
    public void KeywordTypeParameter_VerbatimForm_EmitsAndParses()
    {
        var file = SourceFile.InNamespace("N");
        file.Class("Box").WithTypeParameter("@class");

        var source = file.ToString();

        source.Should().Contain("class Box<@class>");
        ParseErrors(source).Should().BeEmpty();
    }

    private static IEnumerable<Diagnostic> ParseErrors(string source)
        => CSharpSyntaxTree.ParseText(source).GetDiagnostics();
}
