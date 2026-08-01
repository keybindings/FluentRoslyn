using System.Collections.Generic;
using FluentRoslyn.Builders;

namespace FluentRoslyn.UnitTests.Builders;

[TestClass]
public class UsingDirectiveTests
{
    [TestMethod]
    public void SimplifyTypeNames_ShortensAndImports()
    {
        var cb = NewClass().SimplifyTypeNames();
        cb.DefineField<List<int>>("_items");

        cb.ToString().Should().Be(string.Join("\n",
            "using System.Collections.Generic;",
            "",
            "namespace MyApp;",
            "public class Repo",
            "{",
            "    private List<int> _items;",
            "}"));
    }

    [TestMethod]
    public void WithoutSimplify_NamesStayFullyQualified()
    {
        var cb = NewClass();
        cb.DefineField<List<int>>("_items");

        cb.ToString().Should().Contain("System.Collections.Generic.List<int> _items")
            .And.NotContain("using");
    }

    [TestMethod]
    public void WithUsing_EmitsExplicitDirectiveWithoutSimplifying()
    {
        var cb = NewClass().WithUsing("System.Linq");
        cb.DefineField<List<int>>("_items");

        var value = cb.ToString();
        value.Should().Contain("using System.Linq;");
        // Explicit usings do not imply simplification.
        value.Should().Contain("System.Collections.Generic.List<int>");
    }

    [TestMethod]
    public void Usings_SortSystemFirstThenAlphabetically()
    {
        var cb = NewClass()
            .WithUsing("MyApp.Other")
            .WithUsing("System.Linq")
            .WithUsing("Acme.Widgets")
            .WithUsing("System");

        cb.ToString().Should().StartWith(string.Join("\n",
            "using System;",
            "using System.Linq;",
            "using Acme.Widgets;",
            "using MyApp.Other;"));
    }

    [TestMethod]
    public void AmbiguousSimpleName_StaysFullyQualified()
    {
        // Two namespaces both offer `Task`, so importing either would be ambiguous.
        var cb = NewClass().SimplifyTypeNames();
        cb.DefineField<System.Threading.Tasks.Task>("_a");
        cb.DefineField<Colliding.Task>("_b");

        var value = cb.ToString();
        value.Should().Contain("System.Threading.Tasks.Task _a");
        value.Should().Contain("FluentRoslyn.UnitTests.Colliding.Task _b");
        value.Should().NotContain("using");
    }

    [TestMethod]
    public void NameDeclaredInThisFile_StaysFullyQualified()
    {
        // Importing System.Collections.Generic would be shadowed by the nested List.
        var cb = NewClass().SimplifyTypeNames();
        cb.DefineClass("List");
        cb.DefineField<List<int>>("_items");

        cb.ToString().Should().Contain("System.Collections.Generic.List<int> _items")
            .And.NotContain("using");
    }

    [TestMethod]
    public void SameNamespaceReference_SimplifiedWithoutImport()
    {
        var other = NamespaceBuilder.Get("MyApp").Class("Base");
        var derived = NamespaceBuilder.Get("MyApp").Class("Derived").SimplifyTypeNames().WithParent(other);

        derived.ToString().Should().Contain("public class Derived : Base")
            .And.NotContain("using MyApp;");
    }

    [TestMethod]
    public void ExplicitUsingOfOwnNamespace_IsDropped()
    {
        var cb = NewClass().WithUsing("MyApp");

        cb.ToString().Should().NotContain("using MyApp;");
    }

    [TestMethod]
    public void Usings_WorkWithBlockScopedNamespace()
    {
        var cb = NewClass().BlockScopedNamespace().SimplifyTypeNames();
        cb.DefineField<List<int>>("_items");

        cb.ToString().Should().Be(string.Join("\n",
            "using System.Collections.Generic;",
            "",
            "namespace MyApp",
            "{",
            "    public class Repo",
            "    {",
            "        private List<int> _items;",
            "    }",
            "}"));
    }

    [TestMethod]
    public void Simplify_AppliesToNestedTypeReferences()
    {
        var inner = NamespaceBuilder.Get("MyApp").Class("Outer").DefineClass("Inner");
        var consumer = NamespaceBuilder.Get("Other").Class("Consumer").SimplifyTypeNames().WithParent(inner);

        // Shortening the namespace qualification leaves the nested path intact.
        consumer.ToString().Should().Contain("using MyApp;").And.Contain(": Outer.Inner");
    }

    [TestMethod]
    public void Simplify_OnRecordEnumAndInterface()
    {
        var record = NamespaceBuilder.Get("MyApp").Record("Box").SimplifyTypeNames().WithParameter<List<int>>("Items");
        var iface = NamespaceBuilder.Get("MyApp").Interface("IRepo").SimplifyTypeNames();
        iface.DefineMethod<List<int>>("All");

        record.ToString().Should().Contain("using System.Collections.Generic;").And.Contain("Box(List<int> Items)");
        iface.ToString().Should().Contain("using System.Collections.Generic;").And.Contain("List<int> All();");
    }

    [TestMethod]
    public void WithUsing_InvalidNamespace_Throws()
    {
        var act = () => NewClass().WithUsing("Not.A Valid.Name");

        act.Should().Throw<ArgumentException>();
    }

    private static ClassBuilder NewClass()
        => NamespaceBuilder.Get("MyApp").Class("Repo");
}
