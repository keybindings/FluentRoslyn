using Generatr.Builders;

namespace Generatr.UnitTests.Builders;

[TestClass]
public class MethodBuilderTests
{
    [TestMethod]
    public void DefineMethod_VoidNoBody_EmitsEmptyBlock()
    {
        var mb = NewClass().DefineMethod("DoThing");

        mb.ToString().Should().Be(string.Join("\n",
            "public void DoThing()",
            "{",
            "}"));
    }

    [TestMethod]
    public void Static_EmitsStaticKeyword()
    {
        var mb = NewClass().DefineMethod("DoThing").Static();

        mb.ToString().Should().StartWith("public static void DoThing()");
    }

    [TestMethod]
    public void WithAccessModifier_OverridesTheAccessModifier()
    {
        var mb = NewClass().DefineMethod("DoThing").WithAccessModifier(AccessModifier.Private);

        mb.ToString().Should().StartWith("private void DoThing()");
    }

    [TestMethod]
    public void WithParameter_AppendsParameters()
    {
        var mb = NewClass().DefineMethod("DoThing")
            .WithParameter<int>("count")
            .WithParameter<string>("name");

        mb.ToString().Should().StartWith("public void DoThing(int count, string name)");
    }

    [TestMethod]
    public void WithParameter_ComposesWithDefineMethodParameters()
    {
        var mb = NewClass().DefineMethod("DoThing", AccessModifier.Public, Parameter<int>.New("count"))
            .WithParameter<string>("name");

        mb.ToString().Should().StartWith("public void DoThing(int count, string name)");
    }

    [TestMethod]
    public void VoidExpressionBody_EmitsArrowWithSemicolon()
    {
        var mb = NewClass().DefineMethod("DoThing").AsExpressionBody("System.Console.WriteLine(\"x\")");

        mb.ToString().Should().Be("public void DoThing() => System.Console.WriteLine(\"x\");");
    }

    [TestMethod]
    public void ReturningMethod_WithExpressionBody_EmitsReturnTypeAndArrow()
    {
        var mb = NewClass().DefineMethod<int>("Add")
            .WithParameter<int>("a")
            .WithParameter<int>("b")
            .AsExpressionBody("a + b");

        mb.ToString().Should().Be("public int Add(int a, int b) => a + b;");
    }

    [TestMethod]
    public void ReturningMethod_GenericReturnType_QualifiesTheType()
    {
        var mb = NewClass().DefineMethod<System.Collections.Generic.List<int>>("Make")
            .AsExpressionBody("new()");

        mb.ToString().Should().Be("public System.Collections.Generic.List<int> Make() => new();");
    }

    [TestMethod]
    public void ReturningMethod_WithoutBody_Throws()
    {
        var mb = NewClass().DefineMethod<int>("Add");

        var act = () => mb.ToString();

        act.Should().Throw<System.InvalidOperationException>().WithMessage("*needs a body*");
    }

    [TestMethod]
    public void AddStatement_VoidMethod_EmitsStatementsInBlock()
    {
        var mb = NewClass().DefineMethod("DoThing")
            .AddStatement("var x = 1;")
            .AddStatement("System.Console.WriteLine(x);");

        mb.ToString().Should().Be(string.Join("\n",
            "public void DoThing()",
            "{",
            "    var x = 1;",
            "    System.Console.WriteLine(x);",
            "}"));
    }

    [TestMethod]
    public void WithBody_ReturningMethod_EmitsReturnStatement()
    {
        var mb = NewClass().DefineMethod<int>("Add")
            .WithParameter<int>("a")
            .WithParameter<int>("b")
            .WithBody("return a + b;");

        mb.ToString().Should().Be(string.Join("\n",
            "public int Add(int a, int b)",
            "{",
            "    return a + b;",
            "}"));
    }

    [TestMethod]
    public void WithBody_ReplacesPreviouslyAddedStatements()
    {
        var mb = NewClass().DefineMethod("DoThing")
            .AddStatement("var x = 1;")
            .WithBody("var y = 2;");

        mb.ToString().Should().NotContain("x").And.Contain("var y = 2;");
    }

