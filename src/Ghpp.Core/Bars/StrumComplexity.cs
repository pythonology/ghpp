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
    /// boundaries in the recent strum-history window.
    ///
    /// Two boundary signals feed the same saturation curve:
    ///   * <b>Picking rhythm</b> — variety in deltas between consecutive
    ///     effective-strum times.
    ///   * <b>Chord-change rhythm</b> — variety in deltas between consecutive
    ///     strums whose chord shape changed from the previous note. Captures
    ///     the irregular GRYB→GRYB→G→GRYB→GRYB→G→G→GRYB-style coordination
    ///     load, which is constant-picking but unpredictable-fret-changing.
    ///
    /// Their boundary counts sum, so a uniform-rhythm chord-changing pattern
    /// can score the same as an irregular-rhythm same-chord pattern.
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
            // Single pass: zero every contribution, build the effective-strum
            // timeline (picking events), and the chord-change-event timeline
            // (the subset of strums where the chord shape changed from the
            // previous note — same-chord re-strums don't count).
            var strumTimes = new List<double>();
            var strumBackref = new List<int>();
            var changeTimes = new List<double>();
            for (var i = 0; i < notes.Count; i++)
            {
                var note = notes[i];
                note.StrumContribution = 0.0;
                if (!IsEffectiveStrum(note)) continue;
                var time = note.Source.TimeSeconds;
                strumTimes.Add(time);
                strumBackref.Add(i);
                if (note.Source.Frets != note.PrevFrets)
                    changeTimes.Add(time);
            }

            var bonusRange = options.StrumMaxBonus - options.StrumBaselineContribution;
            for (var k = 0; k < strumBackref.Count; k++)
            {
                var n = notes[strumBackref[k]];
                var currentTime = n.Source.TimeSeconds;

                var totalBoundaries =
                    CountPickingBoundaries(strumTimes, k, currentTime, options) +
                    CountChangeBoundaries(changeTimes, currentTime, options);

                var rhythmFactor = 1.0 - Math.Exp(-totalBoundaries / options.StrumSaturationK);
                n.StrumContribution = options.StrumBaselineContribution + bonusRange * rhythmFactor;
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

        /// <summary>
        /// Walk back from the current strum and count Xexxar island
        /// boundaries among the most recent picking deltas (including the
        /// delta from the latest past strum to the current strum). Single
        /// scalar pass — no intermediate history or delta lists.
        /// </summary>
        private static int CountPickingBoundaries(
            List<double> strumTimes, int currentStrumIndex, double currentTime, DifficultyOptions options)
        {
            var boundaries = 0;
            var newerDelta = -1.0;
            var lastEventTime = currentTime;
            var tol = options.RhythmDeltaTolerance;
            var lowerBound = 1.0 - tol;
            var upperBound = 1.0 + tol;

            for (var back = 1; back <= options.RhythmHistoryNotes; back++)
            {
                var index = currentStrumIndex - back;
                if (index < 0) break;
                var t = strumTimes[index];
                if (currentTime - t > options.RhythmHistorySeconds) break;

                var currDelta = lastEventTime - t;
                if (newerDelta > 0 && currDelta > 0)
                {
                    var ratio = newerDelta / currDelta;
                    if (ratio < lowerBound || ratio > upperBound) boundaries++;
                }
                newerDelta = currDelta;
                lastEventTime = t;
            }
            return boundaries;
        }

        /// <summary>
        /// Walk back through the chord-change-event timeline and count
        /// boundaries among recent change-event deltas. The current time is
        /// NOT added to the sequence — the current strum may not itself be a
        /// change event. Single scalar pass.
        /// </summary>
        private static int CountChangeBoundaries(
            List<double> changeTimes, double currentTime, DifficultyOptions options)
        {
            var boundaries = 0;
            var newerDelta = -1.0;
            var lastEventTime = -1.0;
            var collected = 0;
            var tol = options.RhythmDeltaTolerance;
            var lowerBound = 1.0 - tol;
            var upperBound = 1.0 + tol;

            for (var i = changeTimes.Count - 1; i >= 0; i--)
            {
                var t = changeTimes[i];
                if (t >= currentTime) continue;
                if (currentTime - t > options.RhythmHistorySeconds) break;
                if (collected >= options.RhythmHistoryNotes) break;

                if (lastEventTime >= 0)
                {
                    var currDelta = lastEventTime - t;
                    if (newerDelta > 0 && currDelta > 0)
                    {
                        var ratio = newerDelta / currDelta;
                        if (ratio < lowerBound || ratio > upperBound) boundaries++;
                    }
                    newerDelta = currDelta;
                }
                lastEventTime = t;
                collected++;
            }
            return boundaries;
        }
    }
}
