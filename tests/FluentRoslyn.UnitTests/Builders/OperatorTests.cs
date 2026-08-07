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

    // An operator is static, so `this` does not exist inside one and an instance member
    // is unreachable outright -- renaming the shadowing parameter could not fix it, so
    // the static-context message wins over the shadowing one.
    [TestMethod]
    public void AParameterShadowingAMember_Throws()
    {
        var valueObject = ValueObject();
        var value = valueObject.DefineProperty("value", "int").GetOnly();

        var declare = () => valueObject.DefineOperator<bool>(OperatorKind.Equality)
            .WithParameter("value", "int", out var parameter)
            .ReturnRaw(Invocations.InvokeRaw(value, "Equals", parameter));

        declare.Should().Throw<InvalidOperationException>()
            .WithMessage("*cannot reference 'value', an instance member*");
    }

    // The same guard, without any shadowing: an unqualified instance member in an
    // operator body would emit bare and fail the consumer's build with CS0120.
    [TestMethod]
    public void AnInstanceMemberInAnOperatorBody_Throws()
    {
        var valueObject = ValueObject();
        var value = valueObject.DefineProperty("Value", "int").GetOnly();

        var declare = () => valueObject.DefineConversion<int>(ConversionKind.Explicit)
            .WithParameter("id", "MyApp.OrderId")
            .ReturnRaw(value);

        declare.Should().Throw<InvalidOperationException>()
            .WithMessage("*cannot reference 'Value', an instance member*");
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

        valueObject.ToString().Should()
            .Contain("explicit operator int (MyApp.OrderId id) => 0;").And
            .Contain("explicit operator checked int (MyApp.OrderId id) => 1;");
    }

    // === Arity (R2-02). C# fixes how many parameters each operator takes; a wrong
    // count emitted anyway is CS1534/CS1019/CS1020 in the consumer's build. ===

    [TestMethod]
    public void AnOperatorWithNoParameters_Throws()
    {
        var valueObject = ValueObject();

        valueObject.DefineOperator<bool>(OperatorKind.Equality).AsExpressionBody("true");

        var build = () => valueObject.ToString();

        build.Should().Throw<InvalidOperationException>().WithMessage("*'==' takes exactly two parameters*has 0*");
    }

    [TestMethod]
    public void ABinaryOperatorWithThreeParameters_Throws()
    {
        var valueObject = ValueObject();

        valueObject.DefineOperator(OperatorKind.Plus, "MyApp.OrderId")
            .WithParameter("a", "MyApp.OrderId")
            .WithParameter("b", "MyApp.OrderId")
            .WithParameter("c", "MyApp.OrderId")
            .AsExpressionBody("a");

        var build = () => valueObject.ToString();

        build.Should().Throw<InvalidOperationException>()
            .WithMessage("*'+' takes one parameter (unary) or two (binary)*has 3*");
    }

    [TestMethod]
    public void AUnaryOnlyOperatorWithTwoParameters_Throws()
    {
        var valueObject = ValueObject();

        valueObject.DefineOperator(OperatorKind.Increment, "MyApp.OrderId")
            .WithParameter("a", "MyApp.OrderId")
            .WithParameter("b", "MyApp.OrderId")
            .AsExpressionBody("a");

        var build = () => valueObject.ToString();

        build.Should().Throw<InvalidOperationException>().WithMessage("*'++' takes exactly one parameter*has 2*");
    }

    [TestMethod]
    public void AConversionWithTwoParameters_Throws()
    {
        var valueObject = ValueObject();

        valueObject.DefineConversion<int>(ConversionKind.Explicit)
            .WithParameter("a", "MyApp.OrderId")
            .WithParameter("b", "int")
            .AsExpressionBody("0");

        var build = () => valueObject.ToString();

        build.Should().Throw<InvalidOperationException>()
            .WithMessage("*a conversion takes exactly one parameter*has 2*");
    }

    // === Signature-aware pairing (R2-03). C# matches partners by signature, not by
    // symbol: ==(A, A) beside !=(A, int) is CS0216 on both. ===

    [TestMethod]
    public void APartnerWithADifferentSignature_DoesNotSatisfyThePair()
    {
        var valueObject = ValueObject();

        valueObject.DefineOperator<bool>(OperatorKind.Equality)
            .WithParameter("left", "MyApp.OrderId")
            .WithParameter("right", "MyApp.OrderId")
            .AsExpressionBody("true");

        valueObject.DefineOperator<bool>(OperatorKind.Inequality)
            .WithParameter("left", "MyApp.OrderId")
            .WithParameter("right", "int")
            .AsExpressionBody("false");

        var build = () => valueObject.ToString();

        build.Should().Throw<InvalidOperationException>()
            .WithMessage("*declares operator '==' without '!='*same parameter types (MyApp.OrderId, MyApp.OrderId)*");
    }

    // === Conversion identity (R2-05). CS0557 rejects both directions between the same
    // types, however they are decorated. ===

    [TestMethod]
    public void AnImplicitAndAnExplicitConversionToTheSameTarget_Throw()
    {
        var valueObject = ValueObject();

        valueObject.DefineConversion<int>(ConversionKind.Implicit)
            .WithParameter("id", "MyApp.OrderId")
            .AsExpressionBody("0");

        valueObject.DefineConversion<int>(ConversionKind.Explicit)
            .WithParameter("id", "MyApp.OrderId")
            .AsExpressionBody("1");

        var build = () => valueObject.ToString();

        build.Should().Throw<InvalidOperationException>()
            .WithMessage("*both an implicit and an explicit conversion*");
    }

    [TestMethod]
    public void TheSameConversionDeclaredTwice_Throws()
    {
        var valueObject = ValueObject();

        valueObject.DefineConversion<int>(ConversionKind.Explicit)
            .WithParameter("id", "MyApp.OrderId")
            .AsExpressionBody("0");

        valueObject.DefineConversion<int>(ConversionKind.Explicit)
            .WithParameter("id", "MyApp.OrderId")
            .AsExpressionBody("1");

        var build = () => valueObject.ToString();

        build.Should().Throw<InvalidOperationException>().WithMessage("*more than once*");
    }

    // === Checked twin identity (R2-04). The twin must share the signature, and
    // conversion identity spans implicit/explicit, so a checked explicit beside an
    // unchecked implicit is both CS9025 and CS0557 -- the mixed-direction rule fires. ===

    [TestMethod]
    public void ACheckedFormWhoseUncheckedTwinDiffersInSignature_Throws()
    {
        var valueObject = ValueObject();

        valueObject.DefineOperator(OperatorKind.Plus, "MyApp.OrderId")
            .WithParameter("left", "MyApp.OrderId")
            .WithParameter("right", "MyApp.OrderId")
            .AsExpressionBody("left");

        valueObject.DefineOperator(OperatorKind.Plus, "MyApp.OrderId")
            .Checked()
            .WithParameter("left", "MyApp.OrderId")
            .WithParameter("right", "int")
            .AsExpressionBody("left");

        var build = () => valueObject.ToString();

        build.Should().Throw<InvalidOperationException>()
            .WithMessage("*checked 'operator +' without a matching unchecked form*(MyApp.OrderId, int)*");
    }

    [TestMethod]
    public void ACheckedExplicitBesideAnUncheckedImplicit_Throws()
    {
        var valueObject = ValueObject();

        valueObject.DefineConversion<int>(ConversionKind.Implicit)
            .WithParameter("id", "MyApp.OrderId")
            .AsExpressionBody("0");

        valueObject.DefineConversion<int>(ConversionKind.Explicit)
            .Checked()
            .WithParameter("id", "MyApp.OrderId")
            .AsExpressionBody("1");

        var build = () => valueObject.ToString();

        build.Should().Throw<InvalidOperationException>()
            .WithMessage("*both an implicit and an explicit conversion*");
    }

    // === Canonical type text (R2-08). Two spellings of one type must count as one:
    // the twin check compares int against System.Int32 and spaced against unspaced
    // generics, because mixing symbol-derived and hand-written names is routine. ===

    [TestMethod]
    public void TypeSpellings_AreCanonicalizedBeforeComparing()
    {
        var valueObject = ValueObject();

        valueObject.DefineConversion<int>(ConversionKind.Explicit)
            .WithParameter("id", "MyApp.OrderId")
            .AsExpressionBody("0");

        // The checked twin names the target System.Int32 and spells the parameter with
        // no space in a generic -- both must match the line above.
        valueObject.DefineConversion(ConversionKind.Explicit, "System.Int32")
            .Checked()
            .WithParameter("id", "MyApp.OrderId")
            .AsExpressionBody("1");

        var build = () => valueObject.ToString();

        build.Should().NotThrow();
    }

    [TestMethod]
    public void GenericWhitespace_IsCanonicalizedBeforeComparing()
    {
        var valueObject = ValueObject();

        valueObject.DefineOperator(OperatorKind.Plus, "MyApp.OrderId")
            .WithParameter("left", "MyApp.Wrapper<string,int>")
            .WithParameter("right", "MyApp.Wrapper<string, int>")
            .AsExpressionBody("left");

        valueObject.DefineOperator(OperatorKind.Plus, "MyApp.OrderId")
            .Checked()
            .WithParameter("left", "MyApp.Wrapper<string, int>")
            .WithParameter("right", "MyApp.Wrapper<string,int>")
            .AsExpressionBody("left");

        var build = () => valueObject.ToString();

        build.Should().NotThrow();
    }

    // === A static class cannot declare operators (R2-06): CS0715, and its type can
    // never be a parameter (CS0721). ===

    [TestMethod]
    public void OperatorsOnAStaticClass_Throw()
    {
        var helpers = NamespaceBuilder.Get("MyApp").Class("Helpers").Static();

        helpers.DefineOperator<bool>(OperatorKind.Equality)
            .WithParameter("l", "int").WithParameter("r", "int").AsExpressionBody("true");
        helpers.DefineOperator<bool>(OperatorKind.Inequality)
            .WithParameter("l", "int").WithParameter("r", "int").AsExpressionBody("false");

        var build = () => helpers.ToString();

        build.Should().Throw<InvalidOperationException>().WithMessage("*static class*cannot declare operators*");
    }

    // === A partial type may legally split a pair across its parts (R2-07); the
    // builder sees one part, so the requirement is waivable per operator. ===

    [TestMethod]
    public void PartnerDeclaredElsewhere_WaivesThePairForAPartialSplit()
    {
        var valueObject = ValueObject();

        valueObject.DefineOperator<bool>(OperatorKind.Equality)
            .PartnerDeclaredElsewhere()
            .WithParameter("left", "MyApp.OrderId")
            .WithParameter("right", "MyApp.OrderId")
            .AsExpressionBody("left.Equals(right)");

        var build = () => valueObject.ToString();

        build.Should().NotThrow();
        valueObject.ToString().Should().Contain("operator ==");
    }

    [TestMethod]
    public void PartnerDeclaredElsewhere_AlsoWaivesTheUncheckedTwin()
    {
        var valueObject = ValueObject();

        valueObject.DefineOperator(OperatorKind.Minus, "MyApp.OrderId")
            .Checked()
            .PartnerDeclaredElsewhere()
            .WithParameter("left", "MyApp.OrderId")
            .WithParameter("right", "MyApp.OrderId")
            .AsExpressionBody("left");

        var build = () => valueObject.ToString();

        build.Should().NotThrow();
    }

    // === operator true/false must return bool (R2-10), and the emission path works. ===

    [TestMethod]
    public void OperatorTrue_WithANonBoolResult_Throws()
    {
        var valueObject = ValueObject();

        valueObject.DefineOperator<int>(OperatorKind.True)
            .WithParameter("value", "MyApp.OrderId")
            .AsExpressionBody("1");

        var build = () => valueObject.ToString();

        build.Should().Throw<InvalidOperationException>().WithMessage("*'true' must return bool, not 'int'*");
    }

    [TestMethod]
    public void OperatorTrueAndFalse_EmitAsAPair()
    {
        var valueObject = ValueObject();

        valueObject.DefineOperator<bool>(OperatorKind.True)
            .WithParameter("value", "MyApp.OrderId")
            .AsExpressionBody("true");
        valueObject.DefineOperator<bool>(OperatorKind.False)
            .WithParameter("value", "MyApp.OrderId")
            .AsExpressionBody("false");

        var code = valueObject.ToString();

        code.Should()
            .Contain("public static bool operator true (MyApp.OrderId value) => true;").And
            .Contain("public static bool operator false (MyApp.OrderId value) => false;");
    }

    // === Out-of-range enum values (R2-12, R2-13): a contextual error at Define time,
    // not a KeyNotFoundException from a private dictionary -- and default(ConversionKind)
    // is deliberately invalid, so a computed default cannot silently mean implicit. ===

    [TestMethod]
    public void AnUndefinedOperatorKind_ThrowsAtDefineTime()
    {
        var valueObject = ValueObject();

        var define = () => valueObject.DefineOperator((OperatorKind)999, "int");

        define.Should().Throw<ArgumentException>().WithMessage("*'999' is not a defined OperatorKind*");
    }

    [TestMethod]
    public void AnUndefinedConversionKind_ThrowsAtDefineTime()
    {
        var valueObject = ValueObject();

        var outOfRange = () => valueObject.DefineConversion<int>((ConversionKind)99);
        var computedDefault = () => valueObject.DefineConversion<int>(default(ConversionKind));

        outOfRange.Should().Throw<ArgumentException>().WithMessage("*'99' is not a defined ConversionKind*");
        computedDefault.Should().Throw<ArgumentException>().WithMessage("*'0' is not a defined ConversionKind*");
    }

    // === Member-level validation runs on the member's own build path (R2-09), so a
    // lone builder's ToString() refuses the same declarations a whole type does. ===

    [TestMethod]
    public void AMemberToString_RunsTheSameValidation()
    {
        var valueObject = ValueObject();
        var @operator = valueObject.DefineOperator(OperatorKind.Modulo, "MyApp.OrderId")
            .Checked()
            .WithParameter("l", "MyApp.OrderId")
            .WithParameter("r", "MyApp.OrderId")
            .AsExpressionBody("l");

        var memberAlone = () => @operator.ToString();

        memberAlone.Should().Throw<InvalidOperationException>().WithMessage("*'%' cannot be declared checked*");
    }

    // === >>> exists (R2-14, C# 11), and the one formatting quirk is pinned (R2-20):
    // NormalizeWhitespace glues `operator>` alone -- even `>>>` gets its space -- via
    // its generic-angle-bracket heuristic. Both are valid C#; these pin what is
    // emitted, in the same family as `int[, ]` and `implicit operator int (`. ===

    [TestMethod]
    public void UnsignedRightShift_Emits()
    {
        var bits = NamespaceBuilder.Get("MyApp").Struct("Bits");

        bits.DefineOperator(OperatorKind.UnsignedRightShift, "MyApp.Bits")
            .WithParameter("left", "MyApp.Bits")
            .WithParameter("right", "int")
            .AsExpressionBody("left");

        bits.ToString().Should().Contain("public static MyApp.Bits operator >>>(MyApp.Bits left, int right) => left;");
    }

    [TestMethod]
    public void OperatorGreaterThan_EmitsGluedToItsParameterList()
    {
        var valueObject = ValueObject();
        DeclareEqualityPair(valueObject);

        valueObject.DefineOperator<bool>(OperatorKind.GreaterThan)
            .WithParameter("l", "MyApp.OrderId").WithParameter("r", "MyApp.OrderId").AsExpressionBody("true");
        valueObject.DefineOperator<bool>(OperatorKind.LessThan)
            .WithParameter("l", "MyApp.OrderId").WithParameter("r", "MyApp.OrderId").AsExpressionBody("false");

        valueObject.ToString().Should()
            .Contain("bool operator>(MyApp.OrderId l, MyApp.OrderId r)").And
            .Contain("bool operator <(MyApp.OrderId l, MyApp.OrderId r)");
    }

    // === Records can declare operators (R2-19), gaining a brace body -- but never ==
    // or !=, which a record synthesizes and a consumer's build would reject. ===

    [TestMethod]
    public void ARecord_CanDeclareOperatorsAndConversions()
    {
        var point = NamespaceBuilder.Get("MyApp").Record("Point")
            .WithParameter<int>("X")
            .WithParameter<int>("Y");

        point.DefineOperator(OperatorKind.Plus, "MyApp.Point")
            .WithParameter("left", "MyApp.Point")
            .WithParameter("right", "MyApp.Point")
            .AsExpressionBody("new MyApp.Point(left.X + right.X, left.Y + right.Y)");

        var code = point.ToString();

        code.Should()
            .Contain("public record Point(int X, int Y)").And
            .Contain("public static MyApp.Point operator +(MyApp.Point left, MyApp.Point right)").And
            .NotContain("record Point(int X, int Y);");
    }

    [TestMethod]
    public void ARecordWithoutOperators_StillEndsInASemicolon()
    {
        var point = NamespaceBuilder.Get("MyApp").Record("Point").WithParameter<int>("X");

        point.ToString().Should().Contain("public record Point(int X);");
    }

    [TestMethod]
    public void ARecord_RefusesEqualityOperators()
    {
        var point = NamespaceBuilder.Get("MyApp").Record("Point").WithParameter<int>("X");

        var equality = () => point.DefineOperator<bool>(OperatorKind.Equality);

        equality.Should().Throw<InvalidOperationException>()
            .WithMessage("*synthesizes == and !=*declare Equals instead*");
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
