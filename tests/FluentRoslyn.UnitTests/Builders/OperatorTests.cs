using System;
using FluentRoslyn.Builders;

namespace FluentRoslyn.UnitTests.Builders;

/// <summary>
/// Covers operator and conversion declarations. Pulled by the value-objects example: a
/// strongly-typed id whose users must write <c>a.Equals(b)</c> instead of <c>a == b</c>
/// is not one anyone would adopt.
/// </summary>
[TestClass]
public class OperatorTests
{
    private static StructBuilder ValueObject()
        => NamespaceBuilder.Get("MyApp").Struct("OrderId").Readonly().Partial();

    private static void DeclareEqualityPair(StructBuilder valueObject)
    {
        valueObject.DefineOperator<bool>(OperatorKind.Equality)
            .WithParameter("left", "MyApp.OrderId")
            .WithParameter("right", "MyApp.OrderId")
            .AsExpressionBody("left.Equals(right)");

        valueObject.DefineOperator<bool>(OperatorKind.Inequality)
            .WithParameter("left", "MyApp.OrderId")
            .WithParameter("right", "MyApp.OrderId")
            .AsExpressionBody("!(left == right)");
    }

    [TestMethod]
    public void Operators_EmitAsPublicStatic()
    {
        var valueObject = ValueObject();

        DeclareEqualityPair(valueObject);

        var code = valueObject.ToString();

        code.Should()
            .Contain("public static bool operator ==(MyApp.OrderId left, MyApp.OrderId right) => left.Equals(right);").And
            .Contain("public static bool operator !=(MyApp.OrderId left, MyApp.OrderId right) => !(left == right);");
    }

    // C# rejects `==` declared without `!=` (CS0216). Only the type sees both, so it
    // refuses to emit rather than handing the consumer a build error.
    [TestMethod]
    public void AnEqualityOperatorWithoutItsPartner_Throws()
    {
        var valueObject = ValueObject();

        valueObject.DefineOperator<bool>(OperatorKind.Equality)
            .WithParameter("left", "MyApp.OrderId")
            .WithParameter("right", "MyApp.OrderId")
            .AsExpressionBody("true");

        var build = () => valueObject.ToString();

        build.Should().Throw<InvalidOperationException>()
            .WithMessage("*declares operator '==' without '!='*");
    }

    [TestMethod]
    public void AnOrderingOperatorWithoutItsPartner_Throws()
    {
        var valueObject = ValueObject();
        DeclareEqualityPair(valueObject);

        valueObject.DefineOperator<bool>(OperatorKind.LessThan)
            .WithParameter("left", "MyApp.OrderId")
            .WithParameter("right", "MyApp.OrderId")
            .AsExpressionBody("true");

        var build = () => valueObject.ToString();

        build.Should().Throw<InvalidOperationException>().WithMessage("*'<' without '>'*");
    }

    [TestMethod]
    public void TrueAndFalse_MustAlsoPair()
    {
        var valueObject = ValueObject();

        valueObject.DefineOperator<bool>(OperatorKind.True)
            .WithParameter("value", "MyApp.OrderId")
            .AsExpressionBody("true");

        var build = () => valueObject.ToString();

        build.Should().Throw<InvalidOperationException>().WithMessage("*'true' without 'false'*");
    }

    [TestMethod]
    public void BothHalvesOfAPair_Satisfy()
    {
        var valueObject = ValueObject();
        DeclareEqualityPair(valueObject);

        var build = () => valueObject.ToString();

        build.Should().NotThrow();
    }

    // NormalizeWhitespace pads between the target type and the parameter list when the
    // target is a predefined-type keyword -- the same family of quirk as `int[, ]` -- but
    // not when it is a qualified name; see the explicit-conversion test below. Both are
    // valid C#. These pin what is emitted rather than what would look tidiest.
    [TestMethod]
    public void AnImplicitConversion_Emits()
    {
        var valueObject = ValueObject();
        var value = valueObject.DefineProperty("Value", "int").GetOnly();

        valueObject.DefineConversion<int>(ConversionKind.Implicit)
            .WithParameter("id", "MyApp.OrderId", out var id)
            .ReturnRaw(id.MemberRaw("Value"));

        var code = valueObject.ToString();

        code.Should()
            .Contain("public static implicit operator int (MyApp.OrderId id)").And
            .Contain("return id.Value;");
    }

