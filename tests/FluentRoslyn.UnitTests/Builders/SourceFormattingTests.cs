using FluentRoslyn.Builders;

namespace FluentRoslyn.UnitTests.Builders;

[TestClass]
public class SourceFormattingTests
{
    [TestMethod]
    public void Default_IsFourSpacesAndLineFeed()
    {
        var cb = NewClass();
        cb.DefineProperty<int>("X");

        // The default must stay byte-identical across operating systems.
        cb.ToString().Should().Be(string.Join("\n",
            "namespace N;",
            "public class C",
            "{",
            "    public int X { get; set; }",
            "}"));
    }

    [TestMethod]
    public void WithIndentation_Tabs_IndentsWithTabs()
    {
        var cb = NewFile().WithIndentation("\t").Class("C");
        cb.DefineProperty<int>("X");

        cb.ToString().Should().Contain("\tpublic int X").And.NotContain("    public int X");
    }

    [TestMethod]
    public void WithIndentation_TwoSpaces_Applies()
    {
        var cb = NewFile().WithIndentation("  ").Class("C");
        cb.DefineProperty<int>("X");

        cb.ToString().Should().Contain("\n  public int X");
    }

    [TestMethod]
    public void WithLineEndings_CrLf_Applies()
    {
        var cb = NewFile().WithIndentation("    ").WithLineEndings("\r\n").Class("C");
        cb.DefineProperty<int>("X");

        var value = cb.ToString();
        value.Should().Contain("\r\n");
        value.Should().Be(string.Join("\r\n",
            "namespace N;",
            "public class C",
            "{",
            "    public int X { get; set; }",
            "}"));
    }

    [TestMethod]
    public void Formatting_CarriesIntoToSourceText()
    {
        var cb = NewFile().WithLineEndings("\r\n").Class("C");
        cb.DefineProperty<int>("X");

        cb.ToSourceText().ToString().Should().Contain("\r\n");
    }

    [TestMethod]
    public void Formatting_AppliesToEveryTypeKind()
    {
        SourceFile.InNamespace("N").WithIndentation("\t").Record("R").WithParameter<int>("X")
            .ToString().Should().NotBeNullOrEmpty();

        var e = SourceFile.InNamespace("N").WithIndentation("\t").Enum("E").AddMember("A");
        e.ToString().Should().Contain("\tA");

        var i = SourceFile.InNamespace("N").WithIndentation("\t").Interface("I");
        i.DefineMethod("M");
        i.ToString().Should().Contain("\tvoid M();");

        SourceFile.InNamespace("N").WithLineEndings("\r\n").Delegate("D")
            .ToString().Should().Contain("\r\n");
    }

    [DataTestMethod]
    [DataRow("x")]
    [DataRow("- ")]
    public void WithIndentation_NonWhitespace_Throws(string indentation)
    {
        // Anything but whitespace would corrupt the source, not merely restyle it.
        var act = () => NewFile().WithIndentation(indentation);

        act.Should().Throw<ArgumentException>();
    }

    [DataTestMethod]
    [DataRow("<br>")]
    [DataRow("")]
    public void WithLineEndings_NotALineEnding_Throws(string lineEndings)
    {
        var act = () => NewFile().WithLineEndings(lineEndings);

        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void SourceFormatting_IsImmutable()
    {
        var original = SourceFormatting.Default;

        var modified = original.WithIndentation("\t").WithLineEndings("\r\n");

        original.Indentation.Should().Be("    ");
        original.LineEndings.Should().Be("\n");
        modified.Indentation.Should().Be("\t");
        modified.LineEndings.Should().Be("\r\n");
    }

    private static SourceFile NewFile() => SourceFile.InNamespace("N");

    private static ClassBuilder NewClass() => NewFile().Class("C");
}
