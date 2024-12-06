using System;
using System.Collections.Generic;

namespace AmbientServices;

/// <summary>
/// A class that contains extension methods for various statistics interfaces that add functions to aggregate statistic samples.
/// </summary>
public static class MissingSampleHandlingExtensions
{
    private interface IMissingSampleExtrapolator
    {
        long LeadingSampleExtrapolation(long firstNonNullSample, long secondNonNullSample, int samplesBetweenNonNullSamples, int indexBeforeAllNonNullValues);
        long MiddleSampleExtrapolation(long firstNonNullSample, long secondNonNullSample, int missingValueIndex, int missingValueCount);
        long TrailingSampleExtrapolation(long firstNonNullSample, long secondNonNullSample, int samplesBetweenNonNullSamples, int indexAfterAllNonNullValues);
    }
    private class LinearExtrapolator : IMissingSampleExtrapolator
    {
        public static LinearExtrapolator Instance { get; } = new();

        public long LeadingSampleExtrapolation(long firstNonNullSample, long secondNonNullSample, int samplesBetweenNonNullSamples, int indexBeforeAllNonNullValues)
        {
            return firstNonNullSample - (secondNonNullSample - firstNonNullSample) / (samplesBetweenNonNullSamples + 1) * (indexBeforeAllNonNullValues + 1);
        }
        public long MiddleSampleExtrapolation(long firstNonNullSample, long secondNonNullSample, int missingValueIndex, int missingValueCount)
        {
            return firstNonNullSample + ((secondNonNullSample - firstNonNullSample) * (missingValueIndex + 1) + ((missingValueCount + 1) / 2)) / (missingValueCount + 1);
        }
        public long TrailingSampleExtrapolation(long firstNonNullSample, long secondNonNullSample, int samplesBetweenNonNullSamples, int indexAfterAllNonNullValues)
        {
            return secondNonNullSample + (secondNonNullSample - firstNonNullSample) / (samplesBetweenNonNullSamples + 1) * (indexAfterAllNonNullValues + 1);
        }
    }
    private class ExponentialExtrapolator : IMissingSampleExtrapolator
    {
        public static ExponentialExtrapolator Instance { get; } = new();

        public long LeadingSampleExtrapolation(long lowSample, long highSample, int samplesBetweenNonNullSamples, int indexBeforeAllNonNullValues)
        {
            return (long)Math.Round(Math.Pow(Math.E, Math.Log(lowSample) - (Math.Log(highSample) - Math.Log(lowSample)) / (samplesBetweenNonNullSamples + 1) * (indexBeforeAllNonNullValues + 1)));
        }
        public long MiddleSampleExtrapolation(long lowSample, long highSample, int missingValueIndex, int missingValueCount)
        {
            return (long)Math.Round(Math.Pow(Math.E, Math.Log(lowSample) + ((Math.Log(highSample) - Math.Log(lowSample)) * (missingValueIndex + 1)) / (missingValueCount + 1)));
        }
        public long TrailingSampleExtrapolation(long lowSample, long highSample, int samplesBetweenNonNullSamples, int indexAfterAllNonNullValues)
        {
            return (long)Math.Round(Math.Pow(Math.E, Math.Log(highSample) + (Math.Log(highSample) - Math.Log(lowSample)) / (samplesBetweenNonNullSamples + 1) * (indexAfterAllNonNullValues + 1)));
        }
    }
    private class LogarithmicExtrapolator : IMissingSampleExtrapolator
    {
        public static LogarithmicExtrapolator Instance { get; } = new();

