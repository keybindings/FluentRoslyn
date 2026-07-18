using Generatr.Builders;

namespace Generatr.UnitTests.Builders;

/// <summary>
/// Each test here pins a bug that shipped in the string-emission era because no test
/// reached the code path. The syntax-tree rewrite makes several structurally impossible,
/// but they stay pinned as characterization tests.
/// </summary>
[TestClass]
public class RegressionTests
{
    private static ClassBuilder NewClass(string name = "TestClass")
        => NamespaceBuilder.Get("TestNamespace").Class(name);

    #region ClassBuilder member ordering

    // Ordering by the AccessModifier object (rather than its AccessabilityLevel) threw
    // InvalidOperationException, but only once two members forced an actual comparison.
    [TestMethod]
    public void ClassWithTwoFields_Builds_DoesNotThrow()
    {
        var cb = NewClass();
        cb.DefineField<string>("_b", AccessModifier.Private);
        cb.DefineField<string>("_a", AccessModifier.Public);

        var act = () => cb.ToString();

        act.Should().NotThrow();
    }

    [TestMethod]
    public void ClassWithMixedAccessFields_Builds_LeastProtectedFirstThenAlphabetical()
    {
        var cb = NewClass();
        cb.DefineField<int>("_zPrivate", AccessModifier.Private);
        cb.DefineField<int>("_bPublic", AccessModifier.Public);
        cb.DefineField<int>("_aPublic", AccessModifier.Public);
        cb.DefineField<int>("_mInternal", AccessModifier.Internal);

        var value = cb.ToString();

        var expected = string.Join(Environment.NewLine,
            "namespace TestNamespace;",
            "public class TestClass",
            "{",
            "    public int _aPublic;",
            "    public int _bPublic;",
            "    internal int _mInternal;",
            "    private int _zPrivate;",
            "}");
        value.Should().Be(expected);
    }

    #endregion

    #region TypeNameBuilder generic arguments

    // The comma was emitted only for 0 < i < Count-1, so a two-argument generic
    // rendered as Dictionary<intstring>.
    [TestMethod]
    public void TypeName_TwoGenericArguments_SeparatesWithComma()
    {
        TypeNameBuilder.New<Dictionary<int, string>>().ToString()
            .Should().Be("System.Collections.Generic.Dictionary<int, string>");
    }

    [TestMethod]
    public void TypeName_ThreeGenericArguments_SeparatesEveryArgument()
    {
        TypeNameBuilder.New<Tuple<int, string, bool>>().ToString()
            .Should().Be("System.Tuple<int, string, bool>");
    }

    [TestMethod]
    public void TypeName_SingleGenericArgument_Unchanged()
    {
        TypeNameBuilder.New<List<List<string>>>().ToString()
            .Should().Be("System.Collections.Generic.List<System.Collections.Generic.List<string>>");
    }

    #endregion

    #region MethodBuilder

    // Build omitted the space after the access modifier and after the return type,
    // inverted the parameter separator, and dereferenced a null statement body.
    [TestMethod]
    public void Method_NoParameters_BuildsWithSpacingAndEmptyBody()
    {
        var mb = MethodBuilder.Action(NewClass(), "DoThing", AccessModifier.Public, []);

        mb.ToString().Should().StartWith("public void DoThing()");
    }

    [TestMethod]
    public void Method_TwoParameters_SeparatesWithCommaBetweenNotAfter()
    {
        var mb = MethodBuilder.Action(NewClass(), "DoThing", AccessModifier.Public,
            [Parameter<int>.New("count"), Parameter<string>.New("name")]);

        mb.ToString().Should().StartWith("public void DoThing(int count, string name)");
    }

    [TestMethod]
    public void Method_SingleParameter_HasNoTrailingComma()
    {
        var mb = MethodBuilder.Action(NewClass(), "DoThing", AccessModifier.Public,
            [Parameter<int>.New("count")]);

        mb.ToString().Should().StartWith("public void DoThing(int count)");
    }

    [TestMethod]
    public void Method_NoStatements_EmitsEmptyBlockWithoutThrowing()
    {
        var mb = MethodBuilder.Action(NewClass(), "DoThing", AccessModifier.Public, []);

        var act = () => mb.ToString();

        act.Should().NotThrow();
        mb.ToString().Should().Contain("{").And.Contain("}");
    }

    #endregion

    #region PropertyBuilder

    // _getMethodBuilder was never assigned, so Build was an unconditional NRE.
    [TestMethod]
    public void Property_AutoProperty_BuildsGetAndSet()
    {
        var pb = new PropertyBuilder<int>(NewClass(), "Count", AccessModifier.Public);

        pb.ToString().Should().Be("public int Count { get; set; }");
    }

    [TestMethod]
    public void Property_StaticAutoProperty_BuildsStaticKeyword()
    {
        var pb = new PropertyBuilder<string>(NewClass(), "Name", AccessModifier.Private) { IsStatic = true };

        pb.ToString().Should().Be("private static string Name { get; set; }");
    }

    [TestMethod]
    public void Property_NonAutoProperty_Throws()
    {
        var pb = new PropertyBuilder<int>(NewClass(), "Count", AccessModifier.Public) { IsAutoProperty = false };

        var act = () => pb.ToString();

        act.Should().Throw<NotImplementedException>();
    }

    #endregion
}
