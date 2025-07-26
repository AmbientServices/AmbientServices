using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices.Test;

[TestClass]
public class TestContextMutator
{
    private static AsyncLocal<string> _contextValue = new();
    private static AsyncLocal<string> _temporaryContextValue = new();

    [TestMethod]
    public async Task ContextMutatorAction()
    {
        _contextValue.Value = nameof(ContextMutatorAction);
        Assert.AreEqual(nameof(ContextMutatorAction), _contextValue.Value);
        await InnerContext1();
        Assert.AreNotEqual(nameof(AsyncTestAction1), _contextValue.Value);
        Assert.AreEqual(nameof(ContextMutatorAction), _contextValue.Value);
        await InnerContext2();
        Assert.AreNotEqual(nameof(AsyncTestAction2), _contextValue.Value);
        Assert.AreEqual(nameof(ContextMutatorAction), _contextValue.Value);
        (await AsyncTestAction1()).ApplyContextChanges();
        Assert.AreEqual(nameof(AsyncTestAction1), _contextValue.Value);
        (await AsyncTestAction2()).ApplyContextChanges();
        Assert.AreEqual(nameof(AsyncTestAction2), _contextValue.Value);
        Assert.AreEqual("test1", (await AsyncTestFunc1()).ApplyContextChanges());
        Assert.AreEqual(nameof(AsyncTestFunc1), _contextValue.Value);
        Assert.AreEqual("test2", (await AsyncTestFunc2()).ApplyContextChanges());
        Assert.AreEqual(nameof(AsyncTestFunc2), _contextValue.Value);
    }
    [TestMethod]
    public async Task TemporaryContextMutatorAction()
    {
        _temporaryContextValue.Value = nameof(TemporaryContextMutatorAction);
        Assert.AreEqual(nameof(TemporaryContextMutatorAction), _temporaryContextValue.Value);
        using (TemporaryContextMutator tcm = (await TemporarayAsyncTestAction()).ApplyContextChanges())
        {
            Assert.AreEqual(nameof(TemporarayAsyncTestAction), _temporaryContextValue.Value);
        }
        Assert.AreEqual(nameof(TemporaryContextMutatorAction), _temporaryContextValue.Value);
    }
    private static async ValueTask InnerContext1()
    {
        _contextValue.Value = nameof(InnerContext1);
        await Task.Delay(100); // Simulate some asynchronous operation
    }
    private static async ValueTask InnerContext2()
    {
        await Task.Delay(100); // Simulate some asynchronous operation
        _contextValue.Value = nameof(InnerContext2);
    }
    private static async ValueTask<ContextMutator> AsyncTestAction1()
    {
        ContextMutator mutator = new(() => _contextValue.Value = nameof(AsyncTestAction1));
        await Task.Delay(100); // Simulate some asynchronous operation
        return mutator;
    }
    private static async ValueTask<ContextMutator> AsyncTestAction2()
    {
        await Task.Delay(100); // Simulate some asynchronous operation
        ContextMutator mutator = new(() => _contextValue.Value = nameof(AsyncTestAction2));
        return mutator;
    }
    private static async ValueTask<TemporaryContextMutator> TemporarayAsyncTestAction()
    {
        string oldValue = "not set";
        TemporaryContextMutator mutator = new(() => { oldValue = _temporaryContextValue.Value; _temporaryContextValue.Value = nameof(TemporarayAsyncTestAction); }, () => { _temporaryContextValue.Value = oldValue; });
        await Task.Delay(100); // Simulate some asynchronous operation
        return mutator;
    }
    private static async ValueTask<ContextMutator<string>> AsyncTestFunc1()
    {
        ContextMutator<string> mutator = new(() => { _contextValue.Value = nameof(AsyncTestFunc1); return "test1"; });
        await Task.Delay(100); // Simulate some asynchronous operation
        return mutator;
    }
    private static async ValueTask<ContextMutator<string>> AsyncTestFunc2()
    {
        await Task.Delay(100); // Simulate some asynchronous operation
        ContextMutator<string> mutator = new(() => { _contextValue.Value = nameof(AsyncTestFunc2); return "test2"; });
        return mutator;
    }
}
