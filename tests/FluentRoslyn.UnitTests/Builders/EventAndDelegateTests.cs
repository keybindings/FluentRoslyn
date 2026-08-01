using System;
using FluentRoslyn.Builders;

namespace FluentRoslyn.UnitTests.Builders;

[TestClass]
public class EventAndDelegateTests
{
    #region Events

    [TestMethod]
    public void DefineEvent_EmitsFieldLikeEvent()
    {
        var eb = NewClass().DefineEvent<EventHandler>("Changed");

        eb.ToString().Should().Be("public event System.EventHandler Changed;");
    }

    [TestMethod]
    public void Event_StaticAndAccessModifier()
    {
        var eb = NewClass().DefineEvent<Action>("Fired")
            .Static()
            .WithAccessModifier(AccessModifier.Internal);

        eb.ToString().Should().Be("internal static event System.Action Fired;");
    }

    [TestMethod]
    public void Event_SupportsSummaryAndAttributes()
    {
        var eb = NewClass().DefineEvent<EventHandler>("Changed")
            .WithSummary("Raised when it changes.")
            .WithAttribute("field: NonSerialized");

        eb.ToString().Should().Contain("/// Raised when it changes.")
            .And.Contain("[field: NonSerialized]");
    }

    [TestMethod]
    public void Event_RawHandlerTypeName_IsAccepted()
    {
        // For a delegate that is being generated alongside, so is not a CLR type.
        var eb = NewClass().DefineEvent("OnCall", "Callback");

        eb.ToString().Should().Be("public event Callback OnCall;");
    }

    [TestMethod]
    public void Events_EmitAfterConstructorsAndBeforeProperties()
    {
        var cb = NewClass();
        cb.DefineProperty<int>("X");
        cb.DefineEvent<EventHandler>("Changed");
        cb.DefineConstructor();

        var value = cb.ToString();
        value.IndexOf("Widget()", StringComparison.Ordinal)
            .Should().BeLessThan(value.IndexOf("event", StringComparison.Ordinal));
        value.IndexOf("event", StringComparison.Ordinal)
            .Should().BeLessThan(value.IndexOf("public int X", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Event_ComposesWithSimplifyTypeNames()
    {
        var cb = NewClass().SimplifyTypeNames();
        cb.DefineEvent<EventHandler>("Changed");

        cb.ToString().Should().Contain("using System;").And.Contain("public event EventHandler Changed;");
    }

    #endregion

    #region Delegates

    [TestMethod]
    public void Delegate_VoidReturn_Emits()
    {
        var db = NamespaceBuilder.Get("N").Delegate("Handler").WithParameter<int>("x");

        db.ToString().Should().Be(string.Join("\n",
            "namespace N;",
            "public delegate void Handler(int x);"));
    }

    [TestMethod]
    public void Delegate_TypedReturn_Emits()
    {
        var db = NamespaceBuilder.Get("N").Delegate<int>("Calc").WithParameter<int>("a");

        db.ToString().Should().Contain("public delegate int Calc(int a);");
    }

    [TestMethod]
    public void Delegate_Generic_WithConstraint()
    {
        var db = NamespaceBuilder.Get("N").Delegate("Factory")
            .Returns("T")
            .WithTypeParameter("T")
            .WithConstraint("T", "new()");

        db.ToString().Should().Contain("public delegate T Factory<T>()")
            .And.Contain("where T : new()");
    }

    [TestMethod]
    public void Delegate_SupportsSummaryAttributeAndAccess()
    {
        var db = NamespaceBuilder.Get("N").Delegate("Handler")
            .WithAccessModifier(AccessModifier.Internal)
            .WithSummary("Handles a thing.")
            .WithAttribute("Obsolete");

        db.ToString().Should().Contain("/// Handles a thing.")
            .And.Contain("[Obsolete]")
            .And.Contain("internal delegate void Handler();");
    }

    [TestMethod]
    public void NestedDelegate_EmitsInsideType()
    {
        var cb = NewClass();
        cb.DefineDelegate("Callback").WithParameter<string>("message");

        cb.ToString().Should().Contain("public delegate void Callback(string message);");
    }

    [TestMethod]
    public void NestedDelegate_CanBackAnEventOnTheSameType()
    {
        var cb = NewClass();
        cb.DefineDelegate("Callback").WithParameter<string>("message");
        cb.DefineEvent("OnCall", "Callback");

        var value = cb.ToString();
        value.Should().Contain("public event Callback OnCall;");
        value.Should().Contain("public delegate void Callback(string message);");
    }

    [TestMethod]
    public void Delegate_ConstraintWithoutTypeParameter_Throws()
    {
        var db = NamespaceBuilder.Get("N").Delegate("Handler").WithConstraint("T", "class");

        var act = () => db.ToString();

        act.Should().Throw<InvalidOperationException>().WithMessage("*no type parameters*");
    }

    #endregion

    private static ClassBuilder NewClass()
        => NamespaceBuilder.Get("N").Class("Widget");
}
