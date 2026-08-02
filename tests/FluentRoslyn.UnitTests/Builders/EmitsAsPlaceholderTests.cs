using System;
using FluentRoslyn.Abstractions;
using FluentRoslyn.Builders;

namespace FluentRoslyn.UnitTests.Builders;

[EmitsAs("MyApp.Models.Customer")]
internal sealed class CustomerPlaceholder;

[EmitsAs("MyApp.Models.IAudited")]
internal interface IAuditedPlaceholder;

[EmitsAs("GlobalThing")]
internal sealed class GlobalPlaceholder;

[EmitsAs("MyApp.Bad<Name>")]
internal sealed class MalformedPlaceholder;

[EmitsAs("MyApp.Generic")]
internal sealed class GenericPlaceholder<T>;

/// <summary>
/// Covers <c>[EmitsAs]</c> placeholders: CLR stand-ins for generated types, so the
/// typed surface — including <c>IReference&lt;T&gt;</c> and <c>Assign</c> — works for
/// types that only exist once the generator has run.
/// </summary>
[TestClass]
public class EmitsAsPlaceholderTests
{
    [TestMethod]
    public void Placeholder_ResolvesToEmittedName_InEveryTypePosition()
    {
        var repo = NamespaceBuilder.Get("MyApp.Data").Class("CustomerRepo");
        repo.DefineField<CustomerPlaceholder>("_cached");
        repo.DefineProperty<CustomerPlaceholder>("Current");
        repo.DefineMethod<CustomerPlaceholder>("Load").WithParameter<int>("id")
            .AsExpressionBody("_cached");

        repo.ToString().Should().Be(string.Join("\n",
            "namespace MyApp.Data;",
            "public class CustomerRepo",
            "{",
            "    private MyApp.Models.Customer _cached;",
            "    public MyApp.Models.Customer Current { get; set; }",
            "",
            "    public MyApp.Models.Customer Load(int id) => _cached;",
            "}"));
    }

    [TestMethod]
    public void Assign_BetweenPlaceholderTypedReferences_Emits()
    {
        var repo = NamespaceBuilder.Get("MyApp.Data").Class("CustomerRepo");
        var current = repo.DefineProperty<CustomerPlaceholder>("Current");

        repo.DefineMethod("Store")
            .WithParameter<CustomerPlaceholder>("customer", out var customerParam)
            .Assign(current, customerParam);

        repo.ToString().Should().Contain("public void Store(MyApp.Models.Customer customer)")
            .And.Contain("Current = customer;");
    }

    [TestMethod]
    public void Placeholder_Interface_EmitsInBaseList()
    {
        var svc = NamespaceBuilder.Get("MyApp").Class("Svc").WithInterface<IAuditedPlaceholder>();

        svc.ToString().Should().Contain("public class Svc : MyApp.Models.IAudited");
    }

    [TestMethod]
    public void Placeholder_WithoutNamespace_EmitsBareName()
    {
        var svc = NamespaceBuilder.Get("MyApp").Class("Svc");
        svc.DefineField<GlobalPlaceholder>("_thing");

        svc.ToString().Should().Contain("private GlobalThing _thing;");
    }

    [TestMethod]
    public void Placeholder_ComposesWithArraysAndGenerics()
    {
        var svc = NamespaceBuilder.Get("MyApp").Class("Svc");
        svc.DefineField<CustomerPlaceholder[]>("_batch");
        svc.DefineProperty<System.Collections.Generic.List<CustomerPlaceholder>>("All");

        svc.ToString().Should().Contain("private MyApp.Models.Customer[] _batch;")
            .And.Contain("public System.Collections.Generic.List<MyApp.Models.Customer> All { get; set; }");
    }

    [TestMethod]
    public void Placeholder_UnderSimplifyTypeNames_ImportsAndShortens()
    {
        var simp = NamespaceBuilder.Get("MyApp").Class("Simp").SimplifyTypeNames();
        simp.DefineProperty<CustomerPlaceholder>("Current");

        simp.ToString().Should().StartWith("using MyApp.Models;")
            .And.Contain("public Customer Current { get; set; }");
    }

    [TestMethod]
    public void Placeholder_WithMalformedName_Throws()
    {
        var svc = NamespaceBuilder.Get("MyApp").Class("Svc");

        var define = () => svc.DefineField<MalformedPlaceholder>("_bad");

        define.Should().Throw<ArgumentException>()
            .WithMessage("*not a plain namespace-qualified identifier*");
    }

    [TestMethod]
    public void Placeholder_Generic_Throws()
    {
        var svc = NamespaceBuilder.Get("MyApp").Class("Svc");

        var define = () => svc.DefineField<GenericPlaceholder<int>>("_bad");

        define.Should().Throw<InvalidOperationException>()
            .WithMessage("*generic, which [EmitsAs] does not support*");
    }

    [TestMethod]
    public void EmitsAs_NullName_Throws()
    {
        var construct = () => new EmitsAsAttribute(null!);

        construct.Should().Throw<ArgumentNullException>();
    }
}
