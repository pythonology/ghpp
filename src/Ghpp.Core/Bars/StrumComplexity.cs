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

            var raw = DifficultyCurve.BuildSliding(
                (IReadOnlyList<NoteContext>)notes, weights, options, startTime, sampleCount);

            // Two-stage shaping:
            //   1. Saturation rolloff: raw · (1 − e^(−raw/k)) dampens low
            //      values quadratically at the bottom of the curve.
            //   2. Power amplification: multiply by (raw / midpoint)^(p−1).
            //      Below midpoint this multiplier is <1 (extra damping on
            //      slow strumming); above midpoint it is >1 (amplifies
            //      high-NPS strumming above what raw NPS alone would give).
            // Net shape: slow strumming gets pushed lower, fast strumming
            // gets pushed higher than the linear baseline — separating
            // sparse picking from dense picking more aggressively.
            var values = raw.Values;
            var k = Math.Max(1e-9, options.StrumNpsSaturationK);
            var midpoint = Math.Max(1e-9, options.StrumNpsMidpoint);
            var amp = Math.Max(0.0, options.StrumNpsExponent - 1.0);
            var floor = options.StrumNpsFloor;
            var floorK = Math.Max(1e-9, options.StrumNpsFloorK);
            for (var i = 0; i < values.Length; i++)
            {
                var v = values[i];
                if (v <= 0)
                {
                    values[i] = 0;
                    continue;
                }
                var saturated = v * (1.0 - Math.Exp(-v / k));
                var multiplier = amp == 0.0 ? 1.0 : Math.Pow(v / midpoint, amp);
                // Floor adds a saturating low-end bonus so slow strumming
                // starts around ~floor instead of ~0. Saturates quickly so
                // it doesn't pile on top of high-NPS values.
                var floorContribution = floor * (1.0 - Math.Exp(-v / floorK));
                values[i] = saturated * multiplier + floorContribution;
            }
            return raw;
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
                // Fret-change bonus: strums where the player must actually
                // move a finger pay an extra per-note bump. The bonus
                // scales with the Hamming distance of fret bits — a
                // GR→YB switch (4 bits change) costs more than a G→R
                // switch (2 bits change) because more fingers physically
                // move at once. Normalized so a 2-bit change (single
                // fret repositioning) equals 1× the base bonus.
                //
                // We don't count Held-compatible transitions (e.g. GRYBO
                // → O → GRYBO) as fret changes since the player just
                // holds the union chord throughout — no fingers move.
                double fretChangeBonus = 0.0;
                if (n.Source.Frets != n.PrevFrets
                    && !IsHeldCompatible((byte) n.PrevFrets, (byte) n.Source.Frets))
                {
                    int hammingDistance = PopCountByte(
                        (byte)((byte) n.Source.Frets ^ (byte) n.PrevFrets));
                    fretChangeBonus = options.StrumFretChangeBonus * (hammingDistance / 2.0);
                }
                n.StrumContribution = options.StrumBaselineContribution
                                    + bonusRange * rhythmFactor
                                    + fretChangeBonus;
            }
        }

        /// <summary>
        /// True when the note participates in the picking-rhythm signal:
        /// only genuine Strum-type notes count. Same-fret HOPOs and Taps
        /// do NOT count even when no fret change occurs — once the chord is
        /// held (e.g. from the previous strum), subsequent same-fret HOPOs
        /// and Taps fire automatically off the held state without requiring
        /// the player to strum again. Treating them as effective strums
        /// would double-count picking work the player isn't actually doing.
        /// </summary>
        private static bool IsEffectiveStrum(NoteContext n)
        {
            return n.Source.Type == NoteType.Strum;
        }

        /// <summary>Count set bits in a byte. Used for Hamming distance.</summary>
        private static int PopCountByte(byte b)
        {
            int n = 0;
            while (b != 0) { n += b & 1; b = (byte)(b >> 1); }
            return n;
        }

        /// <summary>
        /// True when two consecutive chord shapes can both be played without
        /// the player changing fret state — i.e. there's a single held union
        /// chord that fires each note correctly (matching top fret, with
        /// any extra bits below each note's lowest pressed bit treated as
        /// anchor-allowed). Mirrors the Held detection in FretComplexity:
        /// GRYBO and O are held-compatible (hold GRYBO, O fires because
        /// O is the top); GR and G are not (top differs); BO and YO are
        /// not (B would be in the way for YO).
        /// </summary>
        private static bool IsHeldCompatible(byte prev, byte cur)
        {
            if (prev == 0 || cur == 0) return false; // open note — no fret bits to share
            var union = (byte) (prev | cur);
            return FiresFromHeld(prev, union) && FiresFromHeld(cur, union);
        }

        /// <summary>
        /// True when note <paramref name="n"/> can fire while the chord
        /// <paramref name="held"/> is being held: above n's lowest pressed
        /// bit, the held chord must contain exactly n's bits.
        /// </summary>
        private static bool FiresFromHeld(byte n, byte held)
        {
            // Lowest set bit position of n.
            int bottom = 0;
            byte bits = n;
            while ((bits & 1) == 0) { bottom++; bits = (byte)(bits >> 1); }
            byte anchorMask = (byte) ((1 << bottom) - 1);
            return (byte)(held & ~anchorMask) == n;
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
