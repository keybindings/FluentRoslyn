using System.Threading.Tasks;
using FluentRoslyn.Builders;

namespace FluentRoslyn.UnitTests.Builders;

[TestClass]
public class AsyncMethodTests
{
    [TestMethod]
    public void Async_TaskReturn_EmitsAsyncKeyword()
    {
        var mb = NewClass().DefineMethod<Task>("SaveAsync").Async().AddStatement("await _db.SaveAsync();");

        mb.ToString().Should().Be(string.Join("\n",
            "public async System.Threading.Tasks.Task SaveAsync()",
            "{",
            "    await _db.SaveAsync();",
            "}"));
    }

    [TestMethod]
    public void Async_GenericTaskReturn_WithExpressionBody()
    {
        var mb = NewClass().DefineMethod<Task<int>>("CountAsync").Async().AsExpressionBody("await _db.CountAsync()");

        mb.ToString().Should().Be(
            "public async System.Threading.Tasks.Task<int> CountAsync() => await _db.CountAsync();");
    }

    [TestMethod]
    public void Async_Void_IsAllowed()
    {
        // async void is legal (event handlers), if discouraged.
        var mb = NewClass().DefineMethod("OnFired").Async().AddStatement("await Task.Yield();");

        mb.ToString().Should().StartWith("public async void OnFired()");
    }

    [TestMethod]
    public void Async_AfterStatic_EmitsInCanonicalOrder()
    {
        var mb = NewClass().DefineMethod<Task>("RunAsync").Static().Async().AddStatement("await Task.Yield();");

        mb.ToString().Should().StartWith("public static async System.Threading.Tasks.Task RunAsync()");
    }

    [TestMethod]
    public void Async_AfterOverride_EmitsInCanonicalOrder()
    {
        var mb = NewClass().DefineMethod<Task>("RunAsync").Override().Async().AddStatement("await Task.Yield();");

        mb.ToString().Should().StartWith("public override async System.Threading.Tasks.Task RunAsync()");
    }

    [TestMethod]
    public void Async_WithRawReturnType_IsAccepted()
    {
        // A named return type is not second-guessed, so ValueTask, IAsyncEnumerable and
        // custom awaitables all pass.
        var mb = NewClass().DefineMethod("ReadAsync").Returns("ValueTask<int>").Async().AsExpressionBody("default");

        mb.ToString().Should().Be("public async ValueTask<int> ReadAsync() => default;");
    }

    [TestMethod]
    public void Async_CustomAwaitable_IsAccepted()
    {
        var mb = NewClass().DefineMethod("WaitAsync").Returns("MyAwaitable").Async().AsExpressionBody("default");

        mb.ToString().Should().Contain("async MyAwaitable WaitAsync()");
    }

    [DataTestMethod]
    [DataRow("int")]
    [DataRow("bool")]
    [DataRow("string")]
    public void Async_NonAwaitableBuiltInReturn_Throws(string keyword)
    {
        var mb = NewClass().DefineMethod("Bad").Returns(keyword).Async().AsExpressionBody("default");

        var act = () => mb.ToString();

        act.Should().Throw<InvalidOperationException>().WithMessage("*cannot return*");
    }

    [TestMethod]
    public void AsyncAbstract_Throws()
    {
        var cb = NamespaceBuilder.Get("TestNamespace").Class("Svc").Abstract();
        var mb = cb.DefineMethod<Task>("RunAsync").Abstract().Async();

        var act = () => mb.ToString();

        act.Should().Throw<InvalidOperationException>().WithMessage("*abstract and async*");
    }

    [TestMethod]
    public void Async_ReachesClassOutput()
    {
        var cb = NewClass();
        cb.DefineMethod<Task>("SaveAsync").Async().AddStatement("await _db.SaveAsync();");

        cb.ToString().Should().Contain("public async System.Threading.Tasks.Task SaveAsync()");
    }

    private static ClassBuilder NewClass()
        => NamespaceBuilder.Get("TestNamespace").Class("Svc");
}
