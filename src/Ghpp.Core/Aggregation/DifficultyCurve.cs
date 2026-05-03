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
        /// Builds the type-weighted NPS curve from annotated notes. Samples a
        /// sliding sum of per-note weights at <see cref="DifficultyOptions.SampleRateHz"/>
        /// across the chart duration. Edge samples divide by the actual window
        /// overlap so the curve does not dip artificially at the start and end.
        /// </summary>
        internal static DifficultyCurve Build(IReadOnlyList<AnnotatedNote> notes, DifficultyOptions options)
        {
            if (notes.Count == 0)
            {
                return new DifficultyCurve(Array.Empty<double>(), options.SampleRateHz, 0.0);
            }

            var window = options.NpsWindowSeconds;
            var halfWindow = window / 2.0;

            var firstTime = notes[0].Source.TimeSeconds;
            var lastTime = notes[notes.Count - 1].Source.TimeSeconds;
            var startTime = Math.Max(0.0, firstTime - halfWindow);
            var endTime = lastTime + halfWindow;
            var duration = endTime - startTime;
            var sampleCount = Math.Max(1, (int)Math.Ceiling(duration * options.SampleRateHz));

            var weights = ComputeWeights(notes, options);
            var values = new double[sampleCount];

            // Two-pointer sliding sum across time-sorted notes.
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

                // Effective overlap with the chart range avoids spurious drop-off
                // at the boundaries where the window extends past first/last note.
                var effectiveLo = Math.Max(winLo, startTime);
                var effectiveHi = Math.Min(winHi, endTime);
                var effectiveWindow = Math.Max(1e-6, effectiveHi - effectiveLo);

                values[sampleIndex] = running / effectiveWindow;
            }

            return new DifficultyCurve(values, options.SampleRateHz, startTime);
        }

        private static double[] ComputeWeights(IReadOnlyList<AnnotatedNote> notes, DifficultyOptions options)
        {
            var weights = new double[notes.Count];
            for (var i = 0; i < notes.Count; i++)
            {
                var note = notes[i];
                var typeWeight = TypeWeight(note.Source.Type, options);
                weights[i] = note.PlayableUnits * typeWeight * note.RhythmMultiplier * note.FretMultiplier;
            }
            return weights;
        }

        private static double TypeWeight(NoteType type, DifficultyOptions options)
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