    [TestMethod]
    public void ExpressionBodyAndStatements_Together_Throw()
    {
        var mb = NewClass().DefineMethod<int>("Add")
            .AsExpressionBody("1")
            .AddStatement("return 2;");

        var act = () => mb.ToString();

        act.Should().Throw<System.InvalidOperationException>().WithMessage("*both*");
    }

    [TestMethod]
    public void FluentMethods_MutateInPlace_ReturningTheSameInstance()
    {
        var mb = NewClass().DefineMethod("DoThing");

        mb.Static().Should().BeSameAs(mb);
    }

    [TestMethod]
    public void DefineMethodReturning_ReachesClassOutput()
    {
        var cb = NewClass();
        cb.DefineMethod<int>("Add").WithParameter<int>("a").AsExpressionBody("a + 1");

        cb.ToString().Should().Be(string.Join("\n",
            "namespace TestNamespace;",
            "public class TestClass",
            "{",
            "    public int Add(int a) => a + 1;",
            "}"));
    }

    #region Generics

    [TestMethod]
    public void WithTypeParameter_EmitsTypeParameterList()
    {
        var mb = NewClass().DefineMethod("DoThing").WithTypeParameter("T").WithParameter<int>("x");

        mb.ToString().Should().StartWith("public void DoThing<T>(int x)");
    }

    [TestMethod]
    public void MultipleTypeParameters_EmitInOrder()
    {
        var mb = NewClass().DefineMethod("Map")
            .WithTypeParameter("TIn")
            .WithTypeParameter("TOut")
            .Returns("TOut")
            .AsExpressionBody("default");

        mb.ToString().Should().Be("public TOut Map<TIn, TOut>() => default;");
    }

    [TestMethod]
    public void Returns_GenericTypeParameter_UsedAsReturnType()
    {
        var mb = NewClass().DefineMethod("Get")
            .WithTypeParameter("T")
            .Returns("T")
            .AddStatement("return default;");

        mb.ToString().Should().StartWith("public T Get<T>()");
    }

    [TestMethod]
    public void WithConstraint_EmitsWhereClause()
    {
        var mb = NewClass().DefineMethod("Make")
            .WithTypeParameter("T")
            .Returns("T")
            .WithConstraint("T", "class")
            .WithConstraint("T", "new()")
            .AddStatement("return new T();");

        mb.ToString().Should().Contain("public T Make<T>()")
            .And.Contain("where T : class, new()");
    }

    [TestMethod]
    public void WithConstraint_TypeConstraint_ParsesGenericInterface()
    {
        var mb = NewClass().DefineMethod("Sort")
            .WithTypeParameter("T")
            .WithConstraint("T", "System.IComparable<T>");

        mb.ToString().Should().Contain("where T : System.IComparable<T>");
    }

    [TestMethod]
    public void WithConstraint_MultipleTypeParameters_EmitOneClauseEach()
    {
        var mb = NewClass().DefineMethod("Pair")
            .WithTypeParameter("TKey")
            .WithTypeParameter("TValue")
            .WithConstraint("TKey", "notnull")
            .WithConstraint("TValue", "class");

        var value = mb.ToString();
        value.Should().Contain("where TKey : notnull");
        value.Should().Contain("where TValue : class");
    }

    [TestMethod]
    public void Constraint_WithoutTypeParameter_Throws()
    {
        var mb = NewClass().DefineMethod("DoThing").WithConstraint("T", "class");

        var act = () => mb.ToString();

        act.Should().Throw<System.InvalidOperationException>().WithMessage("*no type parameters*");
    }

    [TestMethod]
    public void Constraint_ForUndeclaredTypeParameter_Throws()
    {
        var mb = NewClass().DefineMethod("DoThing")
            .WithTypeParameter("T")
            .WithConstraint("U", "class");

        var act = () => mb.ToString();

        act.Should().Throw<System.InvalidOperationException>().WithMessage("*undeclared type parameter*");
    }

    #endregion

    private static ClassBuilder NewClass(string name = "TestClass")
        => NamespaceBuilder.Get("TestNamespace").Class(name);
}
