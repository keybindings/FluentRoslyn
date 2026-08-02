using System;
using FluentRoslyn.Builders;

namespace FluentRoslyn.UnitTests.Builders;

/// <summary>
/// Covers compound assignment — <c>Assign(target, op, value)</c> and the <c>??=</c>
/// pair. Type matching is a compile-time property, so these pin the operator mapping,
/// the emission, and the guards.
/// </summary>
[TestClass]
public class CompoundAssignmentTests
{
    [DataTestMethod]
    [DataRow(AssignmentOperator.Add, "N += 2;")]
    [DataRow(AssignmentOperator.Subtract, "N -= 2;")]
    [DataRow(AssignmentOperator.Multiply, "N *= 2;")]
    [DataRow(AssignmentOperator.Divide, "N /= 2;")]
    [DataRow(AssignmentOperator.Modulo, "N %= 2;")]
    [DataRow(AssignmentOperator.And, "N &= 2;")]
    [DataRow(AssignmentOperator.Or, "N |= 2;")]
    [DataRow(AssignmentOperator.ExclusiveOr, "N ^= 2;")]
    [DataRow(AssignmentOperator.LeftShift, "N <<= 2;")]
    [DataRow(AssignmentOperator.RightShift, "N >>= 2;")]
    public void AssignLiteral_MapsEachOperatorToItsToken(AssignmentOperator op, string expected)
    {
        var ops = NamespaceBuilder.Get("MyApp").Class("Ops");
        var n = ops.DefineProperty<int>("N");
        ops.DefineMethod("Run").AssignLiteral(n, op, 2);

        ops.ToString().Should().Contain(expected);
    }

    [TestMethod]
    public void Assign_ReferenceForm_Emits()
    {
        var ev = NamespaceBuilder.Get("MyApp").Class("Ev");
        var total = ev.DefineField<int>("_total");
        ev.DefineMethod("Add").WithParameter<int>("delta", out var delta)
            .Assign(total, AssignmentOperator.Add, delta);

        ev.ToString().Should().Contain("_total += delta;");
    }

    [TestMethod]
    public void AssignIfNull_EmitsCoalescingAssignment()
    {
        var d = NamespaceBuilder.Get("MyApp").Class("D");
        var name = d.DefineProperty<string>("Name");
        d.DefineMethod("Ensure").WithParameter<string>("fallback", out var fallback)
            .AssignIfNull(name, fallback)
            .AssignIfNullLiteral(name, "unnamed");

        d.ToString().Should().Contain("Name ??= fallback;")
            .And.Contain("Name ??= \"unnamed\";");
    }

    [TestMethod]
    public void CompoundAssignment_ShadowedTarget_QualifiesWithThis()
    {
        var s = NamespaceBuilder.Get("MyApp").Class("S");
        var value = s.DefineProperty<int>("value");
        s.DefineMethod("Bump").WithParameter<int>("value", out var param)
            .Assign(value, AssignmentOperator.Add, param);

        s.ToString().Should().Contain("this.value += value;");
    }

    [TestMethod]
    public void CompoundAssignment_WorksInAccessorsToo()
    {
        var c = NamespaceBuilder.Get("MyApp").Class("C");
        var count = c.DefineField<int>("_count");
        c.DefineProperty<int>("Count").WithSetter(s => s.Assign(count, AssignmentOperator.Add, s.Value));

        c.ToString().Should().Contain("_count += value;");
    }

    [TestMethod]
    public void UndefinedOperator_Throws()
    {
        var ops = NamespaceBuilder.Get("MyApp").Class("Ops");
        var n = ops.DefineProperty<int>("N");

        var assign = () => ops.DefineMethod("Run").AssignLiteral(n, (AssignmentOperator)99, 1);

        assign.Should().Throw<ArgumentOutOfRangeException>();
    }

    // The enum deliberately omits ??=, which needs a nullable target; that constraint
    // lives on AssignIfNull's signature instead.
    [TestMethod]
    public void AssignmentOperator_DoesNotIncludeCoalesce()
    {
        Enum.GetNames(typeof(AssignmentOperator)).Should().NotContain("Coalesce")
            .And.HaveCount(10);
    }
}
