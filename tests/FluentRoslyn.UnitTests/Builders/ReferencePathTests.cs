using System;
using System.Collections.Generic;
using FluentRoslyn.Abstractions;
using FluentRoslyn.Builders;

namespace FluentRoslyn.UnitTests.Builders;

/// <summary>
/// Covers references built from other references — <c>Config.Label</c>, <c>_items[0]</c>,
/// <c>_map[key]</c>. As with <see cref="ReferenceAssignmentTests"/>, the type matching is
/// a compile-time property and cannot be asserted here; what these pin is the emission,
/// the shadowing rule (the root qualifies, nothing after it does), and the guards.
/// </summary>
[TestClass]
public class ReferencePathTests
{
    [TestMethod]
    public void MemberNamed_AsAssignmentTarget_EmitsThePath()
    {
        var widget = NamespaceBuilder.Get("MyApp").Class("Widget");
        var config = widget.DefineField<Uri>("_config");

        widget.DefineMethod("Configure")
            .WithParameter<string>("host", out var hostParam)
            .Assign(config.MemberNamed<string>("Host"), hostParam);

        widget.ToString().Should().Contain("_config.Host = host;");
    }

    // The checked form: the name and the type both come from the member's own definition,
    // so neither can drift from it.
    [TestMethod]
    public void Member_FromAMemberHandle_TakesItsNameAndType()
    {
        var file = SourceFile.InNamespace("MyApp");
        var inner = file.Class("Inner");
        var label = inner.DefineProperty<string>("Label");

        var widget = file.Class("Widget");
        var current = widget.DefineField<Uri>("_current");

        widget.DefineMethod("Configure")
            .WithParameter<string>("text", out var textParam)
            .Assign(current.Member(label), textParam);

        file.ToString().Should().Contain("_current.Label = text;");
    }

    [TestMethod]
    public void Member_Chained_EmitsTheWholePath()
    {
        var widget = NamespaceBuilder.Get("MyApp").Class("Widget");
        var config = widget.DefineField<Uri>("_config");

        widget.DefineMethod("Configure")
            .WithParameter<string>("text", out var textParam)
            .Assign(config.MemberNamed<Uri>("Inner").MemberNamed<string>("Label"), textParam);

        widget.ToString().Should().Contain("_config.Inner.Label = text;");
    }

    [TestMethod]
    public void Item_OnAnArray_EmitsElementAccess()
    {
        var widget = NamespaceBuilder.Get("MyApp").Class("Widget");
        var items = widget.DefineField<string[]>("_items");

        widget.DefineMethod("Set")
            .WithParameter<string>("text", out var textParam)
            .WithParameter<int>("index", out var indexParam)
            .Assign(items.Item(0), textParam)
            .Assign(items.Item(indexParam), textParam);

        widget.ToString().Should()
            .Contain("_items[0] = text;").And
            .Contain("_items[index] = text;");
    }

    [TestMethod]
    public void Item_OnAList_EmitsElementAccess()
    {
        var widget = NamespaceBuilder.Get("MyApp").Class("Widget");
        var numbers = widget.DefineProperty<List<int>>("Numbers");

        widget.DefineMethod("Set")
            .WithParameter<int>("value", out var valueParam)
            .WithParameter<int>("index", out var indexParam)
            .Assign(numbers.Item(2), valueParam)
            .Assign(numbers.Item(indexParam), valueParam);

        widget.ToString().Should()
            .Contain("Numbers[2] = value;").And
            .Contain("Numbers[index] = value;");
    }

    [TestMethod]
    public void Item_OnADictionary_EmitsKeyedAccess()
    {
        var widget = NamespaceBuilder.Get("MyApp").Class("Widget");
        var map = widget.DefineField<Dictionary<string, string>>("_map");

        widget.DefineMethod("Set")
            .WithParameter<string>("text", out var textParam)
            .WithParameter<string>("key", out var keyParam)
            .Assign(map.Item("name"), textParam)
            .Assign(map.Item(keyParam), textParam);

        widget.ToString().Should()
            .Contain("_map[\"name\"] = text;").And
            .Contain("_map[key] = text;");
    }

