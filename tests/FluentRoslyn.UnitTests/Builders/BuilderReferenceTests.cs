using System;
using FluentRoslyn.Builders;

namespace FluentRoslyn.UnitTests.Builders;

/// <summary>
/// Covers builder references: passing the builder of a type being generated alongside
/// where a type name goes, so the name is spelled once and only definable types can be
/// referenced.
/// </summary>
[TestClass]
public class BuilderReferenceTests
{
    [TestMethod]
    public void Returns_BuilderReference_EmitsQualifiedName()
    {
        var model = NamespaceBuilder.Get("MyApp.Models").Class("Order");
        var svc = NamespaceBuilder.Get("MyApp.Services").Class("OrderService");
        svc.DefineMethod("Load").Returns(model).WithParameter<int>("id").AsExpressionBody("null");

        svc.ToString().Should().Contain("public MyApp.Models.Order Load(int id) => null;");
    }

    [TestMethod]
    public void WithParameter_BuilderReference_OnMethodAndConstructor_Emits()
    {
        var model = NamespaceBuilder.Get("MyApp.Models").Class("Order");
        var svc = NamespaceBuilder.Get("MyApp.Services").Class("OrderService");
        svc.DefineMethod("Save").WithParameter(model, "order");
        svc.DefineConstructor().WithParameter(model, "seed").AddStatement("_ = seed;");

        svc.ToString().Should().Contain("public void Save(MyApp.Models.Order order)")
            .And.Contain("public OrderService(MyApp.Models.Order seed)");
    }

    [TestMethod]
    public void WithInterface_BuilderReference_EmitsInBaseList()
    {
        var audited = NamespaceBuilder.Get("MyApp").Interface("IAudited");
        var entity = NamespaceBuilder.Get("MyApp").Class("Entity").WithInterface(audited);

        entity.ToString().Should().Contain("public class Entity : MyApp.IAudited");
    }

    [TestMethod]
    public void Extends_BuilderReference_EmitsInBaseList()
    {
        var audited = NamespaceBuilder.Get("MyApp").Interface("IAudited");
        var timed = NamespaceBuilder.Get("MyApp").Interface("ITimed").Extends(audited);

        timed.ToString().Should().Contain("public interface ITimed : MyApp.IAudited");
    }

    [TestMethod]
    public void BuilderReference_ToNestedType_QualifiesThroughDeclaringType()
    {
        var outer = NamespaceBuilder.Get("MyApp").Class("Outer");
        var inner = outer.DefineClass("Inner");
        var consumer = NamespaceBuilder.Get("MyApp").Class("Consumer");
        consumer.DefineMethod("Take").WithParameter(inner, "value");

        consumer.ToString().Should().Contain("public void Take(MyApp.Outer.Inner value)");
    }

    // The guard is lazy — it holds even when the type parameter is added AFTER the
    // reference was taken, which an eager check would miss.
    [TestMethod]
    public void BuilderReference_ToGenericType_ThrowsRegardlessOfOrder()
    {
        var generic = NamespaceBuilder.Get("MyApp").Class("Repo");
        var consumer = NamespaceBuilder.Get("MyApp").Class("Consumer");
        consumer.DefineMethod("Use").WithParameter(generic, "repo");
        generic.WithTypeParameter("T");

        var emit = () => consumer.ToString();

        emit.Should().Throw<InvalidOperationException>()
            .WithMessage("*declares type parameters*");
    }

    [TestMethod]
    public void BuilderReference_UnderSimplifyTypeNames_ImportsAndShortens()
    {
        var model = NamespaceBuilder.Get("MyApp.Models").Class("Order");
        var handler = NamespaceBuilder.Get("MyApp.Web").Class("Handler").SimplifyTypeNames();
        handler.DefineConstructor().WithParameter(model, "order").AddStatement("_ = order;");

        handler.ToString().Should().StartWith("using MyApp.Models;")
            .And.Contain("public Handler(Order order)");
    }

    [TestMethod]
    public void WithParameter_BuilderReference_ValidatesParameterName()
    {
        var model = NamespaceBuilder.Get("MyApp").Class("Order");
        var svc = NamespaceBuilder.Get("MyApp").Class("Svc");

        var define = () => svc.DefineMethod("Save").WithParameter(model, "1bad");

        define.Should().Throw<ArgumentException>();
    }
}
