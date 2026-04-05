using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientServices.Test;

/// <summary>
/// Summary description for StatusTest.
/// </summary>
[TestClass]
public class TestAmbientLogSplitter
{
    [TestMethod]
    public async Task AmbientLogSplitter()
    {
        using (AmbientClock.Pause())
        {
            AmbientLogSplitter logger = new();
            // log a message that will go to zero loggers
            logger.Log("test1");
            await logger.Flush();
            logger.AddLogger(AmbientConsoleLogger.Instance);
            // log a message that will go to one logger
            logger.Log("test2");
            await logger.Flush();
            logger.AddLogger(AmbientTraceLogger.Instance);
            // log a message that will go to three loggers
            logger.Log("test3");
            await logger.Flush();

            logger.RemoveLogger(AmbientConsoleLogger.Instance);
            logger.RemoveLogger(AmbientTraceLogger.Instance);
        }
    }

    [TestMethod]
    public void AmbientLogSplitter_LogObject_NullThrows()
    {
        AmbientLogSplitter splitter = new();
        Assert.ThrowsExactly<ArgumentNullException>(() => splitter.Log((object)null!));
    }

    [TestMethod]
    public async Task AmbientLogSplitter_AddSimpleLogger_StringAndStructuredRoundTrip()
    {
        AmbientLogSplitter splitter = new();
        CapturingStringLogger simple = new();
        CapturingDualLogger dual = new();

        splitter.AddSimpleLogger(simple);
        splitter.Log("a");
        Assert.AreEqual("a", simple.Messages[0]);

        splitter.AddSimpleLogger(dual);
        splitter.Log("b");
        splitter.Log(new LogSplitterPayload(42));
        Assert.AreEqual("b", dual.StringMessages[0]);
        CollectionAssert.AreEqual(new[] { "a", "b" }, simple.Messages);
        Assert.AreEqual(1, dual.StructuredMessages.Count);
        Assert.IsInstanceOfType(dual.StructuredMessages[0], typeof(LogSplitterPayload));

        splitter.RemoveSimpleLogger(dual);
        splitter.Log("c");
        splitter.Log(new LogSplitterPayload(99));
        CollectionAssert.AreEqual(new[] { "a", "b", "c" }, simple.Messages);
        Assert.AreEqual(1, dual.StringMessages.Count);
        Assert.AreEqual(1, dual.StructuredMessages.Count);

        await splitter.Flush();
    }

    [TestMethod]
    public async Task AmbientLogSplitter_ToString_JoinsChildRepresentations()
    {
        AmbientLogSplitter splitter = new();
        splitter.AddSimpleLogger(new NamedLogger("L1"));
        splitter.AddLogger(new NamedStructuredLogger("S1"));
        string s = splitter.ToString();
        StringAssert.Contains(s, "L1");
        StringAssert.Contains(s, "S1");
        await splitter.Flush();
    }

    private sealed class CapturingStringLogger : IAmbientLogger
    {
        public List<string> Messages { get; } = new();
        public void Log(string message) => Messages.Add(message);
        public ValueTask Flush(CancellationToken cancel = default) => ValueTask.CompletedTask;
    }

    private sealed class CapturingDualLogger : IAmbientLogger, IAmbientStructuredLogger
    {
        public List<string> StringMessages { get; } = new();
        public List<object> StructuredMessages { get; } = new();
        public void Log(string message) => StringMessages.Add(message);
        public void Log(object structuredData) => StructuredMessages.Add(structuredData);
        public ValueTask Flush(CancellationToken cancel = default) => ValueTask.CompletedTask;
    }

    private sealed class NamedLogger : IAmbientLogger
    {
        private readonly string _name;
        public NamedLogger(string name) => _name = name;
        public void Log(string message) { }
        public ValueTask Flush(CancellationToken cancel = default) => ValueTask.CompletedTask;
        public override string ToString() => _name;
    }

    private sealed class NamedStructuredLogger : IAmbientStructuredLogger, IAmbientLogger
    {
        private readonly string _name;
        public NamedStructuredLogger(string name) => _name = name;
        public void Log(object structuredData) { }
        public void Log(string message) { }
        public ValueTask Flush(CancellationToken cancel = default) => ValueTask.CompletedTask;
        public override string ToString() => _name;
    }

    private sealed class LogSplitterPayload
    {
        public int Id { get; }
        public LogSplitterPayload(int id) => Id = id;
    }
}
