using System;
using FluentRoslyn.Builders;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FluentRoslyn.UnitTests.Builders;

/// <summary>
/// R3-23 (NaN/±Infinity emit as bare text that binds to nothing — CS0103) and R3-07
/// (negative zero emits as the integer literal <c>-0</c>, which converts to positive zero)
/// both trace back to the float/double case in <c>SyntaxLiterals.Expression</c>. Fixed at
/// that single chokepoint, so these hit it from all four literal paths it feeds: field
/// initializers, <c>AssignLiteral</c>, <c>ReturnLiteral</c> and <c>Value.Literal</c>. Every
/// assertion here reads back the compiler's own constant value rather than the source text,
/// because the whole point of R3-07 is that the text can look right and still be wrong.
/// </summary>
[TestClass]
public class SpecialFloatingPointLiteralTests
{
    [TestMethod]
    public void WithInitializer_Double_BindsAndRoundTripsNaNAndInfinity()
    {
        var file = SourceFile.InNamespace("MyApp");
        var c = file.Class("Constants");
        c.DefineField<double>("NaNField", AccessModifier.Public).Const().WithInitializer(double.NaN);
        c.DefineField<double>("PosInf", AccessModifier.Public).Const().WithInitializer(double.PositiveInfinity);
        c.DefineField<double>("NegInf", AccessModifier.Public).Const().WithInitializer(double.NegativeInfinity);

        var source = file.ToString();

        Compiled.Errors(source).Should().BeEmpty();
        Compiled.ConstantValueOf(source, FieldInitializer("NaNField")).Should().Be(double.NaN);
        Compiled.ConstantValueOf(source, FieldInitializer("PosInf")).Should().Be(double.PositiveInfinity);
        Compiled.ConstantValueOf(source, FieldInitializer("NegInf")).Should().Be(double.NegativeInfinity);
    }

    [TestMethod]
    public void WithInitializer_Float_BindsAndRoundTripsNaNAndInfinity()
    {
        var file = SourceFile.InNamespace("MyApp");
        var c = file.Class("Constants");
        c.DefineField<float>("NaNField", AccessModifier.Public).Const().WithInitializer(float.NaN);
        c.DefineField<float>("PosInf", AccessModifier.Public).Const().WithInitializer(float.PositiveInfinity);
        c.DefineField<float>("NegInf", AccessModifier.Public).Const().WithInitializer(float.NegativeInfinity);

        var source = file.ToString();

        Compiled.Errors(source).Should().BeEmpty();
        Compiled.ConstantValueOf(source, FieldInitializer("NaNField")).Should().Be(float.NaN);
        Compiled.ConstantValueOf(source, FieldInitializer("PosInf")).Should().Be(float.PositiveInfinity);
        Compiled.ConstantValueOf(source, FieldInitializer("NegInf")).Should().Be(float.NegativeInfinity);
    }

    [TestMethod]
    public void WithInitializer_NegativeZero_StaysConstAndPreservesSign()
    {
        var file = SourceFile.InNamespace("MyApp");
        var c = file.Class("Constants");
        // Const(), specifically: the emitted form must remain a compile-time constant
        // expression, which a method call to recover the sign bit would not be.
        c.DefineField<double>("NegZeroD", AccessModifier.Public).Const().WithInitializer(-0.0);
        c.DefineField<float>("NegZeroF", AccessModifier.Public).Const().WithInitializer(-0f);

        var source = file.ToString();

        Compiled.Errors(source).Should().BeEmpty();

        var d = (double)Compiled.ConstantValueOf(source, FieldInitializer("NegZeroD"));
        var f = (float)Compiled.ConstantValueOf(source, FieldInitializer("NegZeroF"));
        double.IsNegative(d).Should().BeTrue();
        float.IsNegative(f).Should().BeTrue();
    }

    [TestMethod]
    public void AssignLiteral_BindsAndRoundTripsNaNAndNegativeZero()
    {
        var file = SourceFile.InNamespace("MyApp");
        var c = file.Class("Widget");
        var ratio = c.DefineProperty<double>("Ratio");
        var offset = c.DefineProperty<double>("Offset");
        c.DefineConstructor()
            .AssignLiteral(ratio, double.NaN)
            .AssignLiteral(offset, -0.0);

        var source = file.ToString();

        Compiled.Errors(source).Should().BeEmpty();
        Compiled.ConstantValueOf(source, AssignmentTo("Ratio")).Should().Be(double.NaN);

        var offsetValue = (double)Compiled.ConstantValueOf(source, AssignmentTo("Offset"));
        double.IsNegative(offsetValue).Should().BeTrue();
    }

    [TestMethod]
    public void ReturnLiteral_BindsAndRoundTripsInfinityAndNegativeZero()
    {
        var file = SourceFile.InNamespace("MyApp");
        var c = file.Class("Widget");
        c.DefineMethod<float>("PosInf").ReturnLiteral(float.PositiveInfinity);
        c.DefineMethod<float>("NegZero").ReturnLiteral(-0f);

        var source = file.ToString();

        Compiled.Errors(source).Should().BeEmpty();
        Compiled.ConstantValueOf(source, ReturnFrom("PosInf")).Should().Be(float.PositiveInfinity);

        var negZero = (float)Compiled.ConstantValueOf(source, ReturnFrom("NegZero"));
        float.IsNegative(negZero).Should().BeTrue();
    }

    [TestMethod]
    public void ValueLiteral_BindsAndRoundTripsNegativeInfinityAndNegativeZero()
    {
        var file = SourceFile.InNamespace("MyApp");
        var c = file.Class("Widget");
        c.DefineMethod("LogNegInf")
            .CallStatic(typeof(Console), nameof(Console.WriteLine), Value.Literal(double.NegativeInfinity));
        c.DefineMethod("LogNegZero")
            .CallStatic(typeof(Console), nameof(Console.WriteLine), Value.Literal(-0.0));

        var source = file.ToString();

        Compiled.Errors(source).Should().BeEmpty();
        Compiled.ConstantValueOf(source, ArgumentIn("LogNegInf")).Should().Be(double.NegativeInfinity);

        var negZero = (double)Compiled.ConstantValueOf(source, ArgumentIn("LogNegZero"));
        double.IsNegative(negZero).Should().BeTrue();
    }

    private static Func<SyntaxNode, ExpressionSyntax> FieldInitializer(string fieldName) =>
        node => node is VariableDeclaratorSyntax { Initializer: { } initializer } declarator
                && declarator.Identifier.Text == fieldName
            ? initializer.Value
            : null;

    private static Func<SyntaxNode, ExpressionSyntax> AssignmentTo(string memberName) =>
        node => node is AssignmentExpressionSyntax { Left: IdentifierNameSyntax target } assignment
                && target.Identifier.Text == memberName
            ? assignment.Right
            : null;

    private static Func<SyntaxNode, ExpressionSyntax> ReturnFrom(string methodName) =>
        node => node is ReturnStatementSyntax { Expression: { } expression } statement
                && statement.Ancestors().OfType<MethodDeclarationSyntax>().First().Identifier.Text == methodName
            ? expression
            : null;

    private static Func<SyntaxNode, ExpressionSyntax> ArgumentIn(string methodName) =>
        node => node is InvocationExpressionSyntax { ArgumentList.Arguments: [{ Expression: { } expression }] } invocation
                && invocation.Ancestors().OfType<MethodDeclarationSyntax>().First().Identifier.Text == methodName
            ? expression
            : null;
}
