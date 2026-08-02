using System;
using FluentRoslyn.Builders;

namespace FluentRoslyn.UnitTests.Builders;

/// <summary>
/// Covers typed accessor bodies — <c>WithGetter</c> / <c>WithSetter</c> — which give
/// property accessors the same statement API as method bodies, plus a <c>Return</c> and
/// a <c>Value</c> typed to the property.
/// </summary>
[TestClass]
public class AccessorBodyTests
{
    [TestMethod]
    public void WithGetterAndSetter_EmitsBackingFieldProperty()
    {
        var widget = NamespaceBuilder.Get("MyApp").Class("Widget");
        var backing = widget.DefineField<string>("_name");
        widget.DefineProperty<string>("Name")
            .WithGetter(g => g.Return(backing))
            .WithSetter(s => s.Assign(backing, s.Value));

        widget.ToString().Should().Be(string.Join("\n",
            "namespace MyApp;",
            "public class Widget",
            "{",
            "    private string _name;",
            "    public string Name",
            "    {",
            "        get",
            "        {",
            "            return _name;",
            "        }",
            "",
            "        set",
            "        {",
            "            _name = value;",
            "        }",
            "    }",
            "}"));
    }

    // The whole shared statement surface is available inside an accessor, not just
    // assignment — this is what routing accessors through StatementBuilder bought.
    [TestMethod]
    public void Setter_CanGuardItsIncomingValue()
    {
        var guarded = NamespaceBuilder.Get("MyApp").Class("Guarded");
        var text = guarded.DefineField<string>("_text");
        guarded.DefineProperty<string>("Text")
            .WithSetter(s => s.ThrowIfNull(s.Value).Assign(text, s.Value));

        guarded.ToString().Should()
            .Contain("if (value is null)")
            .And.Contain("throw new System.ArgumentNullException(nameof(value));")
            .And.Contain("_text = value;");
    }

    // `value` is a real name in a setter's scope, so a member of the same name is
    // shadowed. Without qualification this would emit `value = value;` — legal C# that
    // silently does nothing.
    [TestMethod]
    public void Setter_MemberNamedValue_QualifiesWithThis()
    {
        var sh = NamespaceBuilder.Get("MyApp").Class("Sh");
        var field = sh.DefineField<string>("value");
        sh.DefineProperty<string>("Thing").WithSetter(s => s.Assign(field, s.Value));

        sh.ToString().Should().Contain("this.value = value;");
    }

    [TestMethod]
    public void Getter_ReturnLiteral_Emits()
    {
        var l = NamespaceBuilder.Get("MyApp").Class("L");
        l.DefineProperty<int>("Answer").WithGetter(g => g.ReturnLiteral(42));

        l.ToString().Should().Contain("return 42;");
    }

    [TestMethod]
    public void WithGetter_MakesThePropertyNonAuto()
    {
        var c = NamespaceBuilder.Get("MyApp").Class("C");
        var f = c.DefineField<int>("_n");
        c.DefineProperty<int>("N").WithGetter(g => g.Return(f));

        // An auto-property would emit `{ get; set; }`; this one has a real body.
        c.ToString().Should().Contain("get").And.Contain("return _n;")
            .And.NotContain("{ get; set; }");
    }

    [TestMethod]
    public void WithGetter_NullCallback_Throws()
    {
        var prop = NamespaceBuilder.Get("MyApp").Class("C").DefineProperty<int>("N");

        var configure = () => prop.WithGetter(null!);

        configure.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void WithSetter_NullCallback_Throws()
    {
        var prop = NamespaceBuilder.Get("MyApp").Class("C").DefineProperty<int>("N");

        var configure = () => prop.WithSetter(null!);

        configure.Should().Throw<ArgumentNullException>();
    }
}
