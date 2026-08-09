using System;
using System.Collections.Generic;
using System.Text;
using FluentRoslyn.Abstractions;
using FluentRoslyn.Builders;

namespace FluentRoslyn.UnitTests.Builders;

[EmitsAs("MyApp.Outer+Inner")]
internal sealed class NestedPlaceholder;

[EmitsAs("MyApp.Outer.Inner")]
internal sealed class DottedPlaceholder;

/// <summary>
/// The simplifier findings from Review 3 (R3-02, R3-31 through R3-34, R3-43). Every one of
/// them emits source that reads correctly, so these assert what the compiler makes of it —
/// either that it compiles at all, or which type a shortened name actually binds to.
/// </summary>
[TestClass]
public class SimplifierBindingTests
{
    // R3-02. A type parameter occupies the name for the whole declaration, so the shortened
    // reference binds to it. Nothing diagnoses that: the property just has the wrong type.
    [TestMethod]
    public void TypeParameterOfTheSameName_BlocksSimplification()
    {
        var file = SourceFile.InNamespace("MyApp").SimplifyTypeNames();
        var host = file.Class("Host").WithTypeParameter("StringBuilder");
        host.DefineProperty<StringBuilder>("Builder");

        var source = file.ToString();

        source.Should().Contain("System.Text.StringBuilder Builder").And.NotContain("using System.Text;");
        Compiled.MemberType(source, "Host", "Builder").Should().Be("System.Text.StringBuilder");
    }

    // R3-02, the other half: a delegate is a type declaration too, and shadows just as
    // completely — but it is not a BaseTypeDeclarationSyntax, so the old check missed it.
    [TestMethod]
    public void DelegateOfTheSameName_BlocksSimplification()
    {
        var file = SourceFile.InNamespace("MyApp").SimplifyTypeNames();
        file.Delegate("EventHandler");
        file.Class("Host").DefineField<EventHandler>("_handler");

        var source = file.ToString();

        source.Should().Contain("System.EventHandler _handler");
        Compiled.MemberType(source, "Host", "_handler").Should().Be("System.EventHandler");
    }

    // R3-31. `MyApp` is the file's own enclosing namespace, so it wins over any imported
    // type of that name and the shortened reference is CS0118.
    [TestMethod]
    public void NameMatchingAnEnclosingNamespace_StaysQualified()
    {
        var acme = SourceFile.InNamespace("Acme").Class("MyApp");
        var file = SourceFile.InNamespace("MyApp.Models").SimplifyTypeNames();
        file.Class("Consumer").WithParent(acme);

        var source = file.ToString();

        source.Should().Contain(": Acme.MyApp").And.NotContain("using Acme;");
        Compiled.Errors(source, "namespace Acme { public class MyApp { } }").Should().BeEmpty();
    }

    // R3-31 again, for the file's own last segment rather than a parent.
    [TestMethod]
    public void NameMatchingTheFilesOwnNamespace_StaysQualified()
    {
        var acme = SourceFile.InNamespace("Acme").Class("Models");
        var file = SourceFile.InNamespace("MyApp.Models").SimplifyTypeNames();
        file.Class("Consumer").WithParent(acme);

        var source = file.ToString();

        source.Should().Contain(": Acme.Models").And.NotContain("using Acme;");
        Compiled.Errors(source, "namespace Acme { public class Models { } }").Should().BeEmpty();
    }

    // R3-31 again, for a root namespace the simplifier's own import brings into play.
    [TestMethod]
    public void NameMatchingTheRootOfAnAddedImport_StaysQualified()
    {
        var acme = SourceFile.InNamespace("Acme").Class("System");
        var file = SourceFile.InNamespace("MyApp").SimplifyTypeNames();
        var consumer = file.Class("Consumer").WithParent(acme);
        consumer.DefineField<List<int>>("_items");

        var source = file.ToString();

        source.Should().Contain(": Acme.System").And.Contain("using System.Collections.Generic;");
        Compiled.Errors(source, "namespace Acme { public class System { } }").Should().BeEmpty();
    }

    // R3-31, the sibling case, which is only decidable because an explicit using names the
    // sibling — so this is R3-33's wiring as much as R3-31's rule.
    [TestMethod]
    public void NameMatchingASiblingNamespace_StaysQualified()
    {
        var acme = SourceFile.InNamespace("Acme").Class("Other");
        var file = SourceFile.InNamespace("MyApp.Models").WithUsing("MyApp.Other").SimplifyTypeNames();
        file.Class("Consumer").WithParent(acme);

        var source = file.ToString();

        source.Should().Contain(": Acme.Other").And.NotContain("using Acme;");
        Compiled.Errors(
                source,
                "namespace Acme { public class Other { } }",
                "namespace MyApp.Other { public class Marker { } }")
            .Should().BeEmpty();
    }

    // R3-33. An explicit using is in scope whether the simplifier asked for it or not, so a
    // name it already offers cannot be produced by shortening another namespace's type.
    // What the file shows `Legacy` to hold is all this pass can know, and it is enough here.
    [TestMethod]
    public void NameOfferedByAnExplicitImport_StaysQualified()
    {
        var acmeWidget = SourceFile.InNamespace("Acme").Class("Widget");
        var file = SourceFile.InNamespace("MyApp").WithUsing("Legacy").SimplifyTypeNames();
        var host = file.Class("Host");
        host.DefineField("_legacy", "Legacy.Widget");
        host.DefineMethod("M").WithParameter(acmeWidget, "w");

        var source = file.ToString();

        source.Should().Contain("Acme.Widget w").And.NotContain("using Acme;");
        Compiled.Errors(
                source,
                "namespace Acme { public class Widget { } }",
                "namespace Legacy { public class Widget { } }")
            .Should().BeEmpty();
    }

