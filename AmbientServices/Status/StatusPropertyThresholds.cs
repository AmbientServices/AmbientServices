﻿using AmbientServices.Extensions;
using AmbientServices.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace AmbientServices
{
    /// <summary>
    /// An enum identifying the nature of the status threshold.
    /// </summary>
    public enum StatusThresholdNature
    {
        /// <summary>
        /// Low values for this threshold are good.
        /// </summary>
        LowIsGood,
        /// <summary>
        /// High values for this threshold are good.
        /// </summary>
        HighIsGood,
    }
    /// <summary>
    /// An immutable class that holds status information about property threshold values at which status ratings should transition.
    /// Thresholds only apply to <see cref="StatusProperty"/>s whose values are numeric, and those property values must be converted to <see cref="float"/> before they can be compared to the threshold values.
    /// The static <see cref="DefaultPropertyThresholds"/> property provides access to the thresholds for all currently-loaded status checkers and auditors.
    /// </summary>
    public class StatusPropertyThresholds
    {
        private static ConcurrentDictionary<string, StatusPropertyThresholds> _thresholds = InitializeThresholds();
        private static readonly DefaultStatusThresholds _thresholdsAccessor = new(_thresholds);
        /// <summary>
        /// Gets a <see cref="IStatusThresholdsRegistry"/> containing the default status thresholds (those assigned via <see cref="DefaultPropertyThresholdsAttribute"/>s).
        /// </summary>
        public static IStatusThresholdsRegistry DefaultPropertyThresholds => _thresholdsAccessor;

        private static ConcurrentDictionary<string, StatusPropertyThresholds> InitializeThresholds()
        {
            _thresholds = new ConcurrentDictionary<string, StatusPropertyThresholds>();
            // hook into all subsequent assembly loads so we can register their thresholds
            AppDomain.CurrentDomain.AssemblyLoad += CurrentDomain_AssemblyLoad;
            // register the thresholds from all currently-loaded assemblies
            foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                RegisterAssemblyThresholds(assembly);
            }
            return _thresholds;
        }
        private static void CurrentDomain_AssemblyLoad(object? sender, AssemblyLoadEventArgs args)
        {
            RegisterAssemblyThresholds(args.LoadedAssembly);
        }
        private static void RegisterAssemblyThresholds(System.Reflection.Assembly assembly)
        {
            // does the loaded assembly refer to this one (if it doesn't, it can't have the stuff we're looking for)
            if (assembly.DoesAssemblyReferDirectlyToAssembly(System.Reflection.Assembly.GetExecutingAssembly()))
            {
                // loop through all the types looking for types that are not abstract, inherit from StatusNode (directly or indirectly) and have a public empty constructor
                foreach (Type type in assembly.GetLoadableTypes())
                {
                    if (Status.IsTestableStatusCheckerClass(type))
                    {
                        foreach (KeyValuePair<string, StatusPropertyThresholds> kvp in GetThresholds(type))
                        {
                            _thresholds.TryAdd(kvp.Key.ToUpperInvariant(), kvp.Value);
                        }
                    }
                }
            }
        }
        private static IEnumerable<KeyValuePair<string, StatusPropertyThresholds>> GetThresholds(Type type)
        {
            object[] attributes = type.GetCustomAttributes(typeof(DefaultPropertyThresholdsAttribute), true);
            if (attributes != null && attributes.Length > 0)
            {
                foreach (DefaultPropertyThresholdsAttribute checkerThresholds in attributes.Where(o => o.GetType() == typeof(DefaultPropertyThresholdsAttribute)))
                {
                    if (checkerThresholds.DeferToType != null)
                    {
                        foreach (KeyValuePair<string, StatusPropertyThresholds> kvp in GetThresholds(checkerThresholds.DeferToType))
                        {
                            yield return new KeyValuePair<string, StatusPropertyThresholds>(checkerThresholds.PropertyPath + "." + kvp.Key, kvp.Value);
                        }
                    }
                    else if (checkerThresholds.Thresholds != null)
                    {
                        yield return new KeyValuePair<string, StatusPropertyThresholds>(checkerThresholds.PropertyPath, checkerThresholds.Thresholds);
                    }
                }
            }
        }

        /// <summary>
        /// Constructs a <see cref="StatusPropertyThresholds"/> instance with the specified thresholds.
        /// </summary>
        /// <param name="nature">A <see cref="StatusThresholdNature"/> indicating whether or not low values are good.</param>
        /// <param name="failVsAlertThreshold">The threshold which divides failures from alerts (at this value it counts as a failure).</param>
        /// <param name="alertVsOkayThreshold">The threshold which divides alerts from okays (at this value it counts as an alert).</param>
        /// <param name="okayVsSuperlativeThreshold">The threshold which divides okays from superlatives (at this value it counts as an okay).  Default is <see cref="StatusThresholdNature.LowIsGood"/>.</param>
        public StatusPropertyThresholds(float? failVsAlertThreshold, float? alertVsOkayThreshold, float? okayVsSuperlativeThreshold, StatusThresholdNature nature = StatusThresholdNature.LowIsGood)
        {
            StatusThresholdNature? computedNature = null;
            if (failVsAlertThreshold > alertVsOkayThreshold || alertVsOkayThreshold > okayVsSuperlativeThreshold || failVsAlertThreshold > okayVsSuperlativeThreshold) computedNature = StatusThresholdNature.LowIsGood;
            if (failVsAlertThreshold < alertVsOkayThreshold || alertVsOkayThreshold < okayVsSuperlativeThreshold || failVsAlertThreshold < okayVsSuperlativeThreshold)
            {
                if (computedNature != null) throw new ArgumentException("The threshold values must be listed in either ascending or descending order!");
                computedNature = StatusThresholdNature.HighIsGood;
            }
            Nature = computedNature ?? nature;
            FailVsAlertThreshold = failVsAlertThreshold;
            AlertVsOkayThreshold = alertVsOkayThreshold;
            OkayVsSuperlativeThreshold = okayVsSuperlativeThreshold;
        }

        /// <summary>
        /// Gets a <see cref="StatusThresholdNature"/> indicating whether low values are good or not.  
        /// </summary>
        public StatusThresholdNature Nature { get; }
        /// <summary>
        /// Gets the threshold value which divides failures from alerts.  When the measured value is exactly equal to this value, we report a failure.
        /// </summary>
        public float? FailVsAlertThreshold { get; }
        /// <summary>
        /// Gets the threshold value which divides alerts from okay.  When the measured value is exactly equal to this value, we alert.
        /// </summary>
        public float? AlertVsOkayThreshold { get; }
        /// <summary>
        /// Gets the threshold value which divides okays from superlatives.  When the measured value is exactly equal to this value this counts as an okay.
        /// </summary>
        public float? OkayVsSuperlativeThreshold { get; }

        /// <summary>
        /// Rates the value based on the thresholds and gets a <see cref="StatusAuditAlert"/> indicating the status of the value relative to the thresholds.
        /// </summary>
        /// <param name="propertyName">The name of the property being rated.</param>
        /// <param name="value">The value to be rated.</param>
        /// <returns>A <see cref="StatusAuditAlert"/> indicating the status relative to the thresholds.</returns>
        public StatusAuditAlert Rate(string propertyName, float value)
        {
            return Rate(propertyName, value, value);
        }
        /// <summary>
        /// Rates the value based on the thresholds and gets a <see cref="StatusAuditAlert"/> indicating the status of the value relative to the thresholds.
        /// </summary>
        /// <param name="propertyName">The name of the property being rated.</param>
        /// <param name="lowValue">The low value of the range to be rated.</param>
        /// <param name="highValue">The high value of the range to be rated.</param>
        /// <returns>A <see cref="StatusAuditAlert"/> indicating the status relative to the thresholds.</returns>
        public StatusAuditAlert Rate(string propertyName, float lowValue, float highValue)
        {
//            if (lowValue < 0) throw new ArgumentOutOfRangeException(nameof(lowValue), "Status rating values must not be negative!");
//            if (highValue < 0) throw new ArgumentOutOfRangeException(nameof(highValue), "Status rating values must not be negative!");
            string code = propertyName + ".Threshold";
            string tersePrefix;
            string detailedPrefix = (lowValue == highValue)
                ? (propertyName + " is " + lowValue.ToSi(4) + " which is ")
                : (propertyName + " is between " + lowValue.ToSi(4) + " and " + highValue.ToSi(4) + " which is ");
            if (Nature == StatusThresholdNature.LowIsGood)
            {
                float value = highValue;
                tersePrefix = propertyName + ":" + value.ToSi(1);
                if (value < OkayVsSuperlativeThreshold)
                {
                    float seriousness = LowIsGoodImportance(0.0f, OkayVsSuperlativeThreshold.Value, value);
                    float rating = StatusRating.Superlative - seriousness;
                    return new StatusAuditAlert(rating, code, tersePrefix + "<" + OkayVsSuperlativeThreshold.Value.ToSi(1), detailedPrefix + "in the " + nameof(StatusRating.Superlative) + " range, below " + OkayVsSuperlativeThreshold.Value.ToSi(4));
                }
                else if (value < AlertVsOkayThreshold)
                {
                    float lowThreshold = OkayVsSuperlativeThreshold ?? 0.0f;
                    float seriousness = LowIsGoodImportance(lowThreshold, AlertVsOkayThreshold.Value, value);
                    float rating = StatusRating.Okay - seriousness;
                    return new StatusAuditAlert(rating, code, tersePrefix + ">=" + lowThreshold.ToSi(1), detailedPrefix + "in the " + nameof(StatusRating.Okay) + " range, between " + (OkayVsSuperlativeThreshold ?? 0.0).ToSi(4) + " and " + AlertVsOkayThreshold.Value.ToSi(4));
                }
                else if (value < FailVsAlertThreshold)
                {
                    float lowThreshold = AlertVsOkayThreshold ?? OkayVsSuperlativeThreshold ?? 0.0f;
                    float seriousness = LowIsGoodImportance(lowThreshold, FailVsAlertThreshold.Value, value);
                    float rating = StatusRating.Alert - seriousness;
                    return new StatusAuditAlert(rating, code, tersePrefix + ">=" + lowThreshold.ToSi(1), detailedPrefix + "in the " + nameof(StatusRating.Alert) + " range, between " + (AlertVsOkayThreshold ?? OkayVsSuperlativeThreshold ?? 0.0).ToSi(4) + " and " + FailVsAlertThreshold.Value.ToSi(4));
                }
                else
                {
                    float lowThreshold = FailVsAlertThreshold ?? AlertVsOkayThreshold ?? OkayVsSuperlativeThreshold ?? 0.0f;
                    float seriousness = LowIsGoodImportance(lowThreshold, float.MaxValue, value);
                    float rating = StatusRating.Fail - seriousness;
                    return new StatusAuditAlert(rating, code, tersePrefix + ">=" + lowThreshold.ToSi(1), detailedPrefix + "at or above the " + nameof(StatusRating.Fail) + " threshold value, " + lowThreshold.ToSi(4));
                }
            }
            else
            {
                float value = lowValue;
                tersePrefix = propertyName + ":" + value.ToSi(1);
                if (value > OkayVsSuperlativeThreshold)
                {
                    float seriousness = HighIsGoodImportance(OkayVsSuperlativeThreshold.Value, float.MaxValue, value);
                    float rating = StatusRating.Superlative - seriousness;
                    return new StatusAuditAlert(rating, code, tersePrefix + ">" + OkayVsSuperlativeThreshold.Value.ToSi(1), detailedPrefix + "in the " + nameof(StatusRating.Superlative) + " range, above " + OkayVsSuperlativeThreshold.Value.ToSi(4));
                }
                if (value > AlertVsOkayThreshold)
                {
                    float highThreshold = OkayVsSuperlativeThreshold ?? float.MaxValue;
                    float seriousness = HighIsGoodImportance(AlertVsOkayThreshold.Value, highThreshold, value);
                    float rating = StatusRating.Okay - seriousness;
                    return new StatusAuditAlert(rating, code, tersePrefix + "<=" + highThreshold.ToSi(1), detailedPrefix + "in the " + nameof(StatusRating.Okay) + " range, between " + AlertVsOkayThreshold.Value.ToSi(4) + " and " + (OkayVsSuperlativeThreshold ?? float.MaxValue).ToSi(4));
                }
                else if (value > FailVsAlertThreshold)
                {
                    float highThreshold = AlertVsOkayThreshold ?? OkayVsSuperlativeThreshold ?? float.MaxValue;
                    float seriousness = HighIsGoodImportance(FailVsAlertThreshold.Value, highThreshold, value);
                    float rating = StatusRating.Alert - seriousness;
                    return new StatusAuditAlert(rating, code, tersePrefix + "<=" + highThreshold.ToSi(1), detailedPrefix + "in the " + nameof(StatusRating.Alert) + " range, between " + FailVsAlertThreshold.Value.ToSi(4) + " and " + (AlertVsOkayThreshold ?? OkayVsSuperlativeThreshold ?? float.MaxValue).ToSi(4));
                }
                else
                {
                    float highThreshold = FailVsAlertThreshold ?? AlertVsOkayThreshold ?? OkayVsSuperlativeThreshold ?? float.MaxValue;
                    float seriousness = HighIsGoodImportance(0.0f, highThreshold, value);
                    float rating = StatusRating.Fail - seriousness;
                    return new StatusAuditAlert(rating, code, tersePrefix + "<=" + highThreshold.ToSi(1), detailedPrefix + "at or below the " + nameof(StatusRating.Fail) + " threshold value, " + highThreshold.ToSi(4));
                }
            }
        }
        private static float LowIsGoodImportance(float low, float high, float value)
        {
            if (value <= low) return 0.0f;
            if (value >= high) return 1.0f;
            return (float)((value - low) / (high - low));
        }
        private static float HighIsGoodImportance(float low, float high, float value)
        {
            return 1.0f - LowIsGoodImportance(low, high, value);
        }
    }

    internal class DefaultStatusThresholds : IStatusThresholdsRegistry
    {
        private readonly ConcurrentDictionary<string, StatusPropertyThresholds> _thresholds;

        public DefaultStatusThresholds(ConcurrentDictionary<string, StatusPropertyThresholds> thresholds)
        {
            _thresholds = thresholds;
        }

        public StatusPropertyThresholds? GetThresholds(string path)
        {
            StatusPropertyThresholds? value;
            if (!_thresholds.TryGetValue(path.ToUpperInvariant(), out value)) return null;
            return value;
        }
    }
    /// <summary>
    /// An attribute class that identifies the default property thresholds for a status test.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class DefaultPropertyThresholdsAttribute : Attribute
    {

        /// <summary>
        /// Constructs a default property thresholds instance by looking at a different type whose threshold attribute will be deferred to.
        /// </summary>
        /// <param name="propertyPath">The path to the indicated node.</param>
        /// <param name="deferToType">The <see cref="Type"/> that will be added at the specified node path.</param>
        public DefaultPropertyThresholdsAttribute(string propertyPath, Type deferToType)
        {
            PropertyPath = propertyPath;
            Thresholds = null;
            DeferToType = deferToType;
        }
        /// <summary>
        /// Constructs a default property thresholds attribute instance using the specified parameters.  
        /// Note that attribute parameters cannot take nullable values, so we use <see cref="float.NaN"/> instead to indicate that there is no such threshold value.
        /// </summary>
        /// <param name="propertyPath">The path to the property with a default threshold.</param>
        /// <param name="failVsAlertThreshold">The first value that is a failure instead of an alert.  <see cref="float.NaN"/> if there is no such value.</param>
        /// <param name="okayVsSuperlativeThreshold">The first value that is an alert instead of okay.  <see cref="float.NaN"/> if there is no such value.</param>
        /// <param name="alertVsOkayThreshold">The first value that is okay instead of superlative.  <see cref="float.NaN"/> if there is no such value.</param>
        /// <param name="thresholdNature">A <see cref="StatusThresholdNature"/> indicating whether low values are good or bad for this threshold.  Only used if less than two threshold values are specified.</param>
        public DefaultPropertyThresholdsAttribute(string propertyPath, float failVsAlertThreshold = float.NaN, float alertVsOkayThreshold = float.NaN, float okayVsSuperlativeThreshold = float.NaN, StatusThresholdNature thresholdNature = StatusThresholdNature.HighIsGood)
        {
            PropertyPath = propertyPath;
            Thresholds = new StatusPropertyThresholds(float.IsNaN(failVsAlertThreshold) ? null : failVsAlertThreshold, float.IsNaN(alertVsOkayThreshold) ? null : alertVsOkayThreshold, float.IsNaN(okayVsSuperlativeThreshold) ? null : okayVsSuperlativeThreshold, thresholdNature);
            DeferToType = null;
        }
        /// <summary>
        /// Gets the name of the property the thresholds apply to.
        /// </summary>
        public string PropertyPath { get; }
        /// <summary>
        /// Gets the <see cref="StatusPropertyThresholds"/> for the corresponding property.
        /// May be null if deferred to another type but that type has no attribute thresholds.
        /// </summary>
        public StatusPropertyThresholds? Thresholds { get; }
        /// <summary>
        /// A type to defer to for default property thresholds.  Thresholds attached to that type will be added with the property path prefixed.
        /// </summary>
        public Type? DeferToType { get; }
        /// <summary>
        /// Gets the status rating threshold that distinguishes failures from alerts.
        /// </summary>
        public float FailVsAlertThreshold => Thresholds?.FailVsAlertThreshold ?? float.NaN;
        /// <summary>
        /// Gets the status rating threshold that distinguishes alerts from okay.
        /// </summary>
        public float AlertVsOkayThreshold => Thresholds?.AlertVsOkayThreshold ?? float.NaN;
        /// <summary>
        /// Gets the status rating threshold that distinguishes okay from superlative.
        /// </summary>
        public float OkayVsSuperlativeThreshold => Thresholds?.OkayVsSuperlativeThreshold ?? float.NaN;
        /// <summary>
        /// Gets the status rating threshold that distinguishes okay from superlative.
        /// </summary>
        public StatusThresholdNature ThresholdNature => Thresholds?.Nature ?? StatusThresholdNature.HighIsGood;
    }
    /// <summary>
    /// An interface that abstracts the querying of thresholds used to rate system statuses.
    /// </summary>
    public interface IStatusThresholdsRegistry
    {
        /// <summary>
        /// Gets the <see cref="StatusPropertyThresholds"/> instance for the specified path, or null if no overrding thresholds are configured.
        /// </summary>
        /// <param name="path">The target system path whose thresholds are desired.</param> 
        /// <returns>A <see cref="StatusPropertyThresholds"/> instance containing the status rating thresholds, or null if there were no thresholds for the specified path.</returns>
        StatusPropertyThresholds? GetThresholds(string path);
    }
}
