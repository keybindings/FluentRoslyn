using System;
using FluentRoslyn.Abstractions;
using FluentRoslyn.Builders;

namespace FluentRoslyn.UnitTests.Builders;

[EmitsAs("MyApp.Repo")]
internal sealed class RepoPlaceholder;

/// <summary>
/// Type identity and the generic guard — R3-17 through R3-21. Each was the same rule
/// implemented twice, once correctly through <c>TypeNameBuilder.For</c> and once not, so
/// these cover every route to the wrong copy rather than the one the review happened to
/// name.
/// </summary>
[TestClass]
public class TypeIdentityTests
{
    // R3-17. WithParent called BuildTypeSyntax directly, so it never met the guard that
    // WithInterface met -- and `class IntBox : Container` is CS0305 in the consumer's build.
    [TestMethod]
    public void ClassWithParent_OfAGenericType_Throws()
    {
        var container = NamespaceBuilder.Get("MyApp").Class("Container").WithTypeParameter("T");
        var box = NamespaceBuilder.Get("MyApp").Class("IntBox").WithParent(container);

        var emit = () => box.ToString();

        emit.Should().Throw<InvalidOperationException>().WithMessage("*declares type parameters*");
    }

    [TestMethod]
    public void RecordWithParent_OfAGenericRecord_Throws()
    {
        var @base = NamespaceBuilder.Get("MyApp").Record("Envelope").WithTypeParameter("T");
        var derived = NamespaceBuilder.Get("MyApp").Record("IntEnvelope").WithParent(@base);

        var emit = () => derived.ToString();

        emit.Should().Throw<InvalidOperationException>().WithMessage("*declares type parameters*");
    }

    // ...and lazily, so adding the type parameter after the reference is taken is caught
    // too. The record path resolved its base eagerly and would have missed this.
    [TestMethod]
    public void RecordWithParent_GenericAddedAfterwards_StillThrows()
    {
        var @base = NamespaceBuilder.Get("MyApp").Record("Envelope");
        var derived = NamespaceBuilder.Get("MyApp").Record("IntEnvelope").WithParent(@base);
        @base.WithTypeParameter("T");

        var emit = () => derived.ToString();

        emit.Should().Throw<InvalidOperationException>().WithMessage("*declares type parameters*");
    }

    // R3-18. The guard checked the leaf builder only, so a type nested in a generic type
    // emitted `Outer.Inner` with the outer's arguments dropped -- also CS0305.
    [TestMethod]
    public void ReferenceToATypeNestedInAGenericType_Throws()
    {
        var outer = NamespaceBuilder.Get("MyApp").Class("Outer").WithTypeParameter("T");
        var inner = outer.DefineClass("Inner");
        var consumer = NamespaceBuilder.Get("MyApp").Class("Consumer");
        consumer.DefineMethod("Take").WithParameter(inner, "value");

        var emit = () => consumer.ToString();

        emit.Should().Throw<InvalidOperationException>()
            .WithMessage("*nested in 'Outer', which declares type parameters*");
    }

    [TestMethod]
    public void EveryReferencePositionMeetsTheGenericGuard()
    {
        var outer = NamespaceBuilder.Get("MyApp").Class("Outer").WithTypeParameter("T");
        var inner = outer.DefineClass("Inner");
        var iface = NamespaceBuilder.Get("MyApp").Interface("IThing").WithTypeParameter("T");

        var consumer = NamespaceBuilder.Get("MyApp").Class("Consumer");

        ((Action)(() => consumer.WithInterface(iface).ToString()))
            .Should().Throw<InvalidOperationException>().WithMessage("*declares type parameters*");

        ((Action)(() => NamespaceBuilder.Get("MyApp").Class("C2").DefineMethod("M").Returns(inner).ToString()))
            .Should().Throw<InvalidOperationException>().WithMessage("*declares type parameters*");

        ((Action)(() => NamespaceBuilder.Get("MyApp").Class("C3")
                .DefineMethod("M").CallStatic(inner, "Make").ToString()))
            .Should().Throw<InvalidOperationException>().WithMessage("*declares type parameters*");
    }

    // R3-19. The pairing compared against a name that silently dropped the type
    // parameters, so a generic declaring type sailed through and the call emitted CS0305.
    [TestMethod]
    public void AsCallableOn_AGenericDeclaringType_Throws()
    {
        var repo = NamespaceBuilder.Get("MyApp").Class("Repo").WithTypeParameter("T");

        var take = () => repo.DefineMethod("Reset").AsCallableOn<RepoPlaceholder>(out _);

        take.Should().Throw<InvalidOperationException>().WithMessage("*declares type parameters*");
    }

    [TestMethod]
    public void AsConstructable_OnAGenericDeclaringType_Throws()
    {
        var repo = NamespaceBuilder.Get("MyApp").Class("Repo").WithTypeParameter("T");

        var take = () => repo.DefineConstructor(AccessModifier.Public).AsConstructable<RepoPlaceholder>(out _);

        take.Should().Throw<InvalidOperationException>().WithMessage("*declares type parameters*");
    }

