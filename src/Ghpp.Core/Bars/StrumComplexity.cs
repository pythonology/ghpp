using System;
using System.Collections.Generic;
using Ghpp.Core.Abstractions;
using Ghpp.Core.Aggregation;
using Ghpp.Core.Models;

namespace Ghpp.Core.Bars
{
    /// <summary>
    /// Strum-rhythm complexity axis. Each effective-strum's per-note
    /// contribution is a baseline (the floor cost of sustaining a picking
    /// rate) plus a rhythm-driven bonus that grows with Xexxar island
    /// boundaries in the recent strum-history window. Uniform fast strumming
    /// registers as the baseline; vastly-varying patterns (triple → quad →
    /// double, gaps, abrupt rhythm shifts) saturate near the per-note cap.
    /// </summary>
    /// <remarks>
    /// Only effective-strum notes participate. A note counts as an effective
    /// strum if it's a <see cref="NoteType.Strum"/>, or if it's a HOPO/Tap
    /// whose frets exactly match the previous note's — same-fret HOPOs/Taps
    /// can't actually use the HOPO/Tap mechanic (there's no fret change to
    /// hammer-on or tap), so the player will end up strumming them anyway.
    /// True HOPOs/Taps (with a fret change) don't drive picking-rhythm cost
    /// because the strum bar is genuinely optional for them.
    /// </remarks>
    internal static class StrumComplexity
    {
        public static DifficultyCurve Build(
            IList<NoteContext> notes,
            DifficultyOptions options,
            double startTime,
            int sampleCount)
        {
            ApplyContributions(notes, options);

            var weights = new double[notes.Count];
            for (var i = 0; i < notes.Count; i++)
            {
                weights[i] = notes[i].StrumContribution;
            }

            return DifficultyCurve.BuildSliding(
                (IReadOnlyList<NoteContext>)notes, weights, options, startTime, sampleCount);
        }

        private static void ApplyContributions(IList<NoteContext> notes, DifficultyOptions options)
        {
            // Build an effective-strum timeline so the look-back window
            // operates on actual picking events.
            var strumTimes = new List<double>();
            var strumBackref = new List<int>();
            for (var i = 0; i < notes.Count; i++)
            {
                if (!IsEffectiveStrum(notes[i])) continue;
                strumTimes.Add(notes[i].Source.TimeSeconds);
                strumBackref.Add(i);
            }

            for (var k = 0; k < strumBackref.Count; k++)
            {
                var noteIndex = strumBackref[k];
                var n = notes[noteIndex];
                var currentTime = n.Source.TimeSeconds;

                var history = CollectStrumHistory(strumTimes, k, currentTime, options);
                double bonus;
                if (history.Count < 2)
                {
                    // Not enough rhythm context yet — register the baseline.
                    bonus = options.StrumBaselineContribution;
                }
                else
                {
                    var deltas = BuildDeltaSequence(history, currentTime);
                    var boundaries = CountIslandBoundaries(deltas, options.RhythmDeltaTolerance);
                    var rhythmFactor = 1.0 - Math.Exp(-boundaries / options.StrumSaturationK);
                    bonus = options.StrumBaselineContribution
                          + (options.StrumMaxBonus - options.StrumBaselineContribution) * rhythmFactor;
                }

                n.StrumContribution = bonus;
            }

            // Non-effective-strum notes contribute nothing.
            for (var i = 0; i < notes.Count; i++)
            {
                if (!IsEffectiveStrum(notes[i]))
                {
                    notes[i].StrumContribution = 0.0;
                }
            }
        }

        /// <summary>
        /// True when the note participates in the picking-rhythm signal: any
        /// strum, plus HOPOs/Taps that share frets with the previous note (no
        /// fret change → the HOPO/Tap mechanic can't fire → effectively a strum).
        /// </summary>
        private static bool IsEffectiveStrum(NoteContext n)
        {
            if (n.Source.Type == NoteType.Strum) return true;
            return n.Source.Frets == n.PrevFrets;
        }

        private static List<double> CollectStrumHistory(
            List<double> strumTimes, int currentStrumIndex, double currentTime, DifficultyOptions options)
        {
            var history = new List<double>();
            for (var back = 1; back <= options.RhythmHistoryNotes; back++)
            {
                var index = currentStrumIndex - back;
                if (index < 0) break;
                var t = strumTimes[index];
                if (currentTime - t > options.RhythmHistorySeconds) break;
                history.Add(t);
            }
            return history;
        }

        private static List<double> BuildDeltaSequence(List<double> newestFirstHistory, double currentTime)
        {
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

        private static int CountIslandBoundaries(List<double> deltas, double tolerance)
        {
            var islands = 1;
            for (var i = 1; i < deltas.Count; i++)
            {
                var prev = deltas[i - 1];
                var curr = deltas[i];
                if (prev <= 0 || curr <= 0) continue;
                var ratio = curr / prev;
                var sameClass = (1 - tolerance) <= ratio && ratio <= (1 + tolerance);
                if (!sameClass) islands++;
            }
            return islands - 1;
        }
    }
}
