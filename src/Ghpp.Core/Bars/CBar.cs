using System;
using System.Collections.Generic;
using Ghpp.Core.Abstractions;
using Ghpp.Core.Abstractions.Patterns;
using Ghpp.Core.Aggregation;
using Ghpp.Core.Models;

namespace Ghpp.Core.Bars
{
    /// <summary>
    /// Chord/transition-cost axis. Replaces the simplified pipeline's
    /// <c>FretComplexity</c> multiplier with a per-(prev_frets, curr_frets,
    /// note_type) cost model that recognizes anchor overlap, finger-cross
    /// reversals, and HOPO/Tap chord anchor discounts. Pattern-aware: trills
    /// at high local NPS receive a mild rake-tap dampener.
    /// </summary>
    /// <remarks>
    /// CBar produces both per-note costs (written into <see cref="NoteContext.CBarCost"/>)
    /// and a 10 Hz time-series via the same edge-clipped sliding-window sum
    /// as <see cref="DifficultyCurve.Build"/>. The composite curve is the
    /// pointwise sum of the SBar-equivalent and CBar curves until LBar lands
    /// in Phase 5.
    /// </remarks>
    internal static class CBar
    {
        public static DifficultyCurve Build(
            IList<NoteContext> notes,
            DifficultyOptions options,
            double startTime,
            int sampleCount)
        {
            ApplyCosts(notes, options);

            var weights = new double[notes.Count];
            for (var i = 0; i < notes.Count; i++)
            {
                var n = notes[i];
                // CBar is *only* the cost of changing frets. A repeated chord
                // (YB→YB→YB) or a HOPO/Tap that keeps anchors held (GRY→Y→GRY→Y
                // where G,R stay pressed) has CBarCost = 0 and contributes
                // nothing to this Bar; raw picking-hand cost lives in SBar.
                weights[i] = n.PlayableUnits * DifficultyCurve.TypeWeight(n.Source.Type, options) * n.CBarCost;
            }

            return DifficultyCurve.BuildSliding(
                (IReadOnlyList<NoteContext>)notes, weights, options, startTime, sampleCount);
        }

        /// <summary>
        /// Compute the per-note CBar cost for every note in <paramref name="notes"/>.
        /// Open notes and the first note both produce 0 cost (no transition info).
        /// </summary>
        private static void ApplyCosts(IList<NoteContext> notes, DifficultyOptions options)
        {
            var prevDir = 0; // for finger-cross detection: -1, 0, +1
            for (var i = 0; i < notes.Count; i++)
            {
                var curr = notes[i];
                if (i == 0 || curr.Source.IsOpen || curr.PrevFrets == Frets.None)
                {
                    curr.CBarCost = 0.0;
                    prevDir = 0;
                    continue;
                }

                var currTop = curr.FretPosition;
                var prevTop = TopmostFret(curr.PrevFrets);
                if (currTop < 0 || prevTop < 0)
                {
                    curr.CBarCost = 0.0;
                    prevDir = 0;
                    continue;
                }

                var distance = Math.Abs(currTop - prevTop);
                var handShiftCost = LookupJumpCost(distance, options.FretJumpCost);

                // Chord-complexity cost: each finger toggle (one bit changing
                // between prev and curr fret bitmasks) is a finger action.
                // A simple single-fret change always involves 2 toggles
                // (one finger off + one finger on), so toggles beyond that
                // measure the extra coordination cost of multi-fret chord
                // transitions like GRY→YBO (4 toggles, 2 extra).
                var prevFretsByte = (byte)curr.PrevFrets;
                var currFretsByte = (byte)curr.Source.Frets;
                var toggles = PopCount((byte)(prevFretsByte ^ currFretsByte));
                var extraToggles = Math.Max(0, toggles - 2);
                var chordComplexityCost = extraToggles * options.CBarPerExtraToggleCost;

                var cost = handShiftCost + chordComplexityCost;

                // Anchor discount: scaled by the fraction of frets shared
                // relative to the larger chord. Full subset (one chord ⊆ the
                // other) gets the full discount; partial overlap gets less.
                var sharedSize = PopCount((byte)(prevFretsByte & currFretsByte));
                var maxSize = Math.Max(PopCount(prevFretsByte), PopCount(currFretsByte));
                if (maxSize > 0 && sharedSize > 0)
                {
                    var anchorRatio = (double)sharedSize / maxSize;
                    cost *= 1.0 - anchorRatio * options.CBarAnchorDiscount;

                    // HOPO/Tap chord anchor bonus: when a non-strum chord (≥2 frets)
                    // anchors on a shared finger the player can hold position.
                    var isChord = curr.Source.FretCount >= 2;
                    var nonStrum = curr.Source.Type != NoteType.Strum;
                    if (isChord && nonStrum)
                    {
                        cost *= 1.0 - options.CBarHopoChordAnchorBonus;
                    }
                }

                // Finger-cross penalty: direction reversal relative to prior step.
                var dir = currTop > prevTop ? 1 : (currTop < prevTop ? -1 : 0);
                if (prevDir != 0 && dir != 0 && dir != prevDir)
                {
                    cost += options.CBarFingerCrossPenalty;
                }
                prevDir = dir != 0 ? dir : prevDir;

                // Pattern dampener: trills at high local NPS use rake-tap.
                if ((curr.PatternMask & (1UL << (int)PatternKind.Trill)) != 0)
                {
                    var prevTime = curr.Source.TimeSeconds - notes[i - 1].Source.TimeSeconds;
                    if (prevTime > 0)
                    {
                        var localNps = 1.0 / prevTime;
                        if (localNps > options.CBarTrillDampenStartNps)
                        {
                            var raw = options.CBarTrillDampenSlope * (localNps - options.CBarTrillDampenStartNps);
                            var dampen = Math.Min(options.CBarTrillDampenMax, raw);
                            cost *= 1.0 - dampen;
                        }
                    }
                }

                curr.CBarCost = cost;
            }
        }

        private static int TopmostFret(Frets frets)
        {
            if (frets == Frets.None) return -1;
            var bits = (byte)frets;
            var pos = -1;
            while (bits != 0) { pos++; bits >>= 1; }
            return pos;
        }

        private static double LookupJumpCost(int distance, double[] table)
        {
            if (distance < 0 || table == null || distance >= table.Length) return 0.0;
            return table[distance];
        }

        private static int PopCount(byte b)
        {
            var n = 0;
            while (b != 0) { n += b & 1; b >>= 1; }
            return n;
        }
    }
}
