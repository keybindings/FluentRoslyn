using System;
using FluentRoslyn.Abstractions;
using FluentRoslyn.Builders;

namespace FluentRoslyn.UnitTests.Builders;

/// <summary>
/// Covers members whose type is named by text rather than by a type argument —
/// <c>DefineField(name, typeName)</c> and <c>WithParameter(name, typeName)</c>. These
/// exist for the case a generator hits constantly and the typed surface cannot reach:
/// the consumer's own types, which are only ever <c>ISymbol</c>s discovered at
/// generation time.
/// </summary>
[TestClass]
public class RawTypedMemberTests
{
    [TestMethod]
    public void DefineField_WithANamedType_EmitsIt()
    {
        var builder = NamespaceBuilder.Get("MyApp").Class("PersonBuilder");

        builder.DefineField("_name", "global::Consumer.Models.Name");

        builder.ToString().Should().Contain("private global::Consumer.Models.Name _name;");
    }

    [TestMethod]
    public void DefineField_WithAGenericNamedType_EmitsIt()
    {
        var builder = NamespaceBuilder.Get("MyApp").Class("PersonBuilder");

        builder.DefineField("_tags", "global::System.Collections.Generic.List<string>").Readonly();

        builder.ToString().Should()
            .Contain("private readonly global::System.Collections.Generic.List<string> _tags;");
    }

    // The whole fluent surface still applies -- only the type is text.
    [TestMethod]
    public void DefineField_WithANamedType_KeepsTheFluentSurface()
    {
        var builder = NamespaceBuilder.Get("MyApp").Class("PersonBuilder");

        builder.DefineField("_count", "int", AccessModifier.Internal)
            .Static()
            .WithSummary("How many.")
            .WithInitializerExpression("0");

        var code = builder.ToString();

        code.Should()
            .Contain("internal static int _count = 0;").And
            .Contain("How many.");
    }

    [TestMethod]
    public void WithParameter_WithANamedType_EmitsIt()
    {
        var builder = NamespaceBuilder.Get("MyApp").Class("PersonBuilder");

        builder.DefineMethod("WithName")
            .WithParameter("name", "global::Consumer.Models.Name")
            .AddStatement("_name = name;");

        builder.ToString().Should().Contain("void WithName(global::Consumer.Models.Name name)");
    }

    [TestMethod]
    public void WithParameter_WithANamedType_MixesWithTypedParameters()
    {
        var builder = NamespaceBuilder.Get("MyApp").Class("PersonBuilder");

        builder.DefineMethod("Set")
            .WithParameter<int>("id")
            .WithParameter("name", "global::Consumer.Models.Name");

        builder.ToString().Should().Contain("void Set(int id, global::Consumer.Models.Name name)");
    }

    [TestMethod]
    public void ANamedTypeConstructorParameter_Emits()
    {
        var builder = NamespaceBuilder.Get("MyApp").Class("PersonBuilder");

        builder.DefineConstructor(AccessModifier.Public)
            .WithParameter("seed", "global::Consumer.Models.Person");

        builder.ToString().Should().Contain("public PersonBuilder(global::Consumer.Models.Person seed)");
    }

    // The cost of the escape hatch, asserted rather than merely documented. A raw-typed
    // field is a named member, so it is an untyped IReference — that is what lets it be
    // an argument to Value.NewOfType and be shadow-qualified. What it is not is an
    // IReference<T>: there is no T to check against, so the typed surface — Assign,
    // Call, ThrowIfNull — cannot reach it, and every one of those needs IReference<T>.
    [TestMethod]
    public void ARawTypedField_IsAnUntypedReferenceOnly()
    {
        typeof(RawFieldBuilder).Should().BeAssignableTo<IReference>();
        typeof(RawFieldBuilder).Should().NotBeAssignableTo(typeof(IReference<>));

        typeof(RawFieldBuilder).GetInterfaces()
            .Should().NotContain(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IReference<>));

        typeof(FieldBuilder<int>).Should().BeAssignableTo<IReference<int>>();
    }

    [TestMethod]
    public void AMalformedTypeName_IsRejected()
    {
        var builder = NamespaceBuilder.Get("MyApp").Class("PersonBuilder");

        var field = () => builder.DefineField("_bad", "not a type<");
        var parameter = () => builder.DefineMethod("M").WithParameter("bad", "not a type<");

        field.Should().Throw<ArgumentException>();
        parameter.Should().Throw<ArgumentException>();
    }

    // Two same-typed string arguments can be transposed, and the compiler cannot catch
    // it. The name is validated as a C# identifier, which a qualified type name is not,
    // so the common transposition is caught rather than emitted.
    [TestMethod]
    public void TransposedArguments_AreRejected()
    {
        var builder = NamespaceBuilder.Get("MyApp").Class("PersonBuilder");

        var field = () => builder.DefineField("global::Consumer.Models.Name", "_name");
        var parameter = () => builder.DefineMethod("M").WithParameter("global::Consumer.Models.Name", "name");

        field.Should().Throw<ArgumentException>();
        parameter.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void RawAndTypedFields_CoexistInOneType()
    {
        var builder = NamespaceBuilder.Get("MyApp").Class("PersonBuilder");

        builder.DefineField<int>("_id");
        builder.DefineField("_name", "global::Consumer.Models.Name");

        var code = builder.ToString();

        code.Should()
            .Contain("private int _id;").And
            .Contain("private global::Consumer.Models.Name _name;");
    }
}