    // A path is a reference, so every position that already takes one accepts it with no
    // new overload. That is the whole point of extending references rather than adding an
    // expression model.
    [TestMethod]
    public void Path_WorksInEveryReferencePosition()
    {
        var file = SourceFile.InNamespace("MyApp");
        var inner = file.Class("Inner");
        inner.DefineMethod("Apply").WithParameter<string>("text", out _).AsCallable<string>(out var apply);

        var widget = file.Class("Widget");
        var config = widget.DefineField<Uri>("_config");
        var label = widget.DefineProperty<string>("Label");

        widget.DefineMethod<string>("Describe")
            .Assign(label, config.MemberNamed<string>("Host"))
            .Call(config.MemberNamed<Uri>("Inner"), apply, config.MemberNamed<string>("Host"))
            .Return(config.MemberNamed<string>("Host"));

        var code = widget.ToString();

        code.Should()
            .Contain("Label = _config.Host;").And
            .Contain("_config.Inner.Apply(_config.Host);").And
            .Contain("return _config.Host;");
    }

    [TestMethod]
    public void Path_TakesLiteralAndCompoundAssignment()
    {
        var widget = NamespaceBuilder.Get("MyApp").Class("Widget");
        var counts = widget.DefineField<int[]>("_counts");

        widget.DefineMethod("Bump")
            .WithParameter<int>("delta", out var deltaParam)
            .AssignLiteral(counts.Item(0), 1)
            .Assign(counts.Item(1), AssignmentOperator.Add, deltaParam);

        widget.ToString().Should()
            .Contain("_counts[0] = 1;").And
            .Contain("_counts[1] += delta;");
    }

    [TestMethod]
    public void Path_InASetterBody_Emits()
    {
        var widget = NamespaceBuilder.Get("MyApp").Class("Widget");
        var config = widget.DefineField<Uri>("_config");

        widget.DefineProperty<string>("Host")
            .WithGetter(g => g.Return(config.MemberNamed<string>("Host")))
            .WithSetter(s => s.Assign(config.MemberNamed<string>("Host"), s.Value));

        widget.ToString().Should()
            .Contain("return _config.Host;").And
            .Contain("_config.Host = value;");
    }

    // Only the leading name can be shadowed by a parameter: everything after the first
    // dot binds in the target's type, so qualifying it would be wrong.
    [TestMethod]
    public void Path_WhoseRootIsShadowed_QualifiesTheRootOnly()
    {
        var shadow = NamespaceBuilder.Get("MyApp").Class("Shadow");
        var config = shadow.DefineField<Uri>("config");

        shadow.DefineMethod("Set")
            .WithParameter<string>("config", out var configParam)
            .Assign(config.MemberNamed<string>("Host"), configParam);

        shadow.ToString().Should().Contain("this.config.Host = config;");
    }

    [TestMethod]
    public void Path_IndexReference_IsQualifiedLikeAnyOther()
    {
        var shadow = NamespaceBuilder.Get("MyApp").Class("Shadow");
        var items = shadow.DefineField<string[]>("_items");
        var index = shadow.DefineProperty<int>("index");

        shadow.DefineMethod("Set")
            .WithParameter<string>("text", out var textParam)
            .WithParameter<int>("index", out _)
            .Assign(items.Item(index), textParam);

        shadow.ToString().Should().Contain("_items[this.index] = text;");
    }

    [TestMethod]
    public void Path_RootedOnAShadowedStaticMember_Throws()
    {
        var statics = NamespaceBuilder.Get("MyApp").Class("Statics");
        var config = statics.DefineField<Uri>("config").Static();

        var assign = () => statics.DefineMethod("Set")
            .WithParameter<string>("config", out var configParam)
            .Assign(config.MemberNamed<string>("Host"), configParam);

        assign.Should().Throw<InvalidOperationException>()
            .WithMessage("*shadows the member being referenced*");
    }

