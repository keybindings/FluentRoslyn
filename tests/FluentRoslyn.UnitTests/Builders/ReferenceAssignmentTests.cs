using System;
using FluentRoslyn.Abstractions;
using FluentRoslyn.Builders;

namespace FluentRoslyn.UnitTests.Builders;

/// <summary>
/// Covers typed references and <c>Assign</c>. The type matching itself is a compile-time
/// property — <c>Assign(stringProperty, intParameter)</c> does not compile, so it cannot
/// be asserted here; what these tests pin is the emission and the shadowing rules.
/// </summary>
[TestClass]
public class ReferenceAssignmentTests
{
    [TestMethod]
    public void Assign_PropertiesFromParameters_EmitsConstructorBody()
    {
        var user = NamespaceBuilder.Get("MyApp.Models").Class("User");
        var id = user.DefineProperty<int>("Id").GetOnly();
        var name = user.DefineProperty<string>("Name");

        user.DefineConstructor(AccessModifier.Public)
            .WithParameter<int>("id", out var idParam)
            .WithParameter<string>("name", out var nameParam)
            .Assign(id, idParam)
            .Assign(name, nameParam);

        user.ToString().Should().Be(string.Join("\n",
            "namespace MyApp.Models;",
            "public class User",
            "{",
            "    public User(int id, string name)",
            "    {",
            "        Id = id;",
            "        Name = name;",
            "    }",
            "",
            "    public int Id { get; }",
            "    public string Name { get; set; }",
            "}"));
    }

    [TestMethod]
    public void Assign_FieldFromParameter_Emits()
    {
        var widget = NamespaceBuilder.Get("MyApp").Class("Widget");
        var field = widget.DefineField<int>("_id");

        widget.DefineConstructor(AccessModifier.Public)
            .WithParameter<int>("id", out var idParam)
            .Assign(field, idParam);

        widget.ToString().Should().Contain("_id = id;");
    }

    [TestMethod]
    public void Assign_InMethodBody_Emits()
    {
        var widget = NamespaceBuilder.Get("MyApp").Class("Widget");
        var label = widget.DefineProperty<string>("Label");

        widget.DefineMethod("SetLabel")
            .WithParameter<string>("label", out var labelParam)
            .Assign(label, labelParam);

        widget.ToString().Should().Contain("Label = label;");
    }

    // A bare `value = value;` would assign the parameter to itself: source that compiles
    // and is silently wrong. `this.` is the only way to reach the shadowed member.
    [TestMethod]
    public void Assign_ParameterShadowsMember_QualifiesWithThis()
    {
        var shadow = NamespaceBuilder.Get("MyApp").Class("Shadow");
        var value = shadow.DefineProperty<string>("value");

        shadow.DefineConstructor(AccessModifier.Public)
            .WithParameter<string>("value", out var valueParam)
            .Assign(value, valueParam);

        shadow.ToString().Should().Contain("this.value = value;");
    }

    [TestMethod]
    public void Assign_ShadowedStaticMember_Throws()
    {
        var statics = NamespaceBuilder.Get("MyApp").Class("Statics");
        var count = statics.DefineProperty<int>("count").Static();

        var assign = () => statics.DefineConstructor(AccessModifier.Public)
            .WithParameter<int>("count", out var countParam)
            .Assign(count, countParam);

        assign.Should().Throw<InvalidOperationException>()
            .WithMessage("*shadows the member being assigned*");
    }

    [TestMethod]
    public void Assign_ShadowedMemberFromStaticMethod_Throws()
    {
        var statics = NamespaceBuilder.Get("MyApp").Class("Statics");
        var total = statics.DefineProperty<int>("total").Static();

        var assign = () => statics.DefineMethod("Set").Static()
            .WithParameter<int>("total", out var totalParam)
            .Assign(total, totalParam);

        assign.Should().Throw<InvalidOperationException>()
            .WithMessage("*shadows the member being assigned*");
    }

    // Assigning one parameter to another needs no qualification: neither shadows a member.
    [TestMethod]
    public void Assign_ParameterToParameter_EmitsUnqualified()
    {
        var widget = NamespaceBuilder.Get("MyApp").Class("Widget");

        widget.DefineMethod("Swap")
            .WithParameter<int>("a", out var a)
            .WithParameter<int>("b", out var b)
            .Assign(a, b);

        widget.ToString().Should().Contain("a = b;");
    }

    [TestMethod]
    public void WithParameter_OutOverload_StillAppendsTheParameter()
    {
        var ctor = NamespaceBuilder.Get("MyApp").Class("Widget")
            .DefineConstructor(AccessModifier.Public)
            .WithParameter<int>("id", out _)
            .WithParameter<string>("name", out _);

        ctor.ToString().Should().StartWith("public Widget(int id, string name)");
    }

    [TestMethod]
    public void WithParameter_OutOverload_ValidatesTheName()
    {
        var ctor = NamespaceBuilder.Get("MyApp").Class("Widget").DefineConstructor();

        var invalid = () => ctor.WithParameter<int>("1nvalid", out _);

        invalid.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void Reference_ExposesTheEmittedName()
    {
        var widget = NamespaceBuilder.Get("MyApp").Class("Widget");
        IReference<string> name = widget.DefineProperty<string>("Name");

        widget.DefineConstructor().WithParameter<string>("name", out var nameParam);

        name.Name.Should().Be("Name");
        nameParam.Name.Should().Be("name");
    }

    [TestMethod]
    public void Assign_NullReference_Throws()
    {
        var widget = NamespaceBuilder.Get("MyApp").Class("Widget");
        var name = widget.DefineProperty<string>("Name");
        var ctor = widget.DefineConstructor();

        var nullTarget = () => ctor.Assign<string>(null!, name);
        var nullValue = () => ctor.Assign<string>(name, null!);

        nullTarget.Should().Throw<ArgumentNullException>();
        nullValue.Should().Throw<ArgumentNullException>();
    }
}
