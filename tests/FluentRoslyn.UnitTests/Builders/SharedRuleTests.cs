using System;
using FluentRoslyn.Abstractions;
using FluentRoslyn.Builders;

namespace FluentRoslyn.UnitTests.Builders;

[EmitsAs("MyApp.Inner")]
internal sealed class InnerPlaceholder;

/// <summary>
/// The duplication findings from Review 3 (R3-56 through R3-61). Duplication is not
/// directly testable, so what these pin is the behaviour that duplication was hiding: a
/// message that named no member, two forms of one call that could drift apart, a fact
/// tracked twice and desynced by a public method, and public surface that nothing
/// exercised.
/// </summary>
[TestClass]
public class SharedRuleTests
{
    // R3-56. The accessor copy of the expression-body rule named no member at all, so a
    // generator author was told "an accessor" somewhere had two bodies.
    [TestMethod]
    public void AccessorWithTwoBodies_NamesTheMember()
    {
        var property = NamespaceBuilder.Get("MyApp").Class("C")
            .DefineProperty<int>("Count")
            .WithGetterExpression("_count")
            .WithGetterBody("return _count;");

        var emit = () => property.ToString();

        emit.Should().Throw<InvalidOperationException>()
            .WithMessage("*Getter of 'Count' cannot have both an expression body and a statement body*");
    }

    [TestMethod]
    public void SetterWithTwoBodies_NamesTheMember()
    {
        var property = NamespaceBuilder.Get("MyApp").Class("C")
            .DefineProperty<int>("Count")
            .WithGetterExpression("_count")
            .WithSetterExpression("_count = value")
            .WithSetterBody("_count = value;");

        var emit = () => property.ToString();

        emit.Should().Throw<InvalidOperationException>()
            .WithMessage("*Setter of 'Count' cannot have both an expression body and a statement body*");
    }

    [DataTestMethod]
    [DataRow("method")]
    [DataRow("constructor")]
    public void ExpressionBodyPlusStatements_RefusedTheSameWay(string kind)
    {
        var c = NamespaceBuilder.Get("MyApp").Class("C");

        Action emit = kind == "method"
            ? () => c.DefineMethod("M").AddStatement("var x = 1;").AsExpressionBody("Nothing()").ToString()
            : () => c.DefineConstructor().AddStatement("var x = 1;").AsExpressionBody("Nothing()").ToString();

        emit.Should().Throw<InvalidOperationException>()
            .WithMessage("*cannot have both an expression body and statements*");
    }

    // R3-57. The statement and value forms of a handle call are the same expression, which
    // two doc comments asserted while one of them hand-built its own.
    [TestMethod]
    public void CallAndInvoke_EmitTheSameExpression()
    {
        var inner = NamespaceBuilder.Get("MyApp").Class("Inner");
        inner.DefineMethod<int>("Measure").WithParameter<string>("text", out _)
            .AsFunction<string>(out var measureValue);
        inner.DefineMethod("Measure2").WithParameter<string>("text", out _)
            .AsCallable<string>(out var measureCall);

        var host = NamespaceBuilder.Get("MyApp").Class("Host");
        var target = host.DefineProperty<InnerPlaceholder>("Current");
        var size = host.DefineField<int>("_size");
        host.DefineMethod("Refresh")
            .WithParameter<string>("text", out var text)
            .Assign(size, target.Invoke(measureValue, text))
            .Call(target, measureCall, text);

        var source = host.ToString();

        source.Should().Contain("_size = Current.Measure(text);")
            .And.Contain("Current.Measure2(text);");
    }

    // R3-58. The two member paths were character-identical, including the CanNameOf
    // recursion that decides whether ThrowIfNull refuses to emit. Both arms still agree.
    [TestMethod]
    public void RawAndTypedMemberPaths_AgreeOnNameOf()
    {
        var host = NamespaceBuilder.Get("MyApp").Class("Host");
        var inner = host.DefineProperty<string[]>("Items");

        var typed = () => host.DefineMethod("A").ThrowIfNull(inner.Item(0).MemberNamed<string>("Value")).ToString();
        var raw = () => host.DefineMethod("B").ThrowIfNullRaw(inner.Item(0).MemberRaw("Value")).ToString();

        typed.Should().Throw<InvalidOperationException>();
        raw.Should().Throw<InvalidOperationException>();
    }

