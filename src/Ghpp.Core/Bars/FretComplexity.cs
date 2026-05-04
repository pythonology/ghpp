using System;
using System.Collections.Generic;
using Ghpp.Core.Abstractions;
using Ghpp.Core.Aggregation;
using Ghpp.Core.Models;

namespace Ghpp.Core.Bars
{
    /// <summary>
    /// Fret-hand complexity axis. Computes a per-note transition cost that
    /// models, in order:
    ///   * <b>Anchors</b> — frets shared between the previous and current note
    ///     do not require movement. For HOPO/Tap notes, if every current fret
    ///     was already held and no held fret sits above the new top, the note
    ///     is free (cost 0).
    ///   * <b>Chord intrinsic difficulty</b> (frets &amp; gaps) — a chord's
    ///     cost rises with both the count of frets held and the gap count
    ///     between them (e.g. GO &gt; GR despite both being 2-fret chords).
    ///     The transition only charges the *increase* in chord cost.
    ///   * <b>Hand shift</b> — the topmost <i>changed</i> fret's index delta
    ///     drives a hand-movement cost via <see cref="DifficultyOptions.FretJumpCost"/>.
    ///   * <b>Pivots</b> — when both prev and curr are chords sharing at least
    ///     one finger, the player can pivot, applying
    ///     <see cref="DifficultyOptions.FretPivotDiscount"/> to hand-shift +
    ///     toggle costs. HOPO/Tap notes never pivot (anchors only).
    ///   * <b>Chord toggle extras</b> — finger toggles beyond the natural two
    ///     (one off, one on) of a single-fret change accumulate at
    ///     <see cref="DifficultyOptions.FretPerExtraToggleCost"/>.
    /// </summary>
    /// <remarks>
    /// Costs are written to <see cref="NoteContext.FretCost"/> and aggregated
    /// into a 10 Hz time-series via the same edge-clipped sliding-window sum
    /// used by the other Bars (see <see cref="DifficultyCurve.BuildSliding"/>).
    /// Open notes and the very first note both produce 0 cost.
    /// </remarks>
    internal static class FretComplexity
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
                weights[i] = DifficultyCurve.TypeWeight(n.Source.Type, options) * n.FretCost;
            }

            return DifficultyCurve.BuildSliding(
                (IReadOnlyList<NoteContext>)notes, weights, options, startTime, sampleCount);
        }

        /// <summary>
        /// Compute the per-note FretComplexity cost for every note in <paramref name="notes"/>.
        /// </summary>
        private static void ApplyCosts(IList<NoteContext> notes, DifficultyOptions options)
        {
            // The "effective frets" track what the player is *actually*
            // holding. A HOPO/Tap that didn't require movement leaves
            // effective frets unchanged (player keeps anchors held).
            var effectiveFrets = Frets.None;

            for (var i = 0; i < notes.Count; i++)
            {
                var curr = notes[i];

                if (i == 0 || curr.Source.IsOpen || effectiveFrets == Frets.None)
                {
                    curr.FretCost = 0.0;
                    effectiveFrets = curr.Source.Frets;
                    continue;
                }

                var prevByte = (byte)effectiveFrets;
                var currByte = (byte)curr.Source.Frets;

                // ---- Anchor free-pass for HOPO/Tap -------------------------
                // When all curr frets are already held *and* no held fret sits
                // above the new topmost fret, the player doesn't move. This
                // is the canonical "HOPO/Tap on top of an anchor" situation
                // (e.g. holding GR, the chart says R: just tap red).
                if (curr.Source.Type != NoteType.Strum)
                {
                    var allHeld = (currByte & prevByte) == currByte;
                    var currTop = curr.FretPosition;
                    var aboveMask = currTop >= 0 ? (byte)(0xFF << (currTop + 1)) : (byte)0;
                    var heldAbove = (prevByte & aboveMask) != 0;
                    if (allHeld && !heldAbove)
                    {
                        curr.FretCost = 0.0;
                        // effectiveFrets unchanged — player still holds prev set.
                        continue;
                    }
                }

                // ---- Chord intrinsic delta ---------------------------------
                // Only charge for the increase in chord-shape difficulty:
                // moving from a hard chord (GYO) to a simple one (G) shouldn't
                // cost anything in chord-intrinsic terms.
                var intrinsicDelta = Math.Max(
                    0.0,
                    ChordIntrinsic(currByte, options) - ChordIntrinsic(prevByte, options));

                // ---- Hand-shift cost (anchor stripping) --------------------
                // Anchored frets (shared between prev and curr) don't move.
                // The hand-shift cost is driven by the topmost *changed* fret.
                var shared = (byte)(prevByte & currByte);
                var prevChanged = (byte)(prevByte & ~shared);
                var currChanged = (byte)(currByte & ~shared);

                var prevChangedTop = TopBit(prevChanged);
                var currChangedTop = TopBit(currChanged);
                var shiftDistance = (prevChangedTop >= 0 && currChangedTop >= 0)
                    ? Math.Abs(currChangedTop - prevChangedTop)
                    : 0;
                var handShiftCost = LookupJumpCost(shiftDistance, options.FretJumpCost);

                // ---- Chord toggle extras -----------------------------------
                // Each finger that toggles between prev and curr is one
                // action. A single-fret change always involves 2 toggles
                // (one off + one on); extras measure multi-fret coordination.
                var toggles = PopCount((byte)(prevByte ^ currByte));
                var extraToggles = Math.Max(0, toggles - 2);
                var chordToggleCost = extraToggles * options.FretPerExtraToggleCost;

                // ---- Pivot discount ----------------------------------------
                // Chord-only: when both prev and curr are chords (≥2 frets)
                // and they share at least one finger, the player can pivot off
                // the shared finger. HOPO/Tap notes do NOT get the pivot —
                // pivoting requires the strum hand to coordinate.
                var movementCost = handShiftCost + chordToggleCost;
                if (curr.Source.Type == NoteType.Strum
                    && PopCount(prevByte) >= 2
                    && PopCount(currByte) >= 2
                    && shared != 0)
                {
                    movementCost *= options.FretPivotDiscount;
                }

                curr.FretCost = intrinsicDelta + movementCost;
                effectiveFrets = curr.Source.Frets;
            }
        }

        /// <summary>
        /// Chord intrinsic difficulty: <c>PerHeld × popcount(F) + PerGap × gapCount(F)</c>.
        /// Open / single-fret returns based on PerHeld only (0 gaps). For chords,
        /// gaps = (highestFretIndex − lowestFretIndex + 1) − popcount(F).
        /// </summary>
        private static double ChordIntrinsic(byte frets, DifficultyOptions options)
        {
            if (frets == 0) return 0.0;
            var held = PopCount(frets);
            var top = TopBit(frets);
            var bottom = BottomBit(frets);
            var span = top - bottom + 1;
            var gaps = span - held;
            return options.FretIntrinsicPerHeld * held + options.FretIntrinsicPerGap * gaps;
        }

        private static int TopBit(byte bits)
        {
            if (bits == 0) return -1;
            var pos = -1;
            while (bits != 0) { pos++; bits >>= 1; }
            return pos;
        }

        private static int BottomBit(byte bits)
        {
            if (bits == 0) return -1;
            var pos = 0;
            while ((bits & 1) == 0) { pos++; bits >>= 1; }
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
