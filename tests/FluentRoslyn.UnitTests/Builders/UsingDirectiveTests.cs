using System.Collections.Generic;
using FluentRoslyn.Builders;

namespace FluentRoslyn.UnitTests.Builders;

[TestClass]
public class UsingDirectiveTests
{
    [TestMethod]
    public void SimplifyTypeNames_ShortensAndImports()
    {
        var cb = NewFile().SimplifyTypeNames().Class("Repo");
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
        var cb = NewFile().WithUsing("System.Linq").Class("Repo");
        cb.DefineField<List<int>>("_items");

        var value = cb.ToString();
        value.Should().Contain("using System.Linq;");
        // Explicit usings do not imply simplification.
        value.Should().Contain("System.Collections.Generic.List<int>");
    }

    [TestMethod]
    public void Usings_SortSystemFirstThenAlphabetically()
    {
        var cb = NewFile()
            .WithUsing("MyApp.Other")
            .WithUsing("System.Linq")
            .WithUsing("Acme.Widgets")
            .WithUsing("System")
            .Class("Repo");

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
        var cb = NewFile().SimplifyTypeNames().Class("Repo");
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
        var cb = NewFile().SimplifyTypeNames().Class("Repo");
        cb.DefineClass("List");
        cb.DefineField<List<int>>("_items");

        cb.ToString().Should().Contain("System.Collections.Generic.List<int> _items")
            .And.NotContain("using");
    }

    [TestMethod]
    public void SameNamespaceReference_SimplifiedWithoutImport()
    {
        var other = NamespaceBuilder.Get("MyApp").Class("Base");
        var derived = NewFile().SimplifyTypeNames().Class("Derived").WithParent(other);

        derived.ToString().Should().Contain("public class Derived : Base")
            .And.NotContain("using MyApp;");
    }

    [TestMethod]
    public void ExplicitUsingOfOwnNamespace_IsDropped()
    {
        var cb = NewFile().WithUsing("MyApp").Class("Repo");

        cb.ToString().Should().NotContain("using MyApp;");
    }

    [TestMethod]
    public void Usings_WorkWithBlockScopedNamespace()
    {
        var cb = NewFile().BlockScopedNamespace().SimplifyTypeNames().Class("Repo");
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
        var consumer = SourceFile.InNamespace("Other").SimplifyTypeNames().Class("Consumer").WithParent(inner);

        // Shortening the namespace qualification leaves the nested path intact.
        consumer.ToString().Should().Contain("using MyApp;").And.Contain(": Outer.Inner");
    }

    [TestMethod]
    public void Simplify_OnRecordEnumAndInterface()
    {
        var record = NewFile().SimplifyTypeNames().Record("Box").WithParameter<List<int>>("Items");
        var iface = NewFile().SimplifyTypeNames().Interface("IRepo");
        iface.DefineMethod<List<int>>("All");

        record.ToString().Should().Contain("using System.Collections.Generic;").And.Contain("Box(List<int> Items)");
        iface.ToString().Should().Contain("using System.Collections.Generic;").And.Contain("List<int> All();");
    }

    // The point of a file-level scope: several types share one set of usings, computed
    // once across all of them.
    [TestMethod]
    public void TypesSharingAFile_ShareItsUsings()
    {
        var file = SourceFile.InNamespace("MyApp").SimplifyTypeNames();
        file.Class("Repo").DefineField<List<int>>("_items");
        file.Record("Box").WithParameter<List<string>>("Values");

        file.ToString().Should().Be(string.Join("\n",
            "using System.Collections.Generic;",
            "",
            "namespace MyApp;",
            "public class Repo",
            "{",
            "    private List<int> _items;",
            "}",
            "",
            "public record Box(List<string> Values);"));
    }

    // Ambiguity is judged across the whole file, not per type: a name one type declares
    // blocks the import for every other type in the file, because the import would be
    // shadowed there too.
    [TestMethod]
    public void NameDeclaredByAnotherTypeInTheFile_BlocksTheImport()
    {
        var file = SourceFile.InNamespace("MyApp").SimplifyTypeNames();
        var repo = file.Class("Repo");
        repo.DefineField<List<int>>("_items");
        file.Class("List");

        file.ToString().Should().Contain("System.Collections.Generic.List<int> _items")
            .And.NotContain("using");
    }

    [TestMethod]
    public void WithUsing_InvalidNamespace_Throws()
    {
        var act = () => NewFile().WithUsing("Not.A Valid.Name");

        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void FileWithNoTypes_Throws()
    {
        var act = () => SourceFile.InNamespace("MyApp").ToString();

        act.Should().Throw<InvalidOperationException>().WithMessage("*declares no types*");
    }

    private static SourceFile NewFile() => SourceFile.InNamespace("MyApp");

    private static ClassBuilder NewClass() => NewFile().Class("Repo");
}