    [TestMethod]
    public void This_OnAGenericType_Throws()
    {
        var repo = NamespaceBuilder.Get("MyApp").Class("Repo").WithTypeParameter("T");

        var take = () => repo.This<RepoPlaceholder>();

        take.Should().Throw<InvalidOperationException>().WithMessage("*declares type parameters*");
    }

    // R3-20. A call through a handle emits no type-argument list, and the handle carries
    // argument types rather than type arguments -- so a generic method is CS0411.
    [TestMethod]
    public void AsCallable_OnAGenericMethod_Throws()
    {
        var repo = NamespaceBuilder.Get("MyApp").Class("Repo");

        var take = () => repo.DefineMethod("Load").WithTypeParameter("T").AsCallable(out _);

        take.Should().Throw<InvalidOperationException>()
            .WithMessage("*declares type parameters, so a handle cannot name it*");
    }

    // ...and the freeze covered parameters only, so the type parameter could simply be
    // added afterwards.
    [TestMethod]
    public void TypeParameterAddedAfterAHandle_Throws()
    {
        var repo = NamespaceBuilder.Get("MyApp").Class("Repo");
        var load = repo.DefineMethod("Load");
        load.AsCallable(out _);

        var widen = () => load.WithTypeParameter("T");

        widen.Should().Throw<InvalidOperationException>()
            .WithMessage("*has issued a handle, so its type parameters cannot change*");
    }

    [TestMethod]
    public void ConstraintAddedAfterAHandle_Throws()
    {
        var repo = NamespaceBuilder.Get("MyApp").Class("Repo");
        var load = repo.DefineMethod("Load");
        load.AsCallable(out _);

        var constrain = () => load.WithConstraint("T", "class");

        constrain.Should().Throw<InvalidOperationException>().WithMessage("*has issued a handle*");
    }

    // R3-21. A handle is carried to another builder and called there; the library does not
    // track where, so it can only issue one for a member reachable from anywhere.
    [TestMethod]
    public void AsCallable_OnAPrivateMethod_Throws()
    {
        var repo = NamespaceBuilder.Get("MyApp").Class("Repo");

        var take = () => repo.DefineMethod("Helper", AccessModifier.Private).AsCallable(out _);

        take.Should().Throw<InvalidOperationException>().WithMessage("*a handle cannot be issued*");
    }

    [TestMethod]
    public void AsConstructable_OnAPrivateConstructor_Throws()
    {
        var widget = NamespaceBuilder.Get("MyApp").Class("Widget");

        var take = () => widget.DefineConstructor(AccessModifier.Private).AsConstructable<WidgetValuePh>(out _);

        take.Should().Throw<InvalidOperationException>().WithMessage("*a handle cannot be issued*");
    }

    [DataTestMethod]
    [DataRow("Protected")]
    [DataRow("PrivateProtected")]
    [DataRow("None")]
    public void AsCallable_OnAMemberReachableOnlyFromSomewhere_Throws(string modifier)
    {
        var repo = NamespaceBuilder.Get("MyApp").Class("Repo");

        var take = () => repo.DefineMethod("Helper", Modifier(modifier)).AsCallable(out _);

        take.Should().Throw<InvalidOperationException>().WithMessage("*a handle cannot be issued*");
    }

    [DataTestMethod]
    [DataRow("Public")]
    [DataRow("Internal")]
    [DataRow("ProtectedInternal")]
    public void AsCallable_OnAMemberReachableFromAnywhere_IsAllowed(string modifier)
    {
        var repo = NamespaceBuilder.Get("MyApp").Class("Repo");

        var take = () => repo.DefineMethod("Helper", Modifier(modifier)).AsCallable(out _);

        take.Should().NotThrow();
    }

    [TestMethod]
    public void NarrowingAccessibilityAfterAHandle_Throws()
    {
        var repo = NamespaceBuilder.Get("MyApp").Class("Repo");
        var helper = repo.DefineMethod("Helper");
        helper.AsCallable(out _);

        var narrow = () => helper.WithAccessModifier(AccessModifier.Private);

        narrow.Should().Throw<InvalidOperationException>()
            .WithMessage("*has issued a handle, so its accessibility cannot change*");
    }

    [TestMethod]
    public void NarrowingAConstructorAfterAHandle_Throws()
    {
        var widget = NamespaceBuilder.Get("MyApp").Class("Widget");
        var ctor = widget.DefineConstructor(AccessModifier.Public);
        ctor.AsConstructable<WidgetValuePh>(out _);

        var narrow = () => ctor.WithAccessModifier(AccessModifier.Private);

        narrow.Should().Throw<InvalidOperationException>()
            .WithMessage("*has issued a handle, so its accessibility cannot change*");
    }

    private static AccessModifier Modifier(string name)
        => name switch
        {
            "Public" => AccessModifier.Public,
            "Internal" => AccessModifier.Internal,
            "Protected" => AccessModifier.Protected,
            "ProtectedInternal" => AccessModifier.ProtectedInternal,
            "PrivateProtected" => AccessModifier.PrivateProtected,
            "None" => AccessModifier.None,
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
        };
}
