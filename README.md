# AmbientServices
The AmbientServices library is a library that allows library code to access basic services which are both universal and optional, allowing those libraries to be used in a variety of systems that provide vastly different implementations (or no implementation) of those basic services.

These basic services include logging, settings, caching, and progress tracking.  
By accessing these services through the interfaces provided by AmbientServices, code integrators can use the libraries without having to provide dependencies.
If integrators want the added benefits provided by a centralized implementation of one or more of those services, they can provide a bridge to their own implementations of these services and register them with the ambient service registry.
With one simple registration, the services will automatically be utilized by every library that uses AmbientServices to access them.

The well known dependency injection pattern is one possible solution to this problem, but dependency injection requires the code consumer to pass the required dependencies to each object constructor, which can be cumbersome, especially when the functionality is optional anyway.
Dependency injection becomes even more cumbersome when the code adds or removes dependencies, requiring the code user to update every constructor invocation with the new dependencies.

While dependency injection is better in most circumstances, when the services are completely optional and so pervasive as to be required by nearly all significant functionality, ambient services is a better solution.

## Using An Ambient Service
```csharp
public class LoggerSample
{
    private static readonly ILogger<LoggerSample> _Logger = Registry<ILogger>.Implementation.GetLogger<LoggerSample>();

    static LoggerSample()
    {
        _Logger?.Log("Startup", "Lifetime", LogLevel.Trace);
    }
}
```    

## Implementing A New Ambient Service
```csharp
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
```    

## Disabling An Ambient Service
```csharp
class Setup
{
	static Setup()
	{
        Registry<ICache>.Implementation = null;
	}
}
```    

## Overriding An Ambient Service
```csharp
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
```    

## Authors and license
This library is licensed under [MIT](https://opensource.org/licenses/MIT).

The library was written by James Ivie.

## Language and Tools
The library is written in C#, using .NET Standard.  The tests are written in .NET Core 3.1.

The code is built using either Microsoft Visual Studio 2017+ or Microsoft Visual Studio Code.