        public long LeadingSampleExtrapolation(long lowSample, long highSample, int samplesBetweenNonNullSamples, int indexBeforeAllNonNullValues)
        {
            return (long)Math.Round(Math.Log(Math.Pow(Math.E, lowSample) - (Math.Pow(Math.E, highSample) - Math.Pow(Math.E, lowSample)) / (samplesBetweenNonNullSamples + 1) * (indexBeforeAllNonNullValues + 1)));
        }
        public long MiddleSampleExtrapolation(long lowSample, long highSample, int missingValueIndex, int missingValueCount)
        {
            return (long)Math.Round(Math.Log(Math.Pow(Math.E, lowSample) + ((Math.Pow(Math.E, highSample) - Math.Pow(Math.E, lowSample)) * (missingValueIndex + 1)) / (missingValueCount + 1)));
        }
        public long TrailingSampleExtrapolation(long lowSample, long highSample, int samplesBetweenNonNullSamples, int indexAfterAllNonNullValues)
        {
            return (long)Math.Round(Math.Log(Math.Pow(Math.E, highSample) + (Math.Pow(Math.E, highSample) - Math.Pow(Math.E, lowSample)) / (samplesBetweenNonNullSamples + 1) * (indexAfterAllNonNullValues + 1)));
        }
    }
    /// <summary>
    /// Handle missing samples in the enumerated samples according to the reader's <see cref="IAmbientStatisticReader.MissingSampleHandling"/> enumeration.
    /// </summary>
    /// <param name="missingSamplesHandling">The <see cref="MissingSampleHandling"/> indicating how to handle null values in <paramref name="samples"/>.</param>
    /// <param name="samples">An enumeration of statistical samples.</param>
    /// <returns>An enumeration that has null samples values filled in according to <paramref name="missingSamplesHandling"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="samples"/> is null.</exception>
    public static IEnumerable<long?> HandleMissingSamples(this MissingSampleHandling missingSamplesHandling, IEnumerable<long?> samples)
    {
        if (samples == null) throw new ArgumentNullException(nameof(samples));

        switch (missingSamplesHandling)
        {
            case MissingSampleHandling.Zero:
                foreach (long? sample in samples)
                {
                    yield return sample ?? 0;
                }
                break;
            case MissingSampleHandling.LinearEstimation:            // for leading null samples, extrapolate linarly from the first two non-null samples using MiddleLinerarExtrapolation, for leading samples between two non-null samples, extrapolate linearly using LeadingExtrapolation, for leading samples at the end of the list, extrapolate linearly from the last two non-null samples using TrailingExtrapolation, if there is only one non-null sample, use that same value for every sample, if there are no non-null samples, return a null sample
                foreach (long? sample in Extrapolate(samples, LinearExtrapolator.Instance))
                {
                    yield return sample;
                }
                break;
            case MissingSampleHandling.ExponentialEstimation:
                foreach (long? sample in Extrapolate(samples, ExponentialExtrapolator.Instance))
                {
                    yield return sample;
                }
                break;
            case MissingSampleHandling.LogarithmicEstimation:
                foreach (long? sample in Extrapolate(samples, LogarithmicExtrapolator.Instance))
                {
                    yield return sample;
                }
                break;
            default:
            case MissingSampleHandling.Skip:
                foreach (long? sample in samples)
                {
                    if (sample != null) yield return sample.Value;
                }
                break;
        }
    }