    [TestMethod]
    public void RawAndTypedMemberPaths_ComposeTheSameAccess()
    {
        var host = NamespaceBuilder.Get("MyApp").Class("Host");
        var inner = host.DefineProperty<string>("Inner");
        var count = host.DefineProperty("Count", "int");

        host.DefineMethod("A").AssignLiteral(inner.MemberNamed<int>("Length"), 1);
        host.DefineMethod("B").AssignRaw(count, inner.MemberRaw("Length"));

        host.ToString().Should().Contain("Inner.Length = 1;").And.Contain("Count = Inner.Length;");
    }

    // R3-61. ReturnsVoid was a second copy of a fact ReturnType already carried, and the
    // route that desynced them -- Returns("void") -- turns out to be closed one level
    // earlier: ParseTypeName("void") carries diagnostics, so SyntaxParse refuses it. The
    // finding was latent for a reason it did not name, which is worth pinning both ways.
    [TestMethod]
    public void ReturnsVoidByName_IsRefusedByTheParser()
    {
        var method = NamespaceBuilder.Get("MyApp").Class("C").DefineMethod("M");

        var set = () => method.Returns("void");

        set.Should().Throw<ArgumentException>().WithMessage("*not a valid C# type name*");
    }

    [TestMethod]
    public void AVoidMethod_AcceptsABareReturnAndRefusesAValue()
    {
        var method = NamespaceBuilder.Get("MyApp").Class("C").DefineMethod("M");

        method.Return();
        var write = () => method.Return(Value.Literal(1));

        write.Should().Throw<InvalidOperationException>().WithMessage("*returns void*");
        method.ToString().Should().Contain("void M()").And.Contain("return;");
    }

    [TestMethod]
    public void ReturnsByName_MakesTheMethodNonVoid()
    {
        var method = NamespaceBuilder.Get("MyApp").Class("C").DefineMethod("M").Returns("int");

        var bare = () => method.Return();
        var emit = () => method.ToString();

        bare.Should().Throw<InvalidOperationException>().WithMessage("*has a return type*");
        emit.Should().Throw<InvalidOperationException>().WithMessage("*needs a body*");
    }

    // Returns(builder) is the other write site the flag used to have, and it has to agree
    // with the type it just set.
    [TestMethod]
    public void ReturnsABuilder_MakesTheMethodNonVoid()
    {
        var inner = NamespaceBuilder.Get("MyApp").Class("Inner");
        var method = NamespaceBuilder.Get("MyApp").Class("C").DefineMethod("M").Returns(inner);

        var bare = () => method.Return();

        bare.Should().Throw<InvalidOperationException>().WithMessage("*has a return type*");
    }

    // R3-61's second half: two pieces of public surface with no call site anywhere, so the
    // whole global-namespace branch was unexercised.
    [TestMethod]
    public void SourceFileTypes_ListsWhatWasDeclared()
    {
        var file = SourceFile.InNamespace("MyApp");
        var first = file.Class("A");
        var second = file.Record("B");

        file.Types.Should().Equal(first, second);
    }

    [TestMethod]
    public void InGlobalNamespace_EmitsNoNamespaceDeclaration()
    {
        var file = SourceFile.InGlobalNamespace();
        file.Class("Widget").DefineProperty<int>("Id");

        var source = file.ToString();

        source.Should().StartWith("public class Widget").And.NotContain("namespace");
        Compiled.Errors(source).Should().BeEmpty();
    }

    [TestMethod]
    public void InGlobalNamespace_UnderSimplifyTypeNames_StillImports()
    {
        var file = SourceFile.InGlobalNamespace().SimplifyTypeNames();
        file.Class("Widget").DefineField<System.Text.StringBuilder>("_text");

        var source = file.ToString();

        source.Should().StartWith("using System.Text;").And.Contain("StringBuilder _text");
        Compiled.Errors(source).Should().BeEmpty();
    }
}
