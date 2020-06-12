using AmbientServices;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Threading;

namespace TestAmbientServices
{
    public class LoggerSample
    {
        private static readonly ILogger<LoggerSample> _Logger = Registry<ILogger>.Implementation.GetLogger<LoggerSample>();

        static LoggerSample()
        {
            _Logger?.Log("Startup", "Lifetime", LogLevel.Trace);
        }
    }


    interface ICallStack
    {
        IDisposable Scope(string function);
        IEnumerable<string> Entries { get; }
    }

    [DefaultImplementation]
    public class CallStack : ICallStack
    {
        static private ThreadLocal<Stack<string>> _stack = new ThreadLocal<Stack<string>>();

        static private Stack<string> GetStack()
        {
            Stack<string> stack = _stack.Value;
            if (_stack.Value == null)
            {
                stack = new Stack<string>();
                _stack.Value = stack;
            }
            return stack;
        }

        public IDisposable Scope(string entry)
        {
            Stack<string> stack = GetStack();
            stack.Push(entry);
            return new CallStackEntry(stack);
        }

        public IEnumerable<string> Entries { get { return GetStack(); } }

        class CallStackEntry : IDisposable
        {
            private Stack<string> _stack;

            public CallStackEntry(Stack<string> stack)
            {
                _stack = stack;
            }

            #region IDisposable Support
            private bool _disposedValue = false;

            protected virtual void Dispose(bool disposing)
            {
                if (!_disposedValue)
                {
                    if (disposing)
                    {
                        if (_stack != null)
                        {
                            _stack.Pop();
                            _stack = null;
                        }
                    }
                    _disposedValue = true;
                }
            }
            public void Dispose()
            {
                Dispose(true);
            }
            #endregion
        }
    }
    
    class Setup
    {
        static Setup()
        {
            Registry<ICache>.Implementation = null;
        }
    }

    class SetupApplication
    {
        static SetupApplication()
        {
            Registry<ISettings>.Implementation = new AppConfigSettings();
        }
    }
    class AppConfigSettings : ISettings
    {
        public ISetting<T> GetSetting<T>(string key, T defaultValue = default(T), Func<string, T> convert = null)
        {
            return new AppConfigSetting<T>(key, defaultValue, convert);
        }
        class AppConfigSetting<T> : ISetting<T>
        {
            private T _value;
            private string _name;
            private T _defaultValue;
            private Func<string, T> _convert;
            public AppConfigSetting(string name, T defaultValue = default(T), Func<string, T> convert = null)
            {
                _name = name;
                _defaultValue = defaultValue;
                _convert = convert;
                string valueString = GetValue(name);
                if (valueString == null) _value = defaultValue;
                _value = convert(valueString);
            }
            private static string GetValue(string name)
            {
                return ConfigurationManager.AppSettings[name];
            }

            public T Value => _value;

#pragma warning disable CS0067
            public event EventHandler<SettingValueChangedEventArgs<T>> SettingValueChanged;
#pragma warning restore CS0067
        }
    }
}
