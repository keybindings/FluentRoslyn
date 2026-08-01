using FluentRoslyn.Builders;

namespace FluentRoslyn.UnitTests.Builders;

[TestClass]
public class DocCommentTests
{
    [TestMethod]
    public void ClassSummary_EmitsAboveDeclaration()
    {
        var cb = NewClass().WithSummary("Stores users.");

        cb.ToString().Should().Be(string.Join("\n",
            "namespace TestNamespace;",
            "/// <summary>",
            "/// Stores users.",
            "/// </summary>",
            "public class Repo",
            "{",
            "}"));
    }

    [TestMethod]
    public void MultiLineSummary_EmitsOneCommentLinePerLine()
    {
        var cb = NewClass().WithSummary("First line.\nSecond line.");

        cb.ToString().Should().Contain(string.Join("\n",
            "/// <summary>",
            "/// First line.",
            "/// Second line.",
            "/// </summary>"));
    }

    [TestMethod]
    public void Summary_EscapesXmlMarkup()
    {
        // Unescaped, this would emit a malformed doc comment.
        var cb = NewClass().WithSummary("Holds a List<T> of A & B.");

        cb.ToString().Should().Contain("/// Holds a List&lt;T&gt; of A &amp; B.")
            .And.NotContain("List<T>");
    }

    [TestMethod]
    public void MethodDocs_EmitSummaryParamAndReturns()
    {
        var mb = NewClass().DefineMethod<int>("Add")
            .WithParameter<int>("n")
            .WithSummary("Adds a number.")
            .WithParameterDoc("n", "The addend.")
            .WithReturnsDoc("The new total.")
            .AsExpressionBody("_count + n");

        mb.ToString().Should().Be(string.Join("\n",
            "/// <summary>",
            "/// Adds a number.",
            "/// </summary>",
            "/// <param name=\"n\">The addend.</param>",
            "/// <returns>The new total.</returns>",
            "public int Add(int n) => _count + n;"));
    }

    [TestMethod]
    public void ParamAttribute_KeepsIdiomaticSpacing()
    {
        // Structured doc trivia would be reformatted to `name = "n"` by NormalizeWhitespace.
        var mb = NewClass().DefineMethod("Do").WithParameter<int>("n").WithParameterDoc("n", "A number.");

        mb.ToString().Should().Contain("<param name=\"n\">").And.NotContain("name = ");
    }

    [TestMethod]
    public void FieldAndPropertyDocs_Emit()
    {
        var cb = NewClass();
        cb.DefineField<int>("_count").WithSummary("Cached count.");
        cb.DefineProperty<int>("Total").WithSummary("The running total.");

        var value = cb.ToString();
        value.Should().Contain("/// Cached count.").And.Contain("/// The running total.");
    }

    [TestMethod]
    public void ConstructorDocs_EmitSummaryAndParam()
    {
        var ctor = NewClass().DefineConstructor(AccessModifier.Public)
            .WithParameter<int>("seed")
            .WithSummary("Creates a repository.")
            .WithParameterDoc("seed", "The initial value.")
            .AddStatement("_count = seed;");

        ctor.ToString().Should().StartWith(string.Join("\n",
            "/// <summary>",
            "/// Creates a repository.",
            "/// </summary>",
            "/// <param name=\"seed\">The initial value.</param>",
            "public Repo(int seed)"));
    }

    [TestMethod]
    public void InterfaceDocs_EmitOnTypeAndMembers()
    {
        var i = NamespaceBuilder.Get("TestNamespace").Interface("IRepo").WithSummary("A repository.");
        i.DefineProperty<int>("Count").WithSummary("How many items.");
        i.DefineMethod<int>("Add").WithParameter<int>("n").WithSummary("Adds.").WithReturnsDoc("The total.");

        var value = i.ToString();
        value.Should().Contain("/// A repository.");
        value.Should().Contain("/// How many items.");
        value.Should().Contain("/// <returns>The total.</returns>");
    }

    [DataTestMethod]
    [DataRow("Point")]
    [DataRow("Colour")]
    public void RecordAndEnumSummaries_Emit(string name)
    {
        var record = NamespaceBuilder.Get("N").Record(name).WithSummary("A thing.").WithParameter<int>("X");
        var @enum = NamespaceBuilder.Get("N").Enum(name).WithSummary("A thing.").AddMember("A");

        record.ToString().Should().Contain("/// A thing.");
        @enum.ToString().Should().Contain("/// A thing.");
    }

    [TestMethod]
    public void StructSummary_Emits()
    {
        NamespaceBuilder.Get("N").Struct("Point").WithSummary("A point.")
            .ToString().Should().Contain("/// A point.");
    }

    [TestMethod]
    public void NoDocs_EmitsNoComment()
    {
        NewClass().ToString().Should().NotContain("///");
    }

    [TestMethod]
    public void ParameterDoc_InvalidName_Throws()
    {
        var mb = NewClass().DefineMethod("Do");

        var act = () => mb.WithParameterDoc("not valid", "text");

        act.Should().Throw<ArgumentException>();
    }

    private static ClassBuilder NewClass()
        => NamespaceBuilder.Get("TestNamespace").Class("Repo");
}
