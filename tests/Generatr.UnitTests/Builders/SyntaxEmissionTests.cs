using System.Text;
using Generatr.Builders;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Generatr.UnitTests.Builders;

/// <summary>
/// Coverage for the Roslyn-backed emission surface introduced by the SyntaxFactory rewrite:
/// DefineProperty/DefineMethod reaching class output, member group ordering,
/// compilation-unit shapes, and SourceText production.
/// </summary>
[TestClass]
public class SyntaxEmissionTests
{
    private static ClassBuilder NewClass(string name = "TestClass")
        => NamespaceBuilder.Get("TestNamespace").Class(name);

    [TestMethod]
    public void DefineProperty_ReachesClassOutput()
    {
        var cb = NewClass();
        cb.DefineProperty<int>("Count");

        var expected = string.Join(Environment.NewLine,
            "namespace TestNamespace;",
            "public class TestClass",
            "{",
            "    public int Count { get; set; }",
            "}");
        cb.ToString().Should().Be(expected);
    }

    [TestMethod]
    public void DefineMethod_ReachesClassOutput()
    {
        var cb = NewClass();
        cb.DefineMethod("DoThing", AccessModifier.Public, Parameter<int>.New("count"));

        var expected = string.Join(Environment.NewLine,
            "namespace TestNamespace;",
            "public class TestClass",
            "{",
            "    public void DoThing(int count)",
            "    {",
            "    }",
            "}");
        cb.ToString().Should().Be(expected);
    }

    [TestMethod]
    public void MemberGroups_EmitFieldsThenPropertiesThenMethods()
    {
        var cb = NewClass();
        cb.DefineMethod("ZMethod");
        cb.DefineProperty<int>("Count");
        cb.DefineField<string>("_name");

        var value = cb.ToString();

        var fieldIndex = value.IndexOf("_name", StringComparison.Ordinal);
        var propertyIndex = value.IndexOf("Count", StringComparison.Ordinal);
        var methodIndex = value.IndexOf("ZMethod", StringComparison.Ordinal);

        fieldIndex.Should().BePositive().And.BeLessThan(propertyIndex);
        propertyIndex.Should().BeLessThan(methodIndex);
    }

    [TestMethod]
    public void BuildCompilationUnit_FileScoped_ContainsFileScopedNamespaceNode()
    {
        var cu = NewClass().BuildCompilationUnit();

        cu.Members.Should().ContainSingle()
            .Which.Should().BeOfType<FileScopedNamespaceDeclarationSyntax>();
    }

    [TestMethod]
    public void BuildCompilationUnit_BlockScoped_ContainsNamespaceDeclarationNode()
    {
        var cu = NewClass().BlockScopedNamespace().BuildCompilationUnit();

        cu.Members.Should().ContainSingle()
            .Which.Should().BeOfType<NamespaceDeclarationSyntax>();
    }

    [TestMethod]
    public void ToSourceText_UsesUtf8AndMatchesToString()
    {
        var cb = NewClass();
        var sourceText = cb.ToSourceText();

        sourceText.Encoding.Should().Be(Encoding.UTF8);
        sourceText.ToString().Should().Be(cb.ToString());
    }
}