    [TestMethod]
    public void AnExplicitConversionToANamedType_Emits()
    {
        var valueObject = ValueObject();

        valueObject.DefineConversion(ConversionKind.Explicit, "MyApp.OrderId")
            .WithParameter("value", "int")
            .AsExpressionBody("new MyApp.OrderId(value)");

        // No padding here: the target is a qualified name rather than a keyword.
        valueObject.ToString().Should().Contain("public static explicit operator MyApp.OrderId(int value)");
    }

    [TestMethod]
    public void AnOperatorReturningTheGeneratedType_UsesTheNamedForm()
    {
        var valueObject = ValueObject();

        valueObject.DefineOperator(OperatorKind.Plus, "MyApp.OrderId")
            .WithParameter("left", "MyApp.OrderId")
            .WithParameter("right", "MyApp.OrderId")
            .AsExpressionBody("new MyApp.OrderId(left.Value + right.Value)");

        valueObject.ToString().Should()
            .Contain("public static MyApp.OrderId operator +(MyApp.OrderId left, MyApp.OrderId right)");
    }

    [TestMethod]
    public void AUnaryOperator_IsJustOneParameter()
    {
        var valueObject = ValueObject();

        valueObject.DefineOperator(OperatorKind.Minus, "MyApp.OrderId")
            .WithParameter("value", "MyApp.OrderId")
            .AsExpressionBody("new MyApp.OrderId(-value.Value)");

        valueObject.ToString().Should()
            .Contain("public static MyApp.OrderId operator -(MyApp.OrderId value)");
    }

    [TestMethod]
    public void AnOperatorWithNoBody_Throws()
    {
        var valueObject = ValueObject();

        valueObject.DefineOperator(OperatorKind.Plus, "MyApp.OrderId")
            .WithParameter("value", "MyApp.OrderId");

        var build = () => valueObject.ToString();

        build.Should().Throw<InvalidOperationException>().WithMessage("*has no body*");
    }

    [TestMethod]
    public void AnOperatorWithBothBodyForms_Throws()
    {
        var valueObject = ValueObject();

        valueObject.DefineOperator(OperatorKind.Plus, "MyApp.OrderId")
            .WithParameter("value", "MyApp.OrderId")
            .AsExpressionBody("value")
            .AddStatement("return value;");

        var build = () => valueObject.ToString();

        build.Should().Throw<InvalidOperationException>()
            .WithMessage("*both an expression body and statements*");
    }

    // An operator is static, so `this` does not exist inside one. A parameter shadowing a
    // member is therefore an error rather than something `this.` can disambiguate --
    // which is the existing static-context rule, inherited for free.
    [TestMethod]
    public void AParameterShadowingAMember_Throws()
    {
        var valueObject = ValueObject();
        var value = valueObject.DefineProperty("value", "int").GetOnly();

        var declare = () => valueObject.DefineOperator<bool>(OperatorKind.Equality)
            .WithParameter("value", "int", out var parameter)
            .ReturnRaw(Invocations.InvokeRaw(value, "Equals", parameter));

        declare.Should().Throw<InvalidOperationException>()
            .WithMessage("*shadows the member being referenced*");
    }

    [TestMethod]
    public void Operators_CarryDocsAndAttributes()
    {
        var valueObject = ValueObject();
        DeclareEqualityPair(valueObject);

        valueObject.DefineOperator<bool>(OperatorKind.LessThan)
            .WithSummary("Orders two ids.")
            .WithAttribute("Obsolete")
            .WithParameter("left", "MyApp.OrderId")
            .WithParameter("right", "MyApp.OrderId")
            .AsExpressionBody("true");

        valueObject.DefineOperator<bool>(OperatorKind.GreaterThan)
            .WithParameter("left", "MyApp.OrderId")
            .WithParameter("right", "MyApp.OrderId")
            .AsExpressionBody("false");

        var code = valueObject.ToString();

        code.Should()
            .Contain("Orders two ids.").And
            .Contain("[Obsolete]");
    }

    // Accessibility and staticness are genuinely fixed (CS0558), but unsafe and -- since
    // C# 11 -- checked are not, so both are offered.
    [TestMethod]
    public void Unsafe_IsEmitted()
    {
        var valueObject = ValueObject();

        valueObject.DefineOperator(OperatorKind.Multiply, "MyApp.OrderId")
            .Unsafe()
            .WithParameter("left", "MyApp.OrderId")
            .WithParameter("right", "MyApp.OrderId")
            .AsExpressionBody("left");

        valueObject.ToString().Should()
            .Contain("public static unsafe MyApp.OrderId operator *(MyApp.OrderId left, MyApp.OrderId right)");
    }

