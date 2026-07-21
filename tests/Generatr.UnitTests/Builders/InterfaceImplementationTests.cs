using System;
using Generatr.Builders;

namespace Generatr.UnitTests.Builders;

[TestClass]
public class InterfaceImplementationTests
{
    [TestMethod]
    public void Class_WithInterface_EmitsInBaseList()
    {
        var c = NamespaceBuilder.Get("N").Class("Widget").WithInterface("IThing");

        c.ToString().Should().Contain("public class Widget : IThing");
    }

    [TestMethod]
    public void Class_WithGenericInterface_PreservesTypeArguments()
    {
        var c = NamespaceBuilder.Get("N").Class("Widget").WithInterface("IEquatable<Widget>");

        c.ToString().Should().Contain("public class Widget : IEquatable<Widget>");
    }

    [TestMethod]
    public void Class_WithTypedInterface_QualifiesTheType()
    {
        var c = NamespaceBuilder.Get("N").Class("Widget").WithInterface<IDisposable>();

        c.ToString().Should().Contain("public class Widget : System.IDisposable");
    }

    [TestMethod]
    public void Class_BaseClassComesBeforeInterfaces()
    {
        var baseClass = NamespaceBuilder.Get("N").Class("BaseThing");
        var c = NamespaceBuilder.Get("N").Class("Widget")
            .WithParent(baseClass)
            .WithInterface("IThing")
            .WithInterface<IDisposable>();

        c.ToString().Should().Contain("public class Widget : N.BaseThing, IThing, System.IDisposable");
    }

    [TestMethod]
    public void Struct_WithInterface_EmitsBaseList()
    {
        var s = NamespaceBuilder.Get("N").Struct("Point").WithInterface("IEquatable<Point>");

        s.ToString().Should().Contain("public struct Point : IEquatable<Point>");
    }

    [TestMethod]
    public void Struct_WithMultipleInterfaces_SeparatesWithCommas()
    {
        var s = NamespaceBuilder.Get("N").Struct("Point")
            .WithInterface("IEquatable<Point>")
            .WithInterface<IComparable>();

        s.ToString().Should().Contain("public struct Point : IEquatable<Point>, System.IComparable");
    }

    [TestMethod]
    public void NoInterfaceOrBase_EmitsNoBaseList()
    {
        var s = NamespaceBuilder.Get("N").Struct("Point");

        s.ToString().Should().NotContain(":");
    }

    [TestMethod]
    public void WithInterface_ReturnsConcreteBuilderForChaining()
    {
        var s = NamespaceBuilder.Get("N").Struct("Point").WithInterface("IThing");

        s.Should().BeOfType<StructBuilder>();
    }
}