    [TestMethod]
    public void ThrowIfNull_OnAMemberPath_GuardsIt()
    {
        var widget = NamespaceBuilder.Get("MyApp").Class("Widget");
        var config = widget.DefineField<Uri>("_config");

        widget.DefineMethod("Check").ThrowIfNull(config.MemberNamed<string>("Host"));

        widget.ToString().Should()
            .Contain("if (_config.Host is null)").And
            .Contain("nameof(_config.Host)");
    }

    // Measured: `nameof(items[0])` is CS8081 and `nameof(items[0].Length)` is CS8082, so
    // the guard cannot be emitted for an element access at any position in the chain.
    // Refusing beats emitting source the consumer's build rejects.
    [TestMethod]
    public void ThrowIfNull_OnAnElementPath_Throws()
    {
        var widget = NamespaceBuilder.Get("MyApp").Class("Widget");
        var items = widget.DefineField<string[]>("_items");

        var guard = () => widget.DefineMethod("Check").ThrowIfNull(items.Item(0));

        guard.Should().Throw<InvalidOperationException>().WithMessage("*nameof*");
    }

    [TestMethod]
    public void ThrowIfNull_OnAMemberOfAnElement_AlsoThrows()
    {
        var widget = NamespaceBuilder.Get("MyApp").Class("Widget");
        var configs = widget.DefineField<Uri[]>("_configs");

        var guard = () => widget.DefineMethod("Check")
            .ThrowIfNull(configs.Item(0).MemberNamed<string>("Host"));

        guard.Should().Throw<InvalidOperationException>().WithMessage("*nameof*");
    }

    [TestMethod]
    public void Path_ReportsTheWholeAccessAsItsName()
    {
        var widget = NamespaceBuilder.Get("MyApp").Class("Widget");
        var config = widget.DefineField<Uri>("_config");
        var items = widget.DefineField<string[]>("_items");
        var map = widget.DefineField<Dictionary<string, string>>("_map");

        IReference<string> member = config.MemberNamed<string>("Host");
        IReference<string> element = items.Item(3);
        IReference<string> keyed = map.Item("name");

        member.Name.Should().Be("_config.Host");
        element.Name.Should().Be("_items[3]");
        keyed.Name.Should().Be("_map[name]");
    }

    [TestMethod]
    public void MemberNamed_WithAnInvalidIdentifier_Throws()
    {
        var widget = NamespaceBuilder.Get("MyApp").Class("Widget");
        var config = widget.DefineField<Uri>("_config");

        var invalid = () => config.MemberNamed<string>("1nvalid");

        invalid.Should().Throw<ArgumentException>();
    }

    // A negative constant index is always a bug, and emitting it would need a unary minus
    // rather than a literal token.
    [TestMethod]
    public void Item_WithANegativeIndex_Throws()
    {
        var widget = NamespaceBuilder.Get("MyApp").Class("Widget");
        var items = widget.DefineField<string[]>("_items");

        var negative = () => items.Item(-1);

        negative.Should().Throw<ArgumentOutOfRangeException>();
    }

    [TestMethod]
    public void Path_WithANullTargetOrMember_Throws()
    {
        var widget = NamespaceBuilder.Get("MyApp").Class("Widget");
        var config = widget.DefineField<Uri>("_config");
        var items = widget.DefineField<string[]>("_items");

        var nullTarget = () => ((IReference)null!).MemberNamed<string>("Host");
        var nullMember = () => config.Member<string>(null!);
        var nullIndex = () => items.Item((IReference<int>)null!);

        nullTarget.Should().Throw<ArgumentNullException>();
        nullMember.Should().Throw<ArgumentNullException>();
        nullIndex.Should().Throw<ArgumentNullException>();
    }
}
