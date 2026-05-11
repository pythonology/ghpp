using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ghpp.Core.Abstractions;
using Ghpp.Core.Aggregation;
using Ghpp.Core.Bars;
using Ghpp.Core.Models;
using Ghpp.Core.Patterns;

namespace Ghpp.Core
{
    /// <summary>
    /// Top-level orchestrator for the difficulty pipeline. Walks the chart
    /// through pattern detection, the per-Bar evaluators, and the percentile
    /// aggregator to produce a star rating.
    /// </summary>
    public static class DifficultyCalculator
    {
        public static DifficultyReport Calculate(Chart chart)
        {
            return Calculate(chart, new DifficultyOptions());
        }

        public static DifficultyReport Calculate(Chart chart, DifficultyOptions options)
        {
            if (chart == null) throw new ArgumentNullException(nameof(chart));
            if (options == null) throw new ArgumentNullException(nameof(options));

            var notes = WrapNotes(chart.Notes);

            // Pattern detection: stamp PatternMask on each note before any
            // Bar evaluator runs. Pattern masks are reserved for future
            // dampener re-introduction; currently unused by the Bars.
            var patternIndex = PatternScanner.Scan(chart.Notes, options);
            for (var i = 0; i < notes.Count; i++)
            {
                notes[i].PatternMask = patternIndex.KindMaskAt(i);
            }

            // Common time grid shared by every Bar so the curves line up.
            DifficultyCurve.ComputeTimeGrid(notes, options, out var startTime, out var sampleCount);

            // The three Bar evaluators are independent CPU-bound passes:
            // each iterates the same notes list, writes its own per-note
            // cost field on NoteContext, and produces an independent curve.
            // No shared mutable state, so Parallel.Invoke is safe.
            FretComplexityResult fretResult = default;
            DifficultyCurve strumComplexityCurve = default;
            Parallel.Invoke(
                () => fretResult = FretComplexity.Build(notes, options, startTime, sampleCount),
                () => strumComplexityCurve = StrumComplexity.Build(notes, options, startTime, sampleCount));
            var fretComplexityCurve = fretResult.Curve;
            var fretChunks = fretResult.Chunks;
            var noteIsAnchor = fretResult.NoteIsAnchor;

            // Sustain complexity is currently disabled; pass an empty curve so
            // it does not contribute to the composite or the report.
            var sustainComplexityCurve = new DifficultyCurve(new double[sampleCount], options.SampleRateHz, startTime);

            var composite = CompositeCurve.LpNorm(fretComplexityCurve, strumComplexityCurve, sustainComplexityCurve, options, startTime);

            var fretLaneNotes = CountPlayableUnits(notes);
            var aggregate = PercentileAggregator.Aggregate(composite.Values, fretLaneNotes, options);

            return BuildReport(chart, notes, composite, fretComplexityCurve, strumComplexityCurve, sustainComplexityCurve, aggregate, fretLaneNotes, patternIndex, fretChunks, noteIsAnchor);
        }

        private static List<NoteContext> WrapNotes(IReadOnlyList<Note> source)
        {
            var wrapped = new List<NoteContext>(source.Count);
            var prevFrets = Frets.None;
            for (var i = 0; i < source.Count; i++)
            {
                var note = source[i];
                wrapped.Add(new NoteContext(note, prevFrets));
                prevFrets = note.Frets;
            }
            return wrapped;
        }

        private static int CountPlayableUnits(List<NoteContext> notes)
        {
            return notes.Sum(t => t.PlayableUnits);
        }

        private static DifficultyReport BuildReport(
            Chart chart,
            List<NoteContext> notes,
            DifficultyCurve composite,
            DifficultyCurve fretComplexityCurve,
            DifficultyCurve strumComplexityCurve,
            DifficultyCurve sustainComplexityCurve,
            PercentileAggregator.Result aggregate,
            int fretLaneNotes,
            PatternIndex patterns,
            IReadOnlyList<FretChunkRecord> fretChunks,
            bool[] noteIsAnchor)
        {
            var strumCount = 0;
            var hopoCount = 0;
            var tapCount = 0;

            foreach (var note in notes)
            {
                switch (note.Source.Type)
                {
                    case NoteType.Strum: strumCount++; break;
                    case NoteType.Hopo: hopoCount++; break;
                    case NoteType.Tap: tapCount++; break;
                    default: throw new ArgumentOutOfRangeException();
                }
            }

            var meanNps = 0.0;
            var maxNps = 0.0;
            foreach (var v in composite.Values)
            {
                meanNps += v;
                if (v > maxNps) maxNps = v;
            }
            if (composite.Values.Length > 0) meanNps /= composite.Values.Length;

            var fretMean = 0.0;
            foreach (var v in fretComplexityCurve.Values) fretMean += v;
            if (fretComplexityCurve.Values.Length > 0) fretMean /= fretComplexityCurve.Values.Length;

            var strumMean = 0.0;
            foreach (var v in strumComplexityCurve.Values) strumMean += v;
            if (strumComplexityCurve.Values.Length > 0) strumMean /= strumComplexityCurve.Values.Length;

            var sustainMean = 0.0;
            foreach (var v in sustainComplexityCurve.Values) sustainMean += v;
            if (sustainComplexityCurve.Values.Length > 0) sustainMean /= sustainComplexityCurve.Values.Length;

            var duration = 0.0;
            if (chart.Notes.Count > 0)
            {
                var first = chart.Notes[0];
                var last = chart.Notes[chart.Notes.Count - 1];
                duration = last.TimeSeconds - first.TimeSeconds;
            }

            return new DifficultyReport
            {
                Curve = composite,
                FretComplexityCurve = fretComplexityCurve,
                FretMean = fretMean,
                StrumComplexityCurve = strumComplexityCurve,
                StrumMean = strumMean,
                SustainComplexityCurve = sustainComplexityCurve,
                SustainMean = sustainMean,

                StarRating = aggregate.StarRating,
                IntensityStars = aggregate.IntensityStars,
                LengthBonus = aggregate.LengthBonus,
                Blended = aggregate.Blended,

                TotalNotes = notes.Count,
                FretLaneNotes = fretLaneNotes,
                StrumCount = strumCount,
                HopoCount = hopoCount,
                TapCount = tapCount,

                DurationSeconds = duration,
                MeanNps = meanNps,
                MaxNps = maxNps,

                P83 = aggregate.P83,
                P93 = aggregate.P93,
                P99 = aggregate.P99,
                L5 = aggregate.L5,

                Patterns = patterns,
                FretChunks = fretChunks,
                NoteIsAnchor = noteIsAnchor,
            };
        }
    }
}
