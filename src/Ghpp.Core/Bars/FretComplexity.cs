using System;
using System.Collections.Generic;
using Ghpp.Core.Abstractions;
using Ghpp.Core.Aggregation;
using Ghpp.Core.Models;

namespace Ghpp.Core.Bars
{
    /// <summary>
    /// Fret-hand complexity axis. Each note's cost is the sum of:
    ///   * <b>Chord intrinsic</b> of the player's resulting hand state minus
    ///     the intrinsic of the anchored portion already held. See
    ///     <see cref="ChordIntrinsic"/>.
    ///   * <b>Action cost</b> — every press and every release counts as one
    ///     action, weighted equally via
    ///     <see cref="DifficultyOptions.ChordPerFingerActionCost"/>.
    ///   * <b>Hand shift</b> — distance between the topmost released and
    ///     topmost added fret, looked up in
    ///     <see cref="DifficultyOptions.FretJumpCost"/>.
    /// Action + hand-shift are then multiplied by
    /// <see cref="DifficultyOptions.FretPivotDiscount"/> whenever any finger
    /// is shared between prev and curr (an anchor or pivot lets the player
    /// ignore that finger).
    /// </summary>
    /// <remarks>
    /// For Strum chords (≥2 frets) the player's hand state matches the
    /// chart chord exactly — no anchors retained. For single-fret Strums and
    /// HOPO/Tap notes of any size, the player retains any prev-held fret at
    /// or below the new curr top as an anchor; held frets above the new top
    /// are released. Open notes clear the hand entirely, billing each
    /// previously-held fret as a release action.
    /// Costs are written to <see cref="NoteContext.FretCost"/> and aggregated
    /// into a 10 Hz time-series via <see cref="DifficultyCurve.BuildSliding"/>.
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
            // Precompute the chord intrinsic for every possible 5-fret mask
            // so the per-note loop reduces to two array lookups + arithmetic.
            var intrinsicTable = BuildIntrinsicTable(options);

            // effectiveFrets tracks what the player is actually holding.
            // For Strum notes the player must form the exact chart chord.
            // For HOPO/Tap notes the player retains any prev-held fret at or
            // below the new curr top as an anchor; held frets above the new
            // top must be released so the curr top registers as highest.
            var effectiveFrets = Frets.None;

