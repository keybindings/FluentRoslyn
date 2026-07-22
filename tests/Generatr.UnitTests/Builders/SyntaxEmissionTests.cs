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

        var expected = string.Join("\n",
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

        var expected = string.Join("\n",
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

    [TestMethod]
    public void Emission_UsesLineFeed_RegardlessOfHostOperatingSystem()
    {
        var cb = NewClass();
        cb.DefineField<int>("_count");

        cb.ToString().Should().Contain("\n").And.NotContain("\r");
    }

    [TestMethod]
    public void GlobalNamespaceClass_EmitsClassWithoutNamespaceDeclaration()
    {
        var cb = NamespaceBuilder.None.Class("TestClass");

        var expected = string.Join("\n",
            "public class TestClass",
            "{",
            "}");
        cb.ToString().Should().Be(expected);
    }

    [TestMethod]
    public void WithParent_EmitsBaseList()
    {
        var baseClass = NamespaceBuilder.Get("Other").Class("BaseClass");
        var cb = NewClass().WithParent(baseClass);

        cb.ToString().Should().Contain("public class TestClass : Other.BaseClass");
    }

    [TestMethod]
    public void WithParent_GlobalNamespaceParent_EmitsUnqualifiedBaseType()
    {
        var baseClass = NamespaceBuilder.None.Class("BaseClass");
        var cb = NewClass().WithParent(baseClass);

        cb.ToString().Should().Contain("public class TestClass : BaseClass");
    }

    [TestMethod]
    public void Property_GetOnly_OmitsSetAccessor()
    {
        var pb = new PropertyBuilder<int>("Count", AccessModifier.Public) { HasSet = false };

        pb.ToString().Should().Be("public int Count { get; }");
    }

    [TestMethod]
    public void Property_WithoutGetter_Throws()
    {
        var pb = new PropertyBuilder<int>("Count", AccessModifier.Public) { HasGet = false };

        var act = () => pb.ToString();

        act.Should().Throw<InvalidOperationException>();
    }
}
