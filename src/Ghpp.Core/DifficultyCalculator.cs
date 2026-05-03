using System;
using System.Collections.Generic;
using System.Linq;
using Ghpp.Core.Abstractions;
using Ghpp.Core.Aggregation;
using Ghpp.Core.Models;
using Ghpp.Core.Complexity;

namespace Ghpp.Core
{
    /// <summary>
    /// Top-level orchestrator for the difficulty pipeline. Walks the chart
    /// through multiple layers of difficulty calculation, and aggregates into a
    /// single star rating value.
    /// </summary>
    public static class DifficultyCalculator
    {
        /// <returns><see cref="DifficultyReport"/> with the headline result and breakdown.</returns>
        public static DifficultyReport Calculate(Chart chart)
        {
            return Calculate(chart, new DifficultyOptions());
        }

        /// <returns><see cref="DifficultyReport"/> with the headline result and breakdown.</returns>
        public static DifficultyReport Calculate(Chart chart, DifficultyOptions options)
        {
            if (chart == null)
            {
                throw new ArgumentNullException(nameof(chart));
            }
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var notes = WrapNotes(chart.Notes);

            // Per-note complexity multipliers.
            RhythmComplexity.Apply(notes, options);
            var fretJumpDistribution = FretComplexity.Apply(notes, options);

            // Type-weighted NPS curve.
            var curve = DifficultyCurve.Build(notes, options);

            // Aggregate into a star rating.
            var fretLaneNotes = CountPlayableUnits(notes);
            var aggregate = PercentileAggregator.Aggregate(curve.Values, fretLaneNotes, options);

            return BuildReport(chart, notes, curve, aggregate, fretLaneNotes, fretJumpDistribution);
        }

        private static List<AnnotatedNote> WrapNotes(IReadOnlyList<Note> source)
        {
            var wrapped = new List<AnnotatedNote>(source.Count);
            wrapped.AddRange(source.Select(t => new AnnotatedNote(t)));
            return wrapped;
        }

        private static int CountPlayableUnits(List<AnnotatedNote> notes)
        {
            return notes.Sum(t => t.PlayableUnits);
        }

        private static DifficultyReport BuildReport(
            Chart chart,
            List<AnnotatedNote> notes,
            DifficultyCurve curve,
            PercentileAggregator.Result aggregate,
            int fretLaneNotes,
            IReadOnlyDictionary<int, int> fretJumpDistribution)
        {
            var strumCount = 0;
            var hopoCount = 0;
            var tapCount = 0;
            var rhythmSum = 0.0;
            var rhythmStrums = 0;
            var fretSum = 0.0;
            var fretSamples = 0;

            for (var i = 0; i < notes.Count; i++)
            {
                var note = notes[i];
                switch (note.Source.Type)
                {
                    case NoteType.Strum:
                        strumCount++;
                        rhythmSum += note.RhythmMultiplier;
                        rhythmStrums++;
                        break;
                    case NoteType.Hopo:
                        hopoCount++;
                        break;
                    case NoteType.Tap:
                        tapCount++;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                if (i <= 0)
                    continue;
                fretSum += note.FretMultiplier;
                fretSamples++;
            }

            var meanNps = 0.0;
            var maxNps = 0.0;
            if (curve.Values.Length > 0)
            {
                var sum = 0.0;
                foreach (var t in curve.Values)
                {
                    sum += t;
                    if (t > maxNps)
                    {
                        maxNps = t;
                    }
                }
                meanNps = sum / curve.Values.Length;
            }

            var duration = 0.0;
            if (chart.Notes.Count <= 0)
                return new DifficultyReport
                {
                    Curve = curve,
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

                    MeanRhythmMultiplier = rhythmStrums > 0 ? rhythmSum / rhythmStrums : 1.0,
                    MeanFretMultiplier = fretSamples > 0 ? fretSum / fretSamples : 1.0,

                    FretJumpDistribution = fretJumpDistribution,
                };
            var first = chart.Notes[0];
            var last = chart.Notes[chart.Notes.Count - 1];
            duration = last.TimeSeconds - first.TimeSeconds;

            return new DifficultyReport
            {
                Curve = curve,
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

                MeanRhythmMultiplier = rhythmStrums > 0 ? rhythmSum / rhythmStrums : 1.0,
                MeanFretMultiplier = fretSamples > 0 ? fretSum / fretSamples : 1.0,

                FretJumpDistribution = fretJumpDistribution,
            };
        }
    }
}
