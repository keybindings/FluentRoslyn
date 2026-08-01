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
        var cb = NewClass().WithIndentation("\t");
        cb.DefineProperty<int>("X");

        cb.ToString().Should().Contain("\tpublic int X").And.NotContain("    public int X");
    }

    [TestMethod]
    public void WithIndentation_TwoSpaces_Applies()
    {
        var cb = NewClass().WithIndentation("  ");
        cb.DefineProperty<int>("X");

        cb.ToString().Should().Contain("\n  public int X");
    }

    [TestMethod]
    public void WithLineEndings_CrLf_Applies()
    {
        var cb = NewClass().WithIndentation("    ").WithLineEndings("\r\n");
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
        var cb = NewClass().WithLineEndings("\r\n");
        cb.DefineProperty<int>("X");

        cb.ToSourceText().ToString().Should().Contain("\r\n");
    }

    [TestMethod]
    public void Formatting_AppliesToEveryTypeKind()
    {
        NamespaceBuilder.Get("N").Record("R").WithIndentation("\t").WithParameter<int>("X")
            .ToString().Should().NotBeNullOrEmpty();

        var e = NamespaceBuilder.Get("N").Enum("E").WithIndentation("\t").AddMember("A");
        e.ToString().Should().Contain("\tA");

        var i = NamespaceBuilder.Get("N").Interface("I").WithIndentation("\t");
        i.DefineMethod("M");
        i.ToString().Should().Contain("\tvoid M();");

        NamespaceBuilder.Get("N").Delegate("D").WithLineEndings("\r\n")
            .ToString().Should().Contain("\r\n");
    }

    [DataTestMethod]
    [DataRow("x")]
    [DataRow("- ")]
    public void WithIndentation_NonWhitespace_Throws(string indentation)
    {
        // Anything but whitespace would corrupt the source, not merely restyle it.
        var act = () => NewClass().WithIndentation(indentation);

        act.Should().Throw<ArgumentException>();
    }

    [DataTestMethod]
    [DataRow("<br>")]
    [DataRow("")]
    public void WithLineEndings_NotALineEnding_Throws(string lineEndings)
    {
        var act = () => NewClass().WithLineEndings(lineEndings);

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

    private static ClassBuilder NewClass()
        => NamespaceBuilder.Get("N").Class("C");
}
