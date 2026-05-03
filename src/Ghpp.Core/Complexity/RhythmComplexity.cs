using System;
using System.Collections.Generic;
using Ghpp.Core.Abstractions;
using Ghpp.Core.Models;

namespace Ghpp.Core.Complexity
{
    /// <summary>
    /// Assigns a rhythm-complexity multiplier to each strum note using
    /// Xexxar-style island grouping. HOPO and Tap notes are specifically
    /// left out (rhythm complexity is considered a strumming-hand cost).
    /// </summary>
    /// <remarks>
    /// For each strum we look back at the previous strums in the configured
    /// time/count window, build the inter-strum delta sequence, and count
    /// transitions between "rhythm classes" (deltas that differ by more than
    /// a fixed ratio tolerance). More transitions means more rhythmic complexity.
    /// The multiplier saturates as <c>1 + RhythmBonusMax * (1 - exp(-boundaries / k))</c>.
    /// </remarks>
    internal static class RhythmComplexity
    {
        public static void Apply(IList<AnnotatedNote> notes, DifficultyOptions options)
        {
            // Collect strum-only timeline so the look-back window only counts strums.
            var strumIndices = new List<int>();
            var strumTimes = new List<double>();
            for (var i = 0; i < notes.Count; i++)
            {
                if (notes[i].Source.Type != NoteType.Strum)
                {
                    continue;
                }
                strumIndices.Add(i);
                strumTimes.Add(notes[i].Source.TimeSeconds);
            }

            for (var k = 0; k < strumIndices.Count; k++)
            {
                var currentTime = strumTimes[k];
                var history = CollectHistoryWindow(strumTimes, k, currentTime, options);
                if (history.Count < 2)
                {
                    // Default multiplier (1.0) already set on AnnotatedNote.
                    continue;
                }

                var deltas = BuildDeltaSequence(history, currentTime);
                var boundaries = CountIslandBoundaries(deltas, options.RhythmDeltaTolerance);
                var rhythmBonus = 1.0 - Math.Exp(-boundaries / options.RhythmSaturationK);
                notes[strumIndices[k]].RhythmMultiplier = 1.0 + options.RhythmBonusMax * rhythmBonus;
            }
        }

        /// <summary>
        /// Walk back from the current strum, collecting up to <c>RhythmHistoryNotes</c>
        /// previous strum times that fall within <c>RhythmHistorySeconds</c>.
        /// Returned list is in newest-first order.
        /// </summary>
        private static List<double> CollectHistoryWindow(
            List<double> strumTimes, int k, double currentTime, DifficultyOptions options)
        {
            var history = new List<double>();
            for (var back = 1; back <= options.RhythmHistoryNotes; back++)
            {
                var index = k - back;
                if (index < 0)
                {
                    break;
                }
                if (currentTime - strumTimes[index] > options.RhythmHistorySeconds)
                {
                    break;
                }
                history.Add(strumTimes[index]);
            }
            return history;
        }

        /// <summary>
        /// Given a newest-first history list and the current time, produce the
        /// chronological inter-strum deltas including the delta to the current note.
        /// </summary>
        private static List<double> BuildDeltaSequence(List<double> newestFirstHistory, double currentTime)
        {
            // Reverse to chronological order, then append the current time.
            var sequence = new List<double>(newestFirstHistory.Count + 1);
            for (var i = newestFirstHistory.Count - 1; i >= 0; i--)
            {
                sequence.Add(newestFirstHistory[i]);
            }
            sequence.Add(currentTime);

            var deltas = new List<double>(sequence.Count - 1);
            for (var i = 0; i < sequence.Count - 1; i++)
            {
                deltas.Add(sequence[i + 1] - sequence[i]);
            }
            return deltas;
        }

        /// <summary>
        /// Two consecutive deltas belong to the same "rhythm class" when their
        /// ratio is within <paramref name="tolerance"/> of 1. Each transition out
        /// of the current class counts as one boundary.
        /// </summary>
        private static int CountIslandBoundaries(List<double> deltas, double tolerance)
        {
            var islands = 1;
            for (var i = 1; i < deltas.Count; i++)
            {
                var previous = deltas[i - 1];
                var current = deltas[i];
                if (previous <= 0 || current <= 0)
                {
                    continue;
                }
                var ratio = current / previous;
                var sameClass = (1 - tolerance) <= ratio && ratio <= (1 + tolerance);
                if (!sameClass)
                {
                    islands++;
                }
            }
            return islands - 1;
        }
    }
}