    private static IEnumerable<long?> Extrapolate(IEnumerable<long?> samples, IMissingSampleExtrapolator extrapolator)
    {
        long? previousNonNullSample = null;
        long? lastNonNullSample = null;
        int distanceBetweenPreviousNonNullSampleAndLastNonNullSample = 0;
        int missingSamplesBeforeLastNonNullSample = 0;
        int missingSamplesAfterLastNonNullSample = 0;
        bool needAnotherNonNullSample = true;
        int sampleCount = 0;
        int samplesOutput = 0;
        foreach (long? sample in samples)
        {
            ++sampleCount;
            // do we need another non-null sample before we can return more (extrapolated) samples?
            if (needAnotherNonNullSample)
            {
                // a non-null sample
                if (sample != null)
                {
                    // no null samples before this one?
                    if (missingSamplesBeforeLastNonNullSample == 0)
                    {
                        yield return sample.Value;
                        ++samplesOutput;
                        // save this as a non-null sample, but keep looking for samples
                        previousNonNullSample = lastNonNullSample;
                        lastNonNullSample = sample;
                        // we're no longer buffering--we hit a non-null sample without null samples preceeding it
                        needAnotherNonNullSample = false;
                    }
                    // was there a non-null sample before this one?
                    else if (lastNonNullSample == null)
                    {
                        // save this as a non-null sample, but keep looking for samples
                        previousNonNullSample = lastNonNullSample;
                        lastNonNullSample = sample;
                    }
                    else
                    {
                        // were there leading samples before the two non-null samples?
                        for (int offset = 0; offset < missingSamplesBeforeLastNonNullSample; ++offset)
                        {
                            yield return extrapolator.LeadingSampleExtrapolation(lastNonNullSample.Value, sample.Value, missingSamplesAfterLastNonNullSample, offset);
                            ++samplesOutput;
                        }
                        missingSamplesBeforeLastNonNullSample = 0;
                        yield return lastNonNullSample.Value;
                        ++samplesOutput;
                        // were there missing samples since the last non-null sample?
                        for (int offset = 0; offset < missingSamplesAfterLastNonNullSample; ++offset)
                        {
                            yield return extrapolator.MiddleSampleExtrapolation(lastNonNullSample.Value, sample.Value, offset, missingSamplesAfterLastNonNullSample);
                            ++samplesOutput;
                        }
                        distanceBetweenPreviousNonNullSampleAndLastNonNullSample = missingSamplesAfterLastNonNullSample;
                        missingSamplesAfterLastNonNullSample = 0;
                        yield return sample.Value;
                        ++samplesOutput;
                        // save this as the last non-null sample
                        previousNonNullSample = lastNonNullSample;
                        lastNonNullSample = sample;
                        // we're no longer buffering
                        needAnotherNonNullSample = false;
                    }
                }
                else if (lastNonNullSample != null) ++missingSamplesAfterLastNonNullSample;
                else ++missingSamplesBeforeLastNonNullSample;
            }
            else // we just caught up, so we need to start counting missing samples again, or just output the non-null samples
            {
                System.Diagnostics.Debug.Assert(lastNonNullSample != null);
                if (sample != null)
                {
                    // were there missing samples since the last non-null sample?
                    for (int offset = 0; offset < missingSamplesAfterLastNonNullSample; ++offset)
                    {
                        yield return extrapolator.MiddleSampleExtrapolation(lastNonNullSample!.Value, sample.Value, offset, missingSamplesAfterLastNonNullSample);
                        ++samplesOutput;
                    }
                    distanceBetweenPreviousNonNullSampleAndLastNonNullSample = missingSamplesAfterLastNonNullSample;
                    missingSamplesAfterLastNonNullSample = 0;
                    yield return sample.Value;
                    ++samplesOutput;
                    // save this as the last non-null sample
                    previousNonNullSample = lastNonNullSample;
                    lastNonNullSample = sample;
                }
                else // a missing sample, so just count how many
                {
                    ++missingSamplesAfterLastNonNullSample;
                }
            }
        }
        // were there leading samples missing?
        if (missingSamplesBeforeLastNonNullSample > 0)
        {
            // were they all missing?
            if (lastNonNullSample == null)
            {
                System.Diagnostics.Debug.Assert(missingSamplesBeforeLastNonNullSample == sampleCount);
                System.Diagnostics.Debug.Assert(samplesOutput == 0);
                System.Diagnostics.Debug.Assert(missingSamplesAfterLastNonNullSample == 0);
                // just leave everything null!
                for (int offset = 0; offset < sampleCount; ++offset)
                {
                    yield return null;
                    ++samplesOutput;
                }
            }
            else // we got at least one sample, but since leading samples is still non-zero, we must have had only ONE sample, so use it for everything
            {
                System.Diagnostics.Debug.Assert(missingSamplesBeforeLastNonNullSample + missingSamplesAfterLastNonNullSample == sampleCount - 1);
                System.Diagnostics.Debug.Assert(samplesOutput == 0);
                // use the one non-null sample for all values
                for (int offset = 0; offset < sampleCount; ++offset)
                {
                    yield return lastNonNullSample.Value;
                    ++samplesOutput;
                }
            }
        }
        // were there trailing missing samples?
        else if (missingSamplesAfterLastNonNullSample > 0)
        {
            System.Diagnostics.Debug.Assert(previousNonNullSample != null);
            System.Diagnostics.Debug.Assert(lastNonNullSample != null);
            // loop through the trailing null values
            for (int offset = 0; offset < missingSamplesAfterLastNonNullSample; ++offset)
            {
                yield return extrapolator.TrailingSampleExtrapolation(previousNonNullSample!.Value, lastNonNullSample!.Value, distanceBetweenPreviousNonNullSampleAndLastNonNullSample, offset);
                ++samplesOutput;
            }
        }
        System.Diagnostics.Debug.Assert(samplesOutput == sampleCount);
    }
}
