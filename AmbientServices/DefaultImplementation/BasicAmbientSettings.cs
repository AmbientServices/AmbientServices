using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace AmbientServices
{
    [DefaultAmbientService]
    class BasicAmbientSettings : IAmbientSettings, IMutableAmbientSettings
    {
        private readonly ConcurrentDictionary<string, StringSetting> _settingsValues = new ConcurrentDictionary<string, StringSetting>();

        public void ChangeSetting(string key, string value)
        {
            StringSetting setting;
            setting = _settingsValues.GetOrAdd(key, k => new StringSetting { Key = key, Value = value });
            setting.Value = value;
        }
        class StringSetting : ISetting<string>
        {
            private string _stringValue;

            public string Key { get; set; }
            public string Value
            {
                get { return _stringValue; }
                set
                {
                    System.Threading.Interlocked.Exchange(ref _stringValue, value);
                    ValueChanged?.Invoke(this, new SettingValueChangedEventArgs<string> { Setting = this, NewValue = value });
                }
            }

            public event EventHandler<SettingValueChangedEventArgs<string>>  ValueChanged;
        }
        class StringSettingValueChangedEventArgs { }

        public ISetting<T> GetSetting<T>(string key, Func<string, T> convert, T defaultValue = default(T))
        {
            StringSetting currentStringSetting;
            currentStringSetting = _settingsValues.GetOrAdd(key, k => new StringSetting { Key = key, Value = null });
            T initialValue = currentStringSetting.Value == null ? defaultValue : convert(currentStringSetting.Value);
            return new Setting<T,string>(key, convert, defaultValue, initialValue, currentStringSetting, s => s, s => (s == null) ? defaultValue : convert(s));
        }

        class Setting<T,U> : ISetting<T>
        {
            private readonly string _key;
            private readonly T _defaultValue;
            private readonly Func<string, T> _convert;
            private readonly Func<string, U> _convertUnderlying;
            private readonly Func<U, T> _convertFromUnderlying;
            private readonly LazyUnsubscribeWeakEventListenerProxy<Setting<T,U>, object, SettingValueChangedEventArgs<U>> _weakValueChanged;
            private readonly LazyUnsubscribeWeakEventListenerProxy<Setting<T,U>, object, ServiceBroker<IAmbientSettings>.ImplementationChangedEventArgs<IAmbientSettings>> _weakAmbientImplementationChanged;

            private ISetting<U> _underlyingSetting;     // interlocked
            private object _currentValue;               // interlocked

            public Setting(string key, Func<string, T> convert, T defaultValue, T initialValue, ISetting<U> underlyingSetting, Func<string, U> convertUnderlying, Func<U,T> convertFromUnderlying)
            {
                _key = key;
                _defaultValue = defaultValue;
                _convert = convert;
                _convertUnderlying = convertUnderlying;
                _convertFromUnderlying = convertFromUnderlying;
                _underlyingSetting = underlyingSetting;
                _currentValue = initialValue;
                _weakValueChanged  = new LazyUnsubscribeWeakEventListenerProxy<Setting<T,U>, object, SettingValueChangedEventArgs<U>>(
                        this, UnderlyingSettingValueChanged, (luwf) => underlyingSetting.ValueChanged -= luwf.WeakEventHandler);
                underlyingSetting.ValueChanged += _weakValueChanged.WeakEventHandler;
                _weakAmbientImplementationChanged = new LazyUnsubscribeWeakEventListenerProxy<Setting<T,U>, object, ServiceBroker<IAmbientSettings>.ImplementationChangedEventArgs<IAmbientSettings>>(
                        this, BrokerImplementationChanged, (luwf) => ServiceBroker<IAmbientSettings>.GlobalImplementationChanged -= luwf.WeakEventHandler);
                ServiceBroker<IAmbientSettings>.GlobalImplementationChanged += _weakAmbientImplementationChanged.WeakEventHandler;
            }
            private static void UnderlyingSettingValueChanged(Setting<T,U> setting, object sender, SettingValueChangedEventArgs<U> e)
            {
                T newValue = setting._convertFromUnderlying(e.NewValue);
                setting.ChangeValue(newValue);
            }
            private static void BrokerImplementationChanged(Setting<T,U> setting, object sender, ServiceBroker<IAmbientSettings>.ImplementationChangedEventArgs<IAmbientSettings> e)
            {
                // unhook from the old underlying setting
                setting._underlyingSetting.ValueChanged -= setting._weakValueChanged.WeakEventHandler;
                // get the new underlying setting
                ISetting<U> newUnderlyingSetting = e.NewImplementation.GetSetting<U>(setting._key, setting._convertUnderlying, default(U));
                // hook to settings value changes in the new underlying setting instead
                newUnderlyingSetting.ValueChanged += setting._weakValueChanged.WeakEventHandler;
                // swap in the new underlying settings
                System.Threading.Interlocked.Exchange(ref setting._underlyingSetting, newUnderlyingSetting);
                // change the "current" value and notify if necessary
                setting.ChangeValue(setting._convertFromUnderlying(newUnderlyingSetting.Value));
            }

            private void ChangeValue(T newValue)
            {
                if (!AreValuesEqual((T)_currentValue, newValue))
                {
                    System.Threading.Interlocked.Exchange(ref _currentValue, newValue); // Note that this has a where T : class constraint :-(, which is why we have to box here
                    ValueChanged?.Invoke(this, new SettingValueChangedEventArgs<T> { Setting = this, NewValue = newValue });
                }
            }

            private static bool AreValuesEqual(T t1, T t2)
            {
                if (Object.ReferenceEquals(t1, null)) return Object.ReferenceEquals(t2, null);
                if (Object.ReferenceEquals(t1, t2)) return true;
                return t1.Equals(t2);
            }

            public T Value => (T)_currentValue;
            public event EventHandler<SettingValueChangedEventArgs<T>> ValueChanged;
        }
    }
}
