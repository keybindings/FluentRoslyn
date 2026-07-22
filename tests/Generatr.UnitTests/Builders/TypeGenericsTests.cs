using Generatr.Builders;

namespace Generatr.UnitTests.Builders;

[TestClass]
public class TypeGenericsTests
{
    [TestMethod]
    public void Class_WithTypeParameter_EmitsGenericName()
    {
        var c = NamespaceBuilder.Get("N").Class("Box").WithTypeParameter("T");

        c.ToString().Should().Contain("public class Box<T>");
    }

    [TestMethod]
    public void Class_MultipleTypeParameters_EmitInOrder()
    {
        var c = NamespaceBuilder.Get("N").Class("Map")
            .WithTypeParameter("TKey")
            .WithTypeParameter("TValue");

        c.ToString().Should().Contain("public class Map<TKey, TValue>");
    }

    [TestMethod]
    public void Class_WithConstraint_EmitsWhereClause()
    {
        var c = NamespaceBuilder.Get("N").Class("Repo")
            .WithTypeParameter("T")
            .WithConstraint("T", "class")
            .WithConstraint("T", "new()");

        c.ToString().Should().Contain("public class Repo<T>")
            .And.Contain("where T : class, new()");
    }

    [TestMethod]
    public void Class_TypeParametersComeBeforeBaseListAndConstraintsAfter()
    {
        var c = NamespaceBuilder.Get("N").Class("Repo")
            .WithTypeParameter("T")
            .WithInterface("IRepo<T>")
            .WithConstraint("T", "class");

        c.ToString().Should().Contain("public class Repo<T> : IRepo<T> where T : class");
    }

    [TestMethod]
    public void Struct_WithTypeParameter_Emits()
    {
        var s = NamespaceBuilder.Get("N").Struct("Nullable").WithTypeParameter("T").WithConstraint("T", "struct");

        s.ToString().Should().Contain("public struct Nullable<T>")
            .And.Contain("where T : struct");
    }

    [TestMethod]
    public void Record_WithTypeParameter_EmitsBeforeParameterList()
    {
        var r = NamespaceBuilder.Get("N").Record("Box")
            .WithTypeParameter("T")
            .WithParameter<int>("Id");

        r.ToString().Should().Contain("public record Box<T>(int Id);");
    }

    [TestMethod]
    public void Record_WithTypeParameterInterfaceAndConstraint_OrdersCorrectly()
    {
        var r = NamespaceBuilder.Get("N").Record("Box")
            .WithTypeParameter("T")
            .WithParameter<int>("Id")
            .WithInterface("IThing")
            .WithConstraint("T", "new()");

        r.ToString().Should().Contain("public record Box<T>(int Id) : IThing where T : new();");
    }

    [TestMethod]
    public void Interface_WithTypeParameter_Emits()
    {
        var i = NamespaceBuilder.Get("N").Interface("IRepo")
            .WithTypeParameter("T")
            .WithConstraint("T", "class");

        i.ToString().Should().Contain("public interface IRepo<T>")
            .And.Contain("where T : class");
    }

    [TestMethod]
    public void ConstraintWithoutTypeParameter_Throws()
    {
        var c = NamespaceBuilder.Get("N").Class("Repo").WithConstraint("T", "class");

        var act = () => c.ToString();

        act.Should().Throw<System.InvalidOperationException>().WithMessage("*no type parameters*");
    }

    [TestMethod]
    public void Constraints_AddedOutOfOrder_EmitInCanonicalOrder()
    {
        // new() before class must still emit `class, ..., new()`.
        var c = NamespaceBuilder.Get("N").Class("Repo")
            .WithTypeParameter("T")
            .WithConstraint("T", "new()")
            .WithConstraint("T", "IComparable<T>")
            .WithConstraint("T", "class");

        c.ToString().Should().Contain("where T : class, IComparable<T>, new()");
    }

    [TestMethod]
    public void ConstraintForUndeclaredTypeParameter_Throws()
    {
        var c = NamespaceBuilder.Get("N").Class("Repo")
            .WithTypeParameter("T")
            .WithConstraint("U", "class");

        var act = () => c.ToString();

        act.Should().Throw<System.InvalidOperationException>().WithMessage("*undeclared type parameter*");
    }
}
