using FluentRoslyn.Builders;

namespace FluentRoslyn.UnitTests.Builders;

/// <summary>
/// Covers <c>ThrowIfNull</c>. The reference-type constraint is a compile-time property —
/// <c>ThrowIfNull(intParameter)</c> does not compile — so these pin the emission.
/// </summary>
[TestClass]
public class NullGuardTests
{
    [TestMethod]
    public void ThrowIfNull_EmitsTheClassicGuard()
    {
        var user = NamespaceBuilder.Get("MyApp").Class("User");
        var name = user.DefineProperty<string>("Name");

        user.DefineConstructor(AccessModifier.Public)
            .WithParameter<string>("name", out var nameParam)
            .ThrowIfNull(nameParam)
            .Assign(name, nameParam);

        user.ToString().Should().Be(string.Join("\n",
            "namespace MyApp;",
            "public class User",
            "{",
            "    public User(string name)",
            "    {",
            "        if (name is null)",
            "            throw new System.ArgumentNullException(nameof(name));",
            "        Name = name;",
            "    }",
            "",
            "    public string Name { get; set; }",
            "}"));
    }

    // The exception goes through TypeNameBuilder rather than a raw string, so it takes
    // part in simplification like any other type reference.
    [TestMethod]
    public void ThrowIfNull_UnderSimplifyTypeNames_ShortensAndImports()
    {
        var s = NamespaceBuilder.Get("MyApp").Class("S").SimplifyTypeNames();
        s.DefineMethod("Check").WithParameter<string>("text", out var text).ThrowIfNull(text);

        s.ToString().Should().StartWith("using System;")
            .And.Contain("throw new ArgumentNullException(nameof(text));");
    }

    [TestMethod]
    public void ThrowIfNull_ShadowedMember_QualifiesBothPositions()
    {
        var sh = NamespaceBuilder.Get("MyApp").Class("Sh");
        var value = sh.DefineProperty<string>("value");
        sh.DefineMethod("Go").WithParameter<int>("value").ThrowIfNull(value);

        // nameof(this.value) is legal C# and still yields "value".
        sh.ToString().Should().Contain("if (this.value is null)")
            .And.Contain("nameof(this.value)");
    }

    [TestMethod]
    public void ThrowIfNull_WorksOnMethodsAndConstructorsAlike()
    {
        var t = NamespaceBuilder.Get("MyApp").Class("T");
        t.DefineConstructor().WithParameter<string>("a", out var a).ThrowIfNull(a);
        t.DefineMethod("M").WithParameter<string>("b", out var b).ThrowIfNull(b);

        t.ToString().Should().Contain("nameof(a)").And.Contain("nameof(b)");
    }
}
