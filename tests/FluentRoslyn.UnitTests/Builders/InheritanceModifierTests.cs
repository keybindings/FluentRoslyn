using FluentRoslyn.Builders;

namespace FluentRoslyn.UnitTests.Builders;

[TestClass]
public class InheritanceModifierTests
{
    #region Method modifiers

    [TestMethod]
    public void Virtual_EmitsVirtualKeyword()
    {
        var mb = NewClass().DefineMethod<int>("Area").Virtual().AsExpressionBody("0");

        mb.ToString().Should().Be("public virtual int Area() => 0;");
    }

    [TestMethod]
    public void Override_EmitsOverrideKeyword()
    {
        var mb = NewClass().DefineMethod<int>("Area").Override().AsExpressionBody("1");

        mb.ToString().Should().Be("public override int Area() => 1;");
    }

    [TestMethod]
    public void SealedOverride_EmitsSealedBeforeOverride()
    {
        var mb = NewClass().DefineMethod<int>("Area").SealedOverride().AsExpressionBody("1");

        mb.ToString().Should().Be("public sealed override int Area() => 1;");
    }

    [TestMethod]
    public void Abstract_EmitsSemicolonInsteadOfBody()
    {
        var mb = NewClass().Abstract().DefineMethod("Draw").Abstract();

        mb.ToString().Should().Be("public abstract void Draw();");
    }

    [TestMethod]
    public void Abstract_ReturningMethod_NeedsNoBody()
    {
        var mb = NewClass().Abstract().DefineMethod<int>("Area").Abstract();

        // The usual "non-void needs a body" rule does not apply to abstract members.
        mb.ToString().Should().Be("public abstract int Area();");
    }

    [TestMethod]
    public void InheritanceModifiers_AreMutuallyExclusive_LastCallWins()
    {
        var mb = NewClass().DefineMethod<int>("Area").Virtual().Override().AsExpressionBody("1");

        mb.ToString().Should().Be("public override int Area() => 1;");
    }

    #endregion

    #region Method validation

    [TestMethod]
    public void StaticVirtual_Throws()
    {
        var mb = NewClass().DefineMethod("X").Static().Virtual();

        var act = () => mb.ToString();

        act.Should().Throw<InvalidOperationException>().WithMessage("*both static and virtual*");
    }

    [TestMethod]
    public void PrivateVirtual_Throws()
    {
        var mb = NewClass().DefineMethod("X", AccessModifier.Private).Virtual();

        var act = () => mb.ToString();

        act.Should().Throw<InvalidOperationException>().WithMessage("*private and virtual*");
    }

    [TestMethod]
    public void PartialOverride_Throws()
    {
        var mb = NewClass().DefineMethod("X").Partial().Override();

        var act = () => mb.ToString();

        act.Should().Throw<InvalidOperationException>().WithMessage("*partial*");
    }

    [TestMethod]
    public void AbstractWithExpressionBody_Throws()
    {
        var mb = NewClass().Abstract().DefineMethod("X").Abstract().AsExpressionBody("1");

        var act = () => mb.ToString();

        act.Should().Throw<InvalidOperationException>().WithMessage("*cannot have a body*");
    }

    [TestMethod]
    public void AbstractWithStatements_Throws()
    {
        var mb = NewClass().Abstract().DefineMethod("X").Abstract().AddStatement("Foo();");

        var act = () => mb.ToString();

        act.Should().Throw<InvalidOperationException>().WithMessage("*cannot have a body*");
    }

    [TestMethod]
    public void AbstractMethodInNonAbstractClass_Throws()
    {
        var cb = NewClass();
        cb.DefineMethod("Draw").Abstract();

        var act = () => cb.ToString();

        act.Should().Throw<InvalidOperationException>().WithMessage("*is not abstract*");
    }

    [TestMethod]
    public void AbstractMethodInStruct_Throws()
    {
        var s = NamespaceBuilder.Get("N").Struct("Point");
        s.DefineMethod("Draw").Abstract();

        var act = () => s.ToString();

        act.Should().Throw<InvalidOperationException>().WithMessage("*is not abstract*");
    }

    #endregion

    #region Class modifiers

    [TestMethod]
    public void AbstractClass_EmitsAbstractKeyword()
    {
        NewClass().Abstract().ToString().Should().Contain("public abstract class Shape");
    }

    [TestMethod]
    public void SealedClass_EmitsSealedKeyword()
    {
        NewClass().Sealed().ToString().Should().Contain("public sealed class Shape");
    }

    [TestMethod]
    public void AbstractPartialClass_EmitsInCanonicalOrder()
    {
        NewClass().Abstract().Partial().ToString().Should().Contain("public abstract partial class Shape");
    }

    [DataTestMethod]
    [DataRow("static+abstract")]
    [DataRow("static+sealed")]
    [DataRow("abstract+sealed")]
    public void MutuallyExclusiveClassModifiers_Throw(string combination)
    {
        var cb = NewClass();
        if (combination.Contains("static")) cb.Static();
        if (combination.Contains("abstract")) cb.Abstract();
        if (combination.Contains("sealed")) cb.Sealed();

        var act = () => cb.ToString();

        act.Should().Throw<InvalidOperationException>().WithMessage("*at most one of static, abstract, or sealed*");
    }

    #endregion

    [TestMethod]
    public void AbstractClass_WithAbstractAndVirtualMembers_EmitsFullShape()
    {
        var cb = NewClass().Abstract();
        cb.DefineMethod("Draw").Abstract();
        cb.DefineMethod<int>("Area").Virtual().AsExpressionBody("0");

        cb.ToString().Should().Be(string.Join("\n",
            "namespace TestNamespace;",
            "public abstract class Shape",
            "{",
            "    public virtual int Area() => 0;",
            "    public abstract void Draw();",
            "}"));
    }

    private static ClassBuilder NewClass()
        => NamespaceBuilder.Get("TestNamespace").Class("Shape");
}
