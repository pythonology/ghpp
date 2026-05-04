using System;
using System.Collections.Generic;
using Ghpp.Core.Abstractions;
using Ghpp.Core.Models;

namespace Ghpp.Core.Aggregation
{
    /// <summary>
    /// Time-series of difficulty values sampled at a fixed rate.
    /// <c>Values[i]</c> corresponds to time <c>StartTimeSeconds + i / SampleRateHz</c>.
    /// </summary>
    public readonly struct DifficultyCurve
    {
        public DifficultyCurve(double[] values, double sampleRateHz, double startTimeSeconds)
        {
            Values = values;
            SampleRateHz = sampleRateHz;
            StartTimeSeconds = startTimeSeconds;
        }

        public double[] Values { get; }
        public double SampleRateHz { get; }
        public double StartTimeSeconds { get; }

        public double TimeAt(int sampleIndex)
        {
            return StartTimeSeconds + sampleIndex / SampleRateHz;
        }

        /// <summary>
        /// Compute a common time grid for the chart: <paramref name="startTime"/>
        /// and <paramref name="sampleCount"/> spanning the first to last note
        /// padded by half the NPS window. All Bars must share this grid so
        /// that pointwise composition lines up sample-for-sample.
        /// </summary>
        internal static void ComputeTimeGrid(
            IReadOnlyList<NoteContext> notes,
            DifficultyOptions options,
            out double startTime,
            out int sampleCount)
        {
            if (notes.Count == 0)
            {
                startTime = 0.0;
                sampleCount = 0;
                return;
            }
            var halfWindow = options.NpsWindowSeconds / 2.0;
            var firstTime = notes[0].Source.TimeSeconds;
            var lastTime = notes[notes.Count - 1].Source.TimeSeconds;
            startTime = Math.Max(0.0, firstTime - halfWindow);
            var endTime = lastTime + halfWindow;
            var duration = endTime - startTime;
            sampleCount = Math.Max(1, (int)Math.Ceiling(duration * options.SampleRateHz));
        }

        /// <summary>
        /// Builds a sliding-window NPS curve over <paramref name="notes"/>
        /// using the supplied per-note <paramref name="weights"/>. Edge samples
        /// divide by the actual window overlap to avoid artificial dips at
        /// chart boundaries.
        /// </summary>
        internal static DifficultyCurve BuildSliding(
            IReadOnlyList<NoteContext> notes,
            double[] weights,
            DifficultyOptions options,
            double startTime,
            int sampleCount)
        {
            var values = new double[sampleCount];
            if (notes.Count == 0 || sampleCount == 0)
            {
                return new DifficultyCurve(values, options.SampleRateHz, startTime);
            }

            var halfWindow = options.NpsWindowSeconds / 2.0;
            var endTime = startTime + sampleCount / options.SampleRateHz;

            var lo = 0;
            var hi = 0;
            var running = 0.0;

            for (var sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
            {
                var time = startTime + sampleIndex / options.SampleRateHz;
                var winLo = time - halfWindow;
                var winHi = time + halfWindow;

                while (hi < notes.Count && notes[hi].Source.TimeSeconds <= winHi)
                {
                    running += weights[hi];
                    hi++;
                }
                while (lo < hi && notes[lo].Source.TimeSeconds < winLo)
                {
                    running -= weights[lo];
                    lo++;
                }

                var effectiveLo = Math.Max(winLo, startTime);
                var effectiveHi = Math.Min(winHi, endTime);
                var effectiveWindow = Math.Max(1e-6, effectiveHi - effectiveLo);

                values[sampleIndex] = running / effectiveWindow;
            }

            return new DifficultyCurve(values, options.SampleRateHz, startTime);
        }

        internal static double TypeWeight(NoteType type, DifficultyOptions options)
        {
            switch (type)
            {
                case NoteType.Strum:
                    return options.StrumWeight;
                case NoteType.Hopo:
                    return options.HopoWeight;
                case NoteType.Tap:
                    return options.TapWeight;
                default:
                    return 1.0;
            }
        }
    }
}