            foreach (var curr in notes)
            {
                var prevEff = (byte)effectiveFrets;
                var currChart = (byte)curr.Source.Frets;

                // Resolve the player's hand state after this note.
                //   * Open notes clear the hand (every held fret released).
                //   * Strum chords (≥2 frets) force exact chart shape.
                //   * Everything else lets the player retain prev-held frets
                //     at or below the curr top as an anchor.
                byte currEff;
                if (curr.Source.IsOpen)
                {
                    currEff = 0;
                }
                else if (curr.Source.Type == NoteType.Strum && PopCount(currChart) > 1)
                {
                    currEff = currChart;
                }
                else
                {
                    var currTop = curr.FretPosition;
                    var belowMask = currTop >= 0 ? (byte)((1 << (currTop + 1)) - 1) : (byte)0;
                    var retained = (byte)(prevEff & belowMask);
                    currEff = (byte)(retained | currChart);
                }

                var shared = (byte)(prevEff & currEff);
                var released = (byte)(prevEff & ~currEff);
                var added = (byte)(currEff & ~prevEff);

                // Full chord intrinsic of the resulting hand, minus the part
                // already held (anchor relief). Pure intrinsic function; the
                // discount lives here in the caller, not inside ChordIntrinsic.
                var chordCost = intrinsicTable[currEff] - intrinsicTable[shared];

                // Symmetric per-finger action cost: every press and every
                // release is one action.
                var actionCount = PopCount(released) + PopCount(added);
                var actionCost = actionCount * options.ChordPerFingerActionCost;

                // Hand shift between the topmost released and topmost added
                // fret. If only one side has changed bits (pure add or pure
                // release), there is no hand-center movement to charge.
                var releasedTop = TopBit(released);
                var addedTop = TopBit(added);
                var shiftDistance = (releasedTop >= 0 && addedTop >= 0)
                    ? Math.Abs(addedTop - releasedTop)
                    : 0;
                var handShiftCost = LookupJumpCost(shiftDistance, options.FretJumpCost);

                // Any shared finger (anchor or pivot) discounts the movement
                // work because the player can ignore that finger.
                var movementCost = actionCost + handShiftCost;
                if (shared != 0)
                    movementCost *= options.FretPivotDiscount;

                curr.FretCost = chordCost + movementCost;
                effectiveFrets = (Frets)currEff;
            }
        }

        /// <summary>
        /// Chord intrinsic difficulty for the given fret mask. Sums four
        /// physical contributions: per-fret placement (lone vs extending a
        /// consecutive run), per-position interior gap cost, a flat barre cost
        /// when all 5 frets are held, and an outer-pair reduction when only
        /// the bottom + top frets are held with a gap between them.
        /// </summary>
        private static double ChordIntrinsic(byte frets, DifficultyOptions options)
        {
            if (frets == 0)
                return 0.0;

            var fingers = PopCount(frets);
            var bottom = BottomBit(frets);
            var top = TopBit(frets);

            // Per-fret cost
            // A held fret extends a run if its lower neighbor is also held.
            var runCount = PopCount((byte)(frets & (frets << 1)));
            
            var loneCount = fingers - runCount;
            var fretCost = loneCount * options.ChordFretLone
                         + runCount * options.ChordFretRun;

            // Interior unheld frets, weighted by which position they sit at.
            var gapCost = 0.0;
            for (var i = bottom + 1; i < top; i++)
            {
                if ((frets & (1 << i)) == 0)
                    gapCost += options.ChordGapCost[i];
            }

            var cost = fretCost + gapCost;
            switch (fingers)
            {
                case 5:
                    cost += options.ChordBarreCost;
                    break;
                case 2 when gapCost > 0.0:
                    cost -= options.ChordOuterPairReduction;
                    break;
            }

            return cost;
        }

        // Precomputed bit-op lookup tables. Every byte mask collapses to a
        // single array index, so the per-note hot loop never runs the
        // bit-by-bit loops at runtime.
        private static readonly int[] TopBitByMask = BuildTopBitTable();
        private static readonly int[] BottomBitByMask = BuildBottomBitTable();
        private static readonly int[] PopCountByMask = BuildPopCountTable();

        private static int[] BuildTopBitTable()
        {
            var table = new int[256];
            for (var i = 0; i < 256; i++)
            {
                var pos = -1;
                var bits = i;
                while (bits != 0) { pos++; bits >>= 1; }
                table[i] = pos;
            }
            return table;
        }

        private static int[] BuildBottomBitTable()
        {
            var table = new int[256];
            table[0] = -1;
            for (var i = 1; i < 256; i++)
            {
                var pos = 0;
                var bits = i;
                while ((bits & 1) == 0) { pos++; bits >>= 1; }
                table[i] = pos;
            }
            return table;
        }

        private static int[] BuildPopCountTable()
        {
            var table = new int[256];
            for (var i = 0; i < 256; i++)
            {
                var n = 0;
                var bits = i;
                while (bits != 0) { n += bits & 1; bits >>= 1; }
                table[i] = n;
            }
            return table;
        }

        /// <summary>
        /// Build a 32-entry chord intrinsic table for the given options.
        /// Chord masks only use 5 bits (G, R, Y, B, O), so a single
        /// precomputed table eliminates per-note ChordIntrinsic calls.
        /// </summary>
        private static double[] BuildIntrinsicTable(DifficultyOptions options)
        {
            var table = new double[32];
            for (var mask = 0; mask < 32; mask++)
                table[mask] = ChordIntrinsic((byte)mask, options);
            return table;
        }

        private static int TopBit(byte bits) => TopBitByMask[bits];

        private static int BottomBit(byte bits) => BottomBitByMask[bits];

        private static int PopCount(byte b) => PopCountByMask[b];

        private static double LookupJumpCost(int distance, double[] table)
        {
            if (distance < 0 || table == null || distance >= table.Length) return 0.0;
            return table[distance];
        }
    }
}
