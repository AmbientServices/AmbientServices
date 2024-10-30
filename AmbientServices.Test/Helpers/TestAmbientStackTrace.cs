using AmbientServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

#nullable enable

namespace AmbientServices.Test;

[TestClass]
public class TestAmbientStackTrace
{
    class Subscriber : IStackTraceUpdateSink
    {
        private ImmutableStack<string> _stackTrace = ImmutableStack<string>.Empty;

        public ImmutableStack<string> StackTrace => _stackTrace;

        public void OnStackTraceUpdated(ImmutableStack<string> stackTrace)
        {
            _stackTrace = stackTrace;
        }
    }
    [TestMethod]
    public void AmbientStackTraceTest()
    {
        Subscriber s = new();
        Assert.AreEqual(0, s.StackTrace.Count());
        AmbientStackTrace.Reset(s, "baseline");
        using IDisposable stackTrace = AmbientStackTrace.Trace();
        Assert.AreEqual(2, s.StackTrace.Count());
        using IDisposable stackTrace2 = AmbientStackTrace.Trace();
        Assert.AreEqual(3, s.StackTrace.Count());
    }
}
