using System;
using System.Collections.Generic;
using System.Linq;
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
            // Bar evaluator runs (CBar/SBar consume PatternMask for dampening).
            var patternIndex = PatternScanner.Scan(chart.Notes, options);
            for (var i = 0; i < notes.Count; i++)
            {
                notes[i].PatternMask = patternIndex.KindMaskAt(i);
            }

            // Common time grid shared by every Bar so the curves line up.
            DifficultyCurve.ComputeTimeGrid(notes, options, out var startTime, out var sampleCount);

            var sbarCurve = SBar.Build(notes, options, startTime, sampleCount);
            var cbarCurve = CBar.Build(notes, options, startTime, sampleCount);
            var lbarCurve = LBar.Build(notes, options, startTime, sampleCount);

            var composite = CompositeCurve.LpNorm(cbarCurve, sbarCurve, lbarCurve, options, startTime);

            var fretLaneNotes = CountPlayableUnits(notes);
            var aggregate = PercentileAggregator.Aggregate(composite.Values, fretLaneNotes, options);

            return BuildReport(chart, notes, composite, cbarCurve, sbarCurve, lbarCurve, aggregate, fretLaneNotes, patternIndex);
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
            DifficultyCurve cbarCurve,
            DifficultyCurve sbarCurve,
            DifficultyCurve lbarCurve,
            PercentileAggregator.Result aggregate,
            int fretLaneNotes,
            PatternIndex patterns)
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

            var cbarMean = 0.0;
            foreach (var v in cbarCurve.Values) cbarMean += v;
            if (cbarCurve.Values.Length > 0) cbarMean /= cbarCurve.Values.Length;

            var sbarMean = 0.0;
            foreach (var v in sbarCurve.Values) sbarMean += v;
            if (sbarCurve.Values.Length > 0) sbarMean /= sbarCurve.Values.Length;

            var lbarMean = 0.0;
            foreach (var v in lbarCurve.Values) lbarMean += v;
            if (lbarCurve.Values.Length > 0) lbarMean /= lbarCurve.Values.Length;

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
                CBarCurve = cbarCurve,
                CBarMean = cbarMean,
                SBarCurve = sbarCurve,
                SBarMean = sbarMean,
                LBarCurve = lbarCurve,
                LBarMean = lbarMean,

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
            };
        }
    }
}
