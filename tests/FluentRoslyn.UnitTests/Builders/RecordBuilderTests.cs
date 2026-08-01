using FluentRoslyn.Builders;

namespace FluentRoslyn.UnitTests.Builders;

[TestClass]
public class RecordBuilderTests
{
    [TestMethod]
    public void PositionalRecord_EmitsSingleLineWithSemicolon()
    {
        var r = NamespaceBuilder.Get("TestNamespace").Record("Person")
            .WithParameter<string>("Name")
            .WithParameter<int>("Age");

        r.ToString().Should().Be(string.Join("\n",
            "namespace TestNamespace;",
            "public record Person(string Name, int Age);"));
    }

    [TestMethod]
    public void Record_NoParameters_EmitsEmptyParameterList()
    {
        var r = NamespaceBuilder.Get("TestNamespace").Record("Marker");

        r.ToString().Should().Contain("public record Marker();");
    }

    [TestMethod]
    public void AsStruct_EmitsRecordStruct()
    {
        var r = NamespaceBuilder.Get("TestNamespace").Record("Point")
            .AsStruct()
            .WithParameter<int>("X")
            .WithParameter<int>("Y");

        r.ToString().Should().Contain("public record struct Point(int X, int Y);");
    }

    [TestMethod]
    public void WithAccessModifier_OverridesAccess()
    {
        var r = NamespaceBuilder.Get("TestNamespace").Record("Person")
            .WithAccessModifier(AccessModifier.Internal)
            .WithParameter<string>("Name");

        r.ToString().Should().Contain("internal record Person(string Name);");
    }

    [TestMethod]
    public void WithAttribute_EmitsAboveRecord()
    {
        var r = NamespaceBuilder.Get("TestNamespace").Record("Person")
            .WithAttribute("Serializable")
            .WithParameter<string>("Name");

        r.ToString().Should().Be(string.Join("\n",
            "namespace TestNamespace;",
            "[Serializable]",
            "public record Person(string Name);"));
    }

    [TestMethod]
    public void BlockScopedNamespace_WrapsRecordInBraces()
    {
        var r = NamespaceBuilder.Get("TestNamespace").Record("Person")
            .BlockScopedNamespace()
            .WithParameter<string>("Name");

        r.ToString().Should().Be(string.Join("\n",
            "namespace TestNamespace",
            "{",
            "    public record Person(string Name);",
            "}"));
    }

    [TestMethod]
    public void GlobalNamespace_EmitsRecordWithoutNamespace()
    {
        var r = NamespaceBuilder.None.Record("Person").WithParameter<string>("Name");

        r.ToString().Should().Be("public record Person(string Name);");
    }

    [TestMethod]
    public void FluentMethods_MutateInPlace_ReturningTheSameInstance()
    {
        var r = NamespaceBuilder.Get("TestNamespace").Record("Person");

        r.AsStruct().Should().BeSameAs(r);
    }
}