    // The same shape with no collision still shortens: the rule blocks a name two
    // namespaces offer, not every file that has a using in it.
    [TestMethod]
    public void ExplicitImportWithoutACollision_DoesNotBlockSimplification()
    {
        var acmeWidget = SourceFile.InNamespace("Acme").Class("Widget");
        var file = SourceFile.InNamespace("MyApp").WithUsing("Legacy").SimplifyTypeNames();
        var host = file.Class("Host");
        host.DefineField("_legacy", "Legacy.Gadget");
        host.DefineMethod("M").WithParameter(acmeWidget, "w");

        file.ToString().Should().Contain("using Acme;").And.Contain("(Widget w)");
    }

    // R3-32. A generic's type arguments are descendants of its own qualified name, so
    // reading the original node threw away the simplification already applied to them —
    // leaving the argument qualified and its import unused.
    [TestMethod]
    public void SimplifiedTypeArgument_SurvivesItsContainersRewrite()
    {
        var file = SourceFile.InNamespace("MyApp").SimplifyTypeNames();
        file.Class("Repo").DefineField<List<StringBuilder>>("_items");

        var source = file.ToString();

        source.Should().Contain("List<StringBuilder> _items")
            .And.Contain("using System.Collections.Generic;")
            .And.Contain("using System.Text;");
        Compiled.MemberType(source, "Repo", "_items")
            .Should().Be("System.Collections.Generic.List<System.Text.StringBuilder>");
    }

    // R3-34. Split at the last dot, `MyApp.Outer.Inner` records `MyApp.Outer` as a
    // namespace, and the import for it names something that does not exist. The `+` marker
    // is the only thing in the string that can say which segment is a type.
    [TestMethod]
    public void EmitsAs_NestedMarker_QualifiesThroughTheDeclaringType()
    {
        var file = SourceFile.InNamespace("Other").SimplifyTypeNames();
        file.Class("Consumer").DefineProperty<NestedPlaceholder>("Held");

        var source = file.ToString();

        source.Should().Contain("using MyApp;").And.Contain("Outer.Inner Held");
        Compiled.Errors(source, "namespace MyApp { public class Outer { public class Inner { } } }")
            .Should().BeEmpty();
    }

    [TestMethod]
    public void EmitsAs_NestedMarker_EmitsTheFullPathWithoutSimplification()
    {
        var file = SourceFile.InNamespace("Other");
        file.Class("Consumer").DefineProperty<NestedPlaceholder>("Held");

        file.ToString().Should().Contain("MyApp.Outer.Inner Held");
    }

    // The dotted form still means "all namespace segments", which is what it always meant
    // and what every other placeholder in the suite relies on.
    [TestMethod]
    public void EmitsAs_DottedName_StillMeansNamespaceQualified()
    {
        var file = SourceFile.InNamespace("Other").SimplifyTypeNames();
        file.Class("Consumer").DefineProperty<DottedPlaceholder>("Held");

        file.ToString().Should().Contain("using MyApp.Outer;").And.Contain("Inner Held");
    }

    [DataTestMethod]
    [DataRow("MyApp.Outer+")]
    [DataRow("+Inner")]
    [DataRow("MyApp.Outer++Inner")]
    public void EmitsAs_MalformedNestingMarker_Throws(string emittedName)
    {
        var act = () => TypeNameBuilder.ForEmittedName(emittedName, "probe");

        act.Should().Throw<ArgumentException>().WithMessage("*nesting marker*");
    }

    [TestMethod]
    public void EmitsAs_NestedSegmentThatIsNotAnIdentifier_Throws()
    {
        var act = () => TypeNameBuilder.ForEmittedName("MyApp.Outer+Inner.Deeper", "probe");

        act.Should().Throw<ArgumentException>();
    }

    // R3-43. A reference to a type this file declares, in this file's own namespace, is the
    // one case shortening is always safe: it needs no import, and the short name binds to
    // the declaration by definition.
    [TestMethod]
    public void ReferenceToATypeDeclaredInTheSameFile_Shortens()
    {
        var file = SourceFile.InNamespace("MyApp").SimplifyTypeNames();
        var @base = file.Class("Base");
        file.Class("Derived").WithParent(@base);

        var source = file.ToString();

        source.Should().Contain("public class Derived : Base").And.NotContain("using MyApp;");
        Compiled.Errors(source).Should().BeEmpty();
    }

    // ...and a nested declaration of that name still blocks it, because inside the
    // declaring type the short name binds to the nested one instead.
    [TestMethod]
    public void NestedTypeOfTheSameName_StillBlocksSimplification()
    {
        var file = SourceFile.InNamespace("MyApp").SimplifyTypeNames();
        var host = file.Class("Host");
        host.DefineClass("List");
        host.DefineField<List<int>>("_items");

        file.ToString().Should().Contain("System.Collections.Generic.List<int> _items")
            .And.NotContain("using System.Collections.Generic;");
    }
}