    [TestMethod]
    public void AChecked_OperatorEmitsAlongsideItsUncheckedForm()
    {
        var valueObject = ValueObject();

        valueObject.DefineOperator(OperatorKind.Plus, "MyApp.OrderId")
            .WithParameter("left", "MyApp.OrderId")
            .WithParameter("right", "MyApp.OrderId")
            .AsExpressionBody("left");

        valueObject.DefineOperator(OperatorKind.Plus, "MyApp.OrderId")
            .Checked()
            .WithParameter("left", "MyApp.OrderId")
            .WithParameter("right", "MyApp.OrderId")
            .AsExpressionBody("right");

        valueObject.ToString().Should()
            .Contain("operator +(MyApp.OrderId left, MyApp.OrderId right) => left;").And
            .Contain("operator checked +(MyApp.OrderId left, MyApp.OrderId right) => right;");
    }

    // CS9025 in the consumer's build; refused here instead.
    [TestMethod]
    public void ACheckedOperatorWithoutItsUncheckedForm_Throws()
    {
        var valueObject = ValueObject();

        valueObject.DefineOperator(OperatorKind.Minus, "MyApp.OrderId")
            .Checked()
            .WithParameter("left", "MyApp.OrderId")
            .WithParameter("right", "MyApp.OrderId")
            .AsExpressionBody("left");

        var build = () => valueObject.ToString();

        build.Should().Throw<InvalidOperationException>()
            .WithMessage("*checked 'operator -' without a matching unchecked form*");
    }

    // CS9023. Only + - * / ++ -- have checked forms.
    [TestMethod]
    public void ACheckedFormOnAnIneligibleOperator_Throws()
    {
        var valueObject = ValueObject();

        valueObject.DefineOperator(OperatorKind.Modulo, "MyApp.OrderId")
            .WithParameter("left", "MyApp.OrderId")
            .WithParameter("right", "MyApp.OrderId")
            .AsExpressionBody("left");

        valueObject.DefineOperator(OperatorKind.Modulo, "MyApp.OrderId")
            .Checked()
            .WithParameter("left", "MyApp.OrderId")
            .WithParameter("right", "MyApp.OrderId")
            .AsExpressionBody("left");

        var build = () => valueObject.ToString();

        build.Should().Throw<InvalidOperationException>().WithMessage("*'%' cannot be declared checked*");
    }

    // Binary + has a checked form; unary + does not, so arity decides.
    [TestMethod]
    public void ACheckedUnaryPlus_Throws()
    {
        var valueObject = ValueObject();

        valueObject.DefineOperator(OperatorKind.Plus, "MyApp.OrderId")
            .WithParameter("value", "MyApp.OrderId")
            .AsExpressionBody("value");

        valueObject.DefineOperator(OperatorKind.Plus, "MyApp.OrderId")
            .Checked()
            .WithParameter("value", "MyApp.OrderId")
            .AsExpressionBody("value");

        var build = () => valueObject.ToString();

        build.Should().Throw<InvalidOperationException>().WithMessage("*'+' cannot be declared checked*");
    }

    // CS9024: only an explicit conversion may be checked.
    [TestMethod]
    public void ACheckedImplicitConversion_Throws()
    {
        var valueObject = ValueObject();

        valueObject.DefineConversion<int>(ConversionKind.Implicit)
            .Checked()
            .WithParameter("id", "MyApp.OrderId")
            .AsExpressionBody("0");

        var build = () => valueObject.ToString();

        build.Should().Throw<InvalidOperationException>()
            .WithMessage("*implicit conversion cannot be declared checked*");
    }

    [TestMethod]
    public void ACheckedExplicitConversion_EmitsAlongsideItsUncheckedForm()
    {
        var valueObject = ValueObject();

        valueObject.DefineConversion<int>(ConversionKind.Explicit)
            .WithParameter("id", "MyApp.OrderId")
            .AsExpressionBody("0");

        valueObject.DefineConversion<int>(ConversionKind.Explicit)
            .Checked()
            .WithParameter("id", "MyApp.OrderId")
            .AsExpressionBody("1");

        valueObject.ToString().Should().Contain("explicit operator checked int (MyApp.OrderId id)");
    }

    // A conversion has no partner, so it is never caught by the pairing rule.
    [TestMethod]
    public void AConversionAlone_IsFine()
    {
        var valueObject = ValueObject();

        valueObject.DefineConversion<int>(ConversionKind.Implicit)
            .WithParameter("id", "MyApp.OrderId")
            .AsExpressionBody("0");

        var build = () => valueObject.ToString();

        build.Should().NotThrow();
    }
}
