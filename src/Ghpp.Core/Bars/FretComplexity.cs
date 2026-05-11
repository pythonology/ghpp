using System;
using System.Collections.Generic;
using Ghpp.Core.Abstractions;
using Ghpp.Core.Aggregation;
using Ghpp.Core.Models;

namespace Ghpp.Core.Bars
{
    /// <summary>
    /// Fret-hand complexity axis. Walks the note stream and segments it into
    /// motion chunks (Trill, RollOn, RollOff, Zig, or Free) by relative timing
    /// and sequence shape. Each non-Free chunk pays a single
    /// <see cref="DifficultyOptions.PatternOnsetCost"/> on its first note plus
    /// a small per-note hold-strain on every subsequent note — modeling a
    /// chunk as one wrist motion that is expensive to start and cheap (but
    /// never zero) to sustain. <see cref="ChordIntrinsic"/> shape cost is
    /// added to every note regardless of chunk membership. The K-period
    /// repeat detector gates the per-chunk TransitionBoost: chunks that
    /// extend an ongoing repeat run skip the boost (the cognitive cost of
    /// switching motions only applies on genuine transitions). Repeated
    /// chunks otherwise pay full weight — there is no separate "follow
    /// discount", because the transition system on its own already accounts
    /// for the difficulty difference between fresh and repeated motion.
    /// </summary>
    /// <summary>
    /// Output of <see cref="FretComplexity.Build"/>: the sliding-window
    /// difficulty curve plus the chunk record stream so visualizers can
    /// color notes by chunk classification.
    /// </summary>
    internal readonly struct FretComplexityResult
    {
        public FretComplexityResult(
            DifficultyCurve curve,
            IReadOnlyList<FretChunkRecord> chunks,
            bool[] noteIsAnchor)
        {
            Curve = curve;
            Chunks = chunks;
            NoteIsAnchor = noteIsAnchor;
        }

        public DifficultyCurve Curve { get; }
        public IReadOnlyList<FretChunkRecord> Chunks { get; }

        /// <summary>
        /// Per-note flag: <c>true</c> when the note's fret position equals
        /// the lowest fret in its enclosing chunk. Anchors are shared across
        /// all chunk shapes, including 1-note Free chunks (a Free chunk's
        /// only note is trivially the anchor of itself). Visualizers paint
        /// anchors gray to keep them visually distinct from the pattern's
        /// "target" notes.
        /// </summary>
        public bool[] NoteIsAnchor { get; }
    }

    internal static class FretComplexity
    {
        public static FretComplexityResult Build(
            IList<NoteContext> notes,
            DifficultyOptions options,
            double startTime,
            int sampleCount)
        {
            var noteIsAnchor = new bool[notes.Count];
            var chunkWeightsBuf = new List<double>();
            var chunks = ApplyCosts(notes, options, noteIsAnchor, chunkWeightsBuf);

            // Weight model: each chunk = one curve event, with constant weight
            // (does NOT scale with chunk note count). The chunk's weight was
            // already computed per-chunk inside ApplyCosts:
            //   Free    = chord_intrinsic(note)
            //   Held    = chord_intrinsic(union chord, paid once)
            //   Motion  = onset + hold_strain + max chord_intrinsic in chunk
            // (each scaled by the K-period repeat discount when applicable).
            //
            // Here we just place that weight at the chunk's start time,
            // multiplied by the start note's TypeWeight (Strum/Hopo/Tap), and
            // add the TransitionBoost when this chunk genuinely marks a
            // pattern change (not a repeat, not Free, not Held).
            //
            // Every non-start note in the chunk gets weight 0 — they share
            // the chunk's single NPS event, they don't add new ones.
            // Pre-compute the NPS of each chunk's enclosing section. A
            // "section" is a maximal run of chunks whose start-note Type
            // matches — i.e. a continuous strum stream, a continuous tap
            // stream, etc. NPS is measured over the whole section, so the
            // transition boost can decide based on the actual pace of the
            // section being entered/exited rather than the local inter-note
            // gap (which is misleading at boundaries between same-pace
            // sections of different types).
            var sectionNps = ComputeSectionNps(chunks, notes);

            var weights = new double[notes.Count];
            int prevType = -1;
            for (int c = 0; c < chunks.Count; c++)
            {
                var chunk = chunks[c];
                int start = chunk.StartNoteIndex;
                int end   = chunk.EndNoteIndex;
                if (start < 0 || end >= notes.Count || start > end) continue;

                var startNoteType = notes[start].Source.Type;
                double typeWeight = DifficultyCurve.TypeWeight(startNoteType, options);
                double w = typeWeight * chunkWeightsBuf[c];

                // Repeat discount: continuation chunks of an ongoing K-period
                // pattern are easier than fresh chunks because the hand is
                // already shaped/timed for them. Applied to the base chunk
                // weight only — the transition boosts below are explicit
                // "fresh" bonuses and shouldn't be discounted.
                if (chunk.InRepeat)
                {
                    w *= options.RepeatDiscount;
                }

                if (chunk.Shape != FretChunkShape.Free
                    && chunk.Shape != FretChunkShape.Held
                    && !chunk.InRepeat)
                {
                    w += options.TransitionBoost;
                }

                // Mode-change boost. Asymmetric by which side of the
                // transition is a Strum:
                //   - Strum-involved transition (Strum↔Tap, Strum↔Hopo):
                //     pay StrumModeTransitionBoost · exp(−(1/dominantNps)/K).
                //     The dominant section NPS — max(prev section NPS, new
                //     section NPS) — sets the second-hand demand. A
                //     fast-tap chorus next to either a slow or fast strum
                //     verse is the difficult case; a slow tap fill is
                //     forgiving regardless of where it sits. 1/dominantNps
                //     is the per-note time available, plugged into the
                //     same exponential-decay shape as the legacy gap-based
                //     formula so the K constant tunes identically.
                //   - Tap↔Hopo transition: pay the smaller TypeTransitionBoost
                //     unconditionally. Both are tap-driven, no second-hand
                //     engagement change, so the cost is gap-independent.
                int curType = (int) startNoteType;
                if (prevType >= 0 && prevType != curType)
                {
                    bool involvesStrum = prevType == (int) NoteType.Strum
                                      || curType  == (int) NoteType.Strum;
                    if (involvesStrum)
                    {
                        // The previous chunk closed the previous section,
                        // so sectionNps[c-1] is its NPS. sectionNps[c] is
                        // the new section starting at this chunk.
                        double prevSecNps = c > 0 ? sectionNps[c - 1] : 0;
                        double curSecNps  = sectionNps[c];
                        double dominantNps = Math.Max(prevSecNps, curSecNps);
                        double effectiveGap = dominantNps > 0
                            ? 1.0 / dominantNps
                            : double.PositiveInfinity;
                        var k = Math.Max(1e-9, options.StrumTransitionGapK);
                        w += options.StrumModeTransitionBoost * Math.Exp(-effectiveGap / k);
                    }
                    else
                    {
                        w += options.TypeTransitionBoost;
                    }
                }
                prevType = curType;

                weights[start] = w;
            }

            var curve = DifficultyCurve.BuildSliding(
                (IReadOnlyList<NoteContext>)notes, weights, options, startTime, sampleCount);

            // Low-NPS rolloff on the fret curve. raw·(1 − e^(−raw/k))
            // dampens low values quadratically (sparse fret events don't
            // register full per-event difficulty) but converges to raw at
            // high values — no cap, so dense sections still scale linearly.
            // A small saturating floor is added on top so sparse fret motion
            // registers a positive contribution instead of near-zero.
            var fcValues = curve.Values;
            var fcK = Math.Max(1e-9, options.FretNpsSaturationK);
            var fcFloor = options.FretNpsFloor;
            var fcFloorK = Math.Max(1e-9, options.FretNpsFloorK);
            for (var i = 0; i < fcValues.Length; i++)
            {
                var raw = fcValues[i];
                if (raw <= 0)
                {
                    fcValues[i] = 0;
                    continue;
                }
                var dampened = raw * (1.0 - Math.Exp(-raw / fcK));
                var floorContribution = fcFloor * (1.0 - Math.Exp(-raw / fcFloorK));
                fcValues[i] = dampened + floorContribution;
            }

            return new FretComplexityResult(curve, chunks, noteIsAnchor);
        }

        // ===== Chunk model ==================================================

        private enum ChunkShape : byte
        {
            Free,    // not part of a recognized motion chunk
            Trill,   // anchor alternates with one or more upper targets
            RollOn,  // strictly ascending top-frets, optional skips
            RollOff, // strictly descending top-frets, optional skips
            Zig,     // exactly one direction reversal
            Held,    // multi-note run playable by holding the union chord (zero fret motion)
        }

        private struct ChunkSignature : IEquatable<ChunkSignature>
        {
            public ChunkShape Shape;
            public sbyte Anchor;
            public byte Span;
            public byte Length;
            public byte SkipCount;

            public bool Equals(ChunkSignature other) =>
                Shape == other.Shape && Anchor == other.Anchor && Span == other.Span
                && Length == other.Length && SkipCount == other.SkipCount;

            public override bool Equals(object obj) => obj is ChunkSignature s && Equals(s);
            public override int GetHashCode() => 0;
        }

        private struct Chunk
        {
            public int Start;
            public int End;
            public ChunkShape Shape;
            public int Anchor;          // lowest *effective* fret position in the chunk
            public int Span;            // top - anchor + 1 (in effective space)
            public int SkipCount;       // total fret positions skipped within the shape
            public int DistinctTargets; // distinct non-anchor frets, used for Trill
            public bool ReverseZig;     // anchor sits in the chunk's interior
            // Bits pressed on EVERY note in the chunk — the "held common fret"
            // that cancels out when classifying chord-tap patterns like
            // BO -> YO -> RO. Subtracting these bits leaves the per-note
            // motion that the chunker reasons about.
            public byte SharedFretBits;
            public ChunkSignature Signature;
        }

        // ===== Cost application ==============================================

        /// <summary>
        /// Walk the chart greedily, peeling off one chunk at a time and billing
        /// every note inside it. A rolling chunk-signature buffer drives
        /// multi-chunk pattern detection: if the last K classified chunks
        /// exactly equal the K chunks before them (for any K up to
        /// <see cref="DifficultyOptions.ChunkPatternMaxLength"/>), the new
        /// chunk is flagged InRepeat. That flag suppresses the
        /// <see cref="DifficultyOptions.TransitionBoost"/> on the chunk in
        /// Build (only fresh transitions pay the cognitive bump). The
        /// per-chunk weight itself is unchanged by the repeat flag — the
        /// transition system alone accounts for the new-vs-repeated
        /// difficulty difference.
        /// </summary>
        private static IReadOnlyList<FretChunkRecord> ApplyCosts(
            IList<NoteContext> notes, DifficultyOptions options,
            bool[] noteIsAnchor, List<double> chunkWeights)
        {
            var intrinsicTable = BuildIntrinsicTable(options);

            // History is capped at 2 * MaxK because the K-period check never
            // looks back further than 2K entries.
            var historyCapacity = Math.Max(2, 2 * options.ChunkPatternMaxLength);
            var chunkHistory = new List<ChunkSignature>(historyCapacity);

            var emitted = new List<FretChunkRecord>();

            // Track the previous chunk's last-note time so we can detect
            // tempo gaps between chunks and reset the K-period repeat
            // detector across them. Without this, a chart that interleaves
            // identical trill bursts with rests still flags every post-rest
            // chunk as InRepeat → no transition boost paid → the curve
            // matches a continuously sustained trill, which feels wrong
            // (the rests genuinely complicate the section).
            double prevChunkEndTime = double.NegativeInfinity;

            // Track the previous chunk's shape and anchor so we can detect
            // anchor changes between consecutive Trills and pay the
            // TrillAnchorTransitionBoost — re-anchoring the fret hand
            // between trill pairs costs more than maintaining the same
            // anchor across a trill stream.
            ChunkShape prevShape = ChunkShape.Free;
            int prevAnchor = int.MinValue;

            var i = 0;
            while (i < notes.Count)
            {
                var chunk = ScanNextChunk(notes, i, options);

                // Reset chunk-history when this chunk follows a long gap.
                // The gap threshold is intentionally larger than the in-run
                // tempo tolerance — a few-millisecond stutter shouldn't
                // count, but a noticeable rest should.
                var chunkStartTime = notes[chunk.Start].Source.TimeSeconds;
                if (chunkStartTime - prevChunkEndTime > options.TransitionGapSeconds)
                {
                    chunkHistory.Clear();
                }

                if (chunkHistory.Count >= historyCapacity)
                    chunkHistory.RemoveAt(0);
                chunkHistory.Add(chunk.Signature);

                // InRepeat is still tracked: it gates the TransitionBoost in
                // Build (only paid when the chunk is a genuine new pattern,
                // not part of an ongoing repeat). The transition boost alone
                // accounts for the cognitive cost of switching motions —
                // repeated chunks pay full per-chunk weight, just no boost.
                var inRepeat = DetectChunkPeriod(chunkHistory, options.ChunkPatternMaxLength);

                prevChunkEndTime = notes[chunk.End].Source.TimeSeconds;

                // ---- Per-note metadata pass: anchor mask + non-anchor
                //      count + max chord intrinsic. Has to run before the
                //      chunk-weight computation because the weight scales
                //      with the non-anchor tap count.
                int nonAnchorCount = 0;
                double maxChord = 0.0;
                for (var j = chunk.Start; j <= chunk.End; j++)
                {
                    var note = notes[j];

                    if (chunk.Shape == ChunkShape.Held)
                    {
                        // Every note in a Held chunk is effectively an
                        // anchor — no fret-motion target exists.
                        noteIsAnchor[j] = true;
                        continue;
                    }

                    // A note is an "anchor" when its EFFECTIVE position
                    // (top remaining bit after stripping chord-shared bits)
                    // equals the chunk's lowest effective position. For a
                    // 1-note Free chunk this is trivially true. For Trills
                    // this picks out the alternating bottom fret. For chord
                    // patterns like BO->YO->RO this correctly tags only RO
                    // (effective Red, the lowest of B/Y/R after stripping
                    // the held Orange) as the anchor. For GR -> GRY, GR's
                    // effective bits are 0 (entire chord is the shared mask)
                    // — that's the chunk's -1 below-everything anchor.
                    var rem = (byte) ((byte) note.Source.Frets & ~chunk.SharedFretBits);
                    int effPos = rem == 0 ? -1 : TopBitByMask[rem];
                    bool isAnchor = (effPos == chunk.Anchor);
                    noteIsAnchor[j] = isAnchor;
                    if (!isAnchor) nonAnchorCount++;

                    if (!note.Source.IsOpen)
                    {
                        var c = intrinsicTable[(byte) note.Source.Frets];
                        if (c > maxChord) maxChord = c;
                    }
                }

                // ---- Per-chunk weight ----
                //
                //  Free   = chord_intrinsic(note)              (1 input)
                //  Held   = chord_intrinsic(union)             (0 active inputs — held)
                //  Motion = onset + max_chord_intrinsic + hold_strain × non_anchor_count
                //
                // The non-anchor count is the count of REAL fret-motion taps
                // the player makes inside the chunk — the trill targets, the
                // roll's added frets, etc. A 6-note Trill RYRYRY has 3 Y taps
                // (non-anchor); RYRY has 2. Holding a single anchor doesn't
                // count as an active input.
                //
                // The chunk still produces ONE curve event at its start time;
                // we only scale that event's weight by tap count, not its
                // count.
                double chunkWeight;
                if (chunk.Shape == ChunkShape.Free)
                {
                    var startNote = notes[chunk.Start];
                    if (startNote.Source.IsOpen)
                    {
                        chunkWeight = 0.0;
                    }
                    else
                    {
                        var frets = (byte) startNote.Source.Frets;
                        var intrinsic = intrinsicTable[frets];
                        // Strum-typed Free chunks: chord intrinsic is
                        // optionally scaled by StrumFretWeight (default 1.0
                        // = full intrinsic). The fret hand is committed to
                        // holding the chord during the strum, so the fret
                        // cost matches a tap/HOPO at the same chord shape.
                        // The simultaneous strum-curve event captures the
                        // picking work; the Lp-norm composite then weights
                        // strums above taps overall because two axes
                        // register at once — that's where the "two-hand
                        // commitment" cost shows up. The single-fret carve-out
                        // (only apply the multiplier to single-fret strums)
                        // remains so a user could discount the fret-curve
                        // contribution of one-fret strumming if desired
                        // without affecting chord strums.
                        bool isSingleFretStrum = startNote.Source.Type == NoteType.Strum
                                              && PopCount(frets) < 2;
                        if (isSingleFretStrum)
                            intrinsic *= options.StrumFretWeight;
                        chunkWeight = intrinsic;
                    }
                }
                else if (chunk.Shape == ChunkShape.Held)
                {
                    chunkWeight = intrinsicTable[chunk.SharedFretBits];
                }
                else
                {
                    // Trills hold the lower-fret anchor and only the upper
                    // target moves, so the held chord shape contributes less
                    // to felt difficulty than it does for Roll/Zig (which
                    // walk the chord finger-by-finger). Scale the maxChord
                    // term down for Trills so a GRYB→O alternation isn't
                    // weighted as if the player were re-forming GRYB on
                    // every tap.
                    double chordTerm = chunk.Shape == ChunkShape.Trill
                        ? maxChord * options.TrillChordWeight
                        : maxChord;
                    // ShapeWeightScale damps the combined shape-complexity
                    // contribution (chord + per-tap hold-strain) so chunks
                    // don't dominate the curve via raw structural weight.
                    // Onset stays unscaled — every motion chunk pays the
                    // full wrist-startup cost regardless of size.
                    // NonAnchorExponent super-linearly rewards longer rolls
                    // (quints feel materially harder than triples even
                    // though they're "just two more notes").
                    var nonAnchorWeight = options.NonAnchorExponent == 1.0
                        ? (double) nonAnchorCount
                        : Math.Pow(nonAnchorCount, options.NonAnchorExponent);
                    chunkWeight = options.PatternOnsetCost
                                + (chordTerm
                                   + ComputeHoldStrain(chunk, options) * nonAnchorWeight)
                                  * options.ShapeWeightScale;
                }
                // Trill anchor-transition bonus: when consecutive trills
                // change their held anchor (e.g. R-Y trill followed by G-R
                // trill), the player has to re-anchor their fret hand.
                // Only applies Trill→Trill with different anchors; other
                // shape transitions are covered by the general
                // TransitionBoost / StrumModeTransitionBoost in Build.
                if (chunk.Shape == ChunkShape.Trill
                    && prevShape == ChunkShape.Trill
                    && chunk.Anchor != prevAnchor)
                {
                    chunkWeight += options.TrillAnchorTransitionBoost;
                }
                prevShape = chunk.Shape;
                prevAnchor = chunk.Anchor;

                chunkWeights.Add(chunkWeight);

                emitted.Add(new FretChunkRecord(
                    chunk.Start, chunk.End, ToPublicShape(chunk.Shape), inRepeat));

                i = chunk.End + 1;
            }

            return emitted;
        }

        private static FretChunkShape ToPublicShape(ChunkShape shape)
        {
            switch (shape)
            {
                case ChunkShape.Trill:   return FretChunkShape.Trill;
                case ChunkShape.RollOn:  return FretChunkShape.RollOn;
                case ChunkShape.RollOff: return FretChunkShape.RollOff;
                case ChunkShape.Zig:     return FretChunkShape.Zig;
                case ChunkShape.Held:    return FretChunkShape.Held;
                default:                 return FretChunkShape.Free;
            }
        }

        /// <summary>
        /// Return <c>true</c> if the most recent K entries in
        /// <paramref name="history"/> exactly match the K entries before them,
        /// for some K in 1..<paramref name="maxK"/>. Smallest matching period
        /// wins — the loop returns at the first K that fits.
        /// </summary>
        private static bool DetectChunkPeriod(List<ChunkSignature> history, int maxK)
        {
            var n = history.Count;
            var maxKActual = Math.Min(maxK, n / 2);
            for (var k = 1; k <= maxKActual; k++)
            {
                var match = true;
                for (var j = 0; j < k; j++)
                {
                    if (!history[n - 1 - j].Equals(history[n - 1 - j - k]))
                    {
                        match = false;
                        break;
                    }
                }
                if (match) return true;
            }
            return false;
        }

        // ===== Chunker ======================================================

        /// <summary>
        /// Find the longest tempo-coherent run starting at <paramref name="start"/>,
        /// then return the longest classifiable prefix of that run as a chunk.
        /// Falls back to a 1-note Free chunk when nothing classifies.
        /// </summary>
        private static Chunk ScanNextChunk(IList<NoteContext> notes, int start, DifficultyOptions options)
        {
            // The first note of a chunk MAY be a strum — it "kicks off" a
            // motion run (Strum G -> Tap R -> Tap Y is a valid RollOn).
            // FindTempoRunEnd / Classify enforce that subsequent notes can't
            // be strums, so a stretch of pure strums still falls through to
            // 1-note Free chunks one strum at a time.
            var runEnd = FindTempoRunEnd(notes, start, options);
            var maxLen = Math.Min(runEnd - start + 1, options.ChunkMaxLength);

            // Roll-first priority: find the longest monotonic (no-reversal)
            // run starting at `start`, using each note's TopBit fret
            // position. If it produces a Roll of >=3 notes, commit to that
            // rather than letting a longer Zig consume it. This means a
            // sequence like O→B→Y→R→Y→B→Y→R→G splits as RollOff(OBYR)
            // followed by Zig(YBYRG) instead of Zig(OBYRYB) + RollOff(YRG).
            //
            // We use raw FretPosition (top bit) for the scan rather than
            // the shared-mask-stripped EffPos because shared-mask
            // cancellation depends on the range being classified, which
            // we don't know yet. Chord patterns that depend on shared-mask
            // cancellation (e.g. BO→YO→RO) will have constant raw top
            // frets and won't be detected here — they'll fall through to
            // the length-descending loop below where Classify handles the
            // shared-mask logic.
            int rollEnd = FindLongestMonotonicRunEnd(notes, start, start + maxLen - 1);
            int rollLen = rollEnd - start + 1;
            if (rollLen >= 3)
            {
                var c = Classify(notes, start, rollEnd);
                // Accept whatever Classify returns IF it's a non-Free motion
                // shape. This handles Held (always preferred), RollOn/RollOff
                // (the target), or whatever else the classifier picks.
                if (c.Shape != ChunkShape.Free
                    && !(c.Shape == ChunkShape.Trill && rollLen != 2))
                {
                    c.Start = start;
                    c.End = rollEnd;
                    c.Signature = MakeSignature(c);
                    return c;
                }
            }

            // Fall back: try longer ranges first, picking up Held / Zig /
            // shared-mask-driven Rolls / 2-note Trills as appropriate.
            for (var len = maxLen; len >= 2; len--)
            {
                var c = Classify(notes, start, start + len - 1);
                if (c.Shape == ChunkShape.Free) continue;
                // Trill is now exclusively the 2-note unit; reject any Trill
                // that arrives at len > 2 (defensive — Classify shouldn't
                // emit one anyway since 3+ alternating detection was removed).
                if (c.Shape == ChunkShape.Trill && len != 2) continue;
                c.Start = start;
                c.End = start + len - 1;
                c.Signature = MakeSignature(c);
                return c;
            }

            return new Chunk
            {
                Shape = ChunkShape.Free,
                Start = start,
                End = start,
                Anchor = notes[start].FretPosition,
                Signature = default,
            };
        }

        /// <summary>
        /// Walk forward from <paramref name="start"/> while the top-fret
        /// position stays monotonic (non-decreasing OR non-increasing) and
        /// has at least one strict step. Returns the last index that fits.
        /// Used by ScanNextChunk to commit to clean Roll motion before
        /// the longest-length-first loop has a chance to find a containing
        /// Zig.
        /// </summary>
        private static int FindLongestMonotonicRunEnd(IList<NoteContext> notes, int start, int maxEnd)
        {
            if (maxEnd <= start) return start;

            int seenDir = 0;  // +1 ascending, -1 descending, 0 still flat
            int last = start;
            for (var k = start + 1; k <= maxEnd; k++)
            {
                var diff = notes[k].FretPosition - notes[k - 1].FretPosition;
                if (diff == 0)
                {
                    // Flat steps don't break monotonicity (chord-release
                    // patterns can include them, e.g. RYB→B is flat in
                    // top-fret terms but part of a descending roll).
                    last = k;
                    continue;
                }
                var dir = diff > 0 ? 1 : -1;
                if (seenDir == 0)
                {
                    seenDir = dir;
                    last = k;
                }
                else if (dir == seenDir)
                {
                    last = k;
                }
                else
                {
                    break;
                }
            }
            return last;
        }

        private static bool IsStrum(NoteContext note)
            => note.Source.Type == NoteType.Strum;

        /// <summary>
        /// Walk forward while inter-note deltas stay coherent: the first delta
        /// sets the run's tempo baseline, and each subsequent delta must sit
        /// within ±<see cref="DifficultyOptions.ChunkDeltaTolerance"/> of that
        /// baseline. Hard-capped by
        /// <see cref="DifficultyOptions.ChunkMaxInterNoteSeconds"/>.
        /// </summary>
        private static int FindTempoRunEnd(IList<NoteContext> notes, int start, DifficultyOptions options)
        {
            var maxDelta = options.ChunkMaxInterNoteSeconds;
            var tolerance = options.ChunkDeltaTolerance;
            var runEnd = start;
            var baseDelta = -1.0;

            while (runEnd + 1 < notes.Count)
            {
                // Tempo coherence is the only run constraint. We deliberately
                // do NOT break on strums here: a long Held chain like
                // strum GRY -> tap GRY -> strum GRY -> tap GRY needs to span
                // every note so it gets emitted as ONE Held chunk that pays
                // the chord intrinsic once at the start. Classify enforces
                // the strum-only-at-position-0 rule for motion shapes; Held
                // doesn't care about strum/tap distinction.
                var delta = notes[runEnd + 1].Source.TimeSeconds - notes[runEnd].Source.TimeSeconds;
                if (delta > maxDelta) break;

                if (baseDelta < 0)
                {
                    baseDelta = delta;
                }
                else
                {
                    var ratio = delta / Math.Max(baseDelta, 1e-9);
                    if (ratio > 1.0 + tolerance || ratio < 1.0 - tolerance) break;
                }

                runEnd++;
            }

            return runEnd;
        }

        /// <summary>
        /// Classify a fixed-range candidate chunk. Open notes always break a
        /// chunk; strums break it everywhere except at index 0 (the FIRST note
        /// of a chunk is allowed to be a strum that "kicks off" the motion).
        /// Chord patterns that share a held common fret across every note in
        /// the range have those bits stripped before classification, so
        /// e.g. BO->YO->RO classifies as RollOff on B->Y->R.
        /// </summary>
        private static Chunk Classify(IList<NoteContext> notes, int start, int end)
        {
            var len = end - start + 1;
            if (len < 2) return new Chunk { Shape = ChunkShape.Free };

            // Open notes break a chunk anywhere — they have no fret bits to
            // contribute to motion or held-chord reasoning.
            for (var k = 0; k < len; k++)
            {
                if (notes[start + k].Source.IsOpen)
                    return new Chunk { Shape = ChunkShape.Free };
            }

            // ---- Held detection (chain-input semantics) ----
            // Held is checked BEFORE the strum filter because strums never
            // require fret motion — a string of strum/tap GRYs all firing off
            // the same held chord is still 0 fret cost. Without this ordering
            // the strum-only-at-position-0 rule would chop a long held chain
            // into many short Held chunks, each re-paying the chord intrinsic
            // and producing an artificially sustained curve.
            // A run is Held when every consecutive note pair can be played
            // without changing held frets — i.e. there exists a single fixed
            // chord that fires every note in the run. Equivalently, holding
            // the union H of all pressed bits, every note N satisfies
            //   (H & ~bitsBelowLow(Nb)) == Nb
            // — above N's lowest pressed bit, H must contain exactly N's bits
            // (no extra bits in the chord's "span"). Bits strictly below N's
            // lowest are anchored and don't matter. Holding H throughout is
            // therefore a "0-cost chain" of strums/hopos/taps: the player
            // never moves a finger.
            //
            // Catches:
            //   GRY -> Y -> GRY -> Y    (Y fires off held GRY: G+R anchored)
            //   GRYBO -> BO -> GRYBO    (BO fires off GRYBO: G+R+Y anchored)
            // Rejects:
            //   BO -> YO -> RO          (holding RYBO puts B between Y and O
            //                            when YO needs to fire alone)
            //   GR -> GRY               (holding GRY breaks GR — Y is above
            //                            GR's top fret R, so GR can't fire)
            byte unionBits = 0;
            for (var k = 0; k < len; k++)
                unionBits |= (byte) notes[start + k].Source.Frets;

            bool isHeld = true;
            for (var k = 0; k < len; k++)
            {
                var nb = (byte) notes[start + k].Source.Frets;
                if (nb == 0) { isHeld = false; break; } // shouldn't happen — open filtered above
                int bottom = BottomBitByMask[nb];
                byte anchorMask = (byte) ((1 << bottom) - 1);
                if ((unionBits & ~anchorMask) != nb)
                {
                    isHeld = false;
                    break;
                }
            }

            if (isHeld)
            {
                return new Chunk
                {
                    Shape          = ChunkShape.Held,
                    Anchor         = TopBitByMask[unionBits],
                    Span           = TopBitByMask[unionBits] - BottomBitByMask[unionBits] + 1,
                    SkipCount      = 0,
                    SharedFretBits = unionBits,
                };
            }

            // ---- Motion classification: strum filter ----
            // Past this point we're looking for fret-motion shapes. Strums
            // only kick off motion at index 0 (Strum G -> Tap R -> Tap Y is
            // a valid RollOn); a strum mid-run breaks the chunk because we
            // don't classify strum-driven sequences as motion patterns.
            for (var k = 1; k < len; k++)
            {
                if (notes[start + k].Source.Type == NoteType.Strum)
                    return new Chunk { Shape = ChunkShape.Free };
            }

            // ---- Shared-bit cancellation ----
            // For chord-tap patterns where every note shares some held fret
            // (e.g. BO -> YO -> RO with O held), strip those bits before
            // classifying. The remaining motion (B -> Y -> R in that example)
            // drives the Trill/Roll/Zig logic.
            byte sharedBits = 0xFF;
            for (var k = 0; k < len; k++)
            {
                sharedBits &= (byte) notes[start + k].Source.Frets;
            }

            // Per-note effective position: the topmost remaining fret bit
            // after stripping the shared bits. Returns -1 when the note has
            // no distinguishing bits — that note IS the shared mask, and
            // serves as the chunk's "below-everything" anchor (e.g. GR in
            // GR -> GRY: GR has no bits left after stripping shared GR, so
            // it sits below GRY's effective Y position and the run is a
            // RollOn from GR-anchor up to Y-target).
            int EffPos(int idx)
            {
                var rem = (byte) ((byte) notes[start + idx].Source.Frets & ~sharedBits);
                if (rem == 0) return -1;
                return TopBitByMask[rem];
            }

            // 2-note classifier: any 2-note range with differing effective
            // positions is a Trill. Trills are the unit "tap chunk": one fret
            // change = one Trill = one NPS event. Longer same-shape runs
            // (3+ ascending / descending / one-reversal / chord-held) are
            // handled by the multi-note path below; anything that doesn't
            // fit a longer shape falls back to 2-note Trills via the loop in
            // ScanNextChunk.
            if (len == 2)
            {
                var p0 = EffPos(0);
                var p1 = EffPos(1);
                if (p0 == p1) return new Chunk { Shape = ChunkShape.Free };
                int anchor2 = Math.Min(p0, p1);
                int top2    = Math.Max(p0, p1);
                int span2   = top2 - anchor2 + 1;
                int skip2   = (top2 - anchor2) - 1; // skipped lanes between the two notes
                return new Chunk
                {
                    Shape           = ChunkShape.Trill,
                    Anchor          = anchor2,
                    Span            = span2,
                    SkipCount       = skip2,
                    DistinctTargets = 1, // exactly one non-anchor target
                    SharedFretBits  = sharedBits,
                };
            }

            int anchor = int.MaxValue, top = int.MinValue;
            for (var k = 0; k < len; k++)
            {
                var p = EffPos(k);
                if (p < anchor) anchor = p;
                if (p > top) top = p;
            }
            var span = top - anchor + 1;

            // Reversal count + skip total over the sequence (in effective space).
            int reversals = 0, prevDir = 0, skipCount = 0;
            bool anyMotion = false;
            for (var k = 1; k < len; k++)
            {
                var diff = EffPos(k) - EffPos(k - 1);
                // Flat transitions (diff==0) are allowed within a motion
                // chunk — they show up in chord-release patterns like
                // RYB(strum)→B(tap)→Y(tap)→R(tap) where the top fret
                // stays at B for the first transition (RYB→B) before
                // descending. Skip them rather than aborting; classify
                // based on the actual non-zero motion.
                if (diff == 0) continue;
                anyMotion = true;
                var dir = diff > 0 ? 1 : -1;
                skipCount += Math.Abs(diff) - 1;
                if (prevDir != 0 && dir != prevDir) reversals++;
                prevDir = dir;
            }
            if (!anyMotion) return new Chunk { Shape = ChunkShape.Free };

            // NOTE: 3+ note alternating "compound trill" detection is
            // intentionally absent. A Trill is the 2-note unit; longer
            // alternating runs like RYRY/RYRYRY don't fit Roll (reversals>0)
            // or Zig (require exactly one reversal AND len>=4), so they fall
            // back to consecutive 2-note Trill chunks via the ScanNextChunk
            // loop. Each pair becomes its own NPS event.

            if (reversals == 0)
            {
                var ascending = EffPos(len - 1) > EffPos(0);
                return new Chunk
                {
                    Shape = ascending ? ChunkShape.RollOn : ChunkShape.RollOff,
                    Anchor = anchor,
                    Span = span,
                    SkipCount = skipCount,
                    SharedFretBits = sharedBits,
                };
            }

            // Zig requires len >= 4. A 3-note "zig" like RYR is more
            // naturally read as two adjacent 2-note Trills (R->Y, Y->R) than
            // as a peaked motion shape — keeping the floor at 4 lets the
            // chunker prefer 2-note Trills for short alternations and
            // reserve Zig for genuinely long peaked/valleyed runs like
            // GRYRG (5-note peak) or GYRYG (reverse peak).
            // Chimney detection: multi-reversal alternating patterns like
            // RYBYRY (positions 1,2,3,2,1,2) where motion stays adjacent
            // and visits 3+ distinct positions. Distinguishes a "chimney"
            // (legitimate compound zig-zag) from a phase-reset pattern like
            // BYRBYR (positions 3,2,1,3,2,1) whose +2 teleport is the
            // signature of a repeated descending Roll. Max |diff| = 1 +
            // 3+ distinct positions is the precise filter.
            if (reversals >= 2 && len >= 5)
            {
                int maxAbsDiff = 0;
                for (var k = 1; k < len; k++)
                {
                    var d = Math.Abs(EffPos(k) - EffPos(k - 1));
                    if (d > maxAbsDiff) maxAbsDiff = d;
                }
                if (maxAbsDiff == 1)
                {
                    // Count distinct effective positions. We're operating
                    // in fret-position space (0..4 for non-shared bits, or
                    // -1 for "shared mask only" notes), so an int bitset
                    // keyed on (EffPos + 1) covers the range cleanly.
                    int distinctMask = 0;
                    for (var k = 0; k < len; k++)
                    {
                        int pos = EffPos(k);
                        distinctMask |= 1 << (pos + 1);
                    }
                    int distinct = PopCount((byte) distinctMask);
                    if (distinct >= 3)
                    {
                        return new Chunk
                        {
                            Shape = ChunkShape.Zig,
                            Anchor = anchor,
                            Span = span,
                            SkipCount = skipCount,
                            ReverseZig = false,
                            SharedFretBits = sharedBits,
                        };
                    }
                }
            }

            if (reversals == 1 && len >= 4)
            {
                // Locate the reversal index and check that the two
                // segments separated by it actually go in OPPOSITE
                // directions. A "Zig" with same-direction segments is a
                // phase-reset / repeated-roll pattern (e.g. BYRBYR is
                // RollOff repeated, not a peak), and we want the loop to
                // fall to a shorter length that classifies it cleanly as
                // RollOn/RollOff. Both segments must also have at least
                // 2 notes — a 1-note tail after a roll isn't a real Zig
                // peak.
                int reversalIdx = -1;
                int seenDir = 0;
                for (var k = 1; k < len; k++)
                {
                    var d = EffPos(k) - EffPos(k - 1);
                    var dir = d > 0 ? 1 : -1;
                    if (seenDir != 0 && dir != seenDir)
                    {
                        reversalIdx = k;
                        break;
                    }
                    seenDir = dir;
                }

                int seg1Len = reversalIdx;          // notes [0..reversalIdx-1]
                int seg2Len = len - reversalIdx;    // notes [reversalIdx..len-1]
                bool segmentsLongEnough = seg1Len >= 2 && seg2Len >= 2;
                int dir1 = EffPos(reversalIdx - 1) > EffPos(0) ? 1 : -1;
                int dir2 = EffPos(len - 1) > EffPos(reversalIdx) ? 1 : -1;
                bool oppositeDirs = dir1 != dir2;

                if (segmentsLongEnough && oppositeDirs)
                {
                    // Forward zig: anchor sits at one of the chunk's endpoints
                    // (e.g. GRYRG — hand starts at anchor, peaks, returns).
                    // Reverse zig: anchor sits in the chunk's interior (e.g.
                    // YRGYRG-like — hand dips through the anchor mid-motion).
                    // Reverse is slightly harder per the design.
                    var anchorAtStart = EffPos(0) == anchor;
                    var anchorAtEnd = EffPos(len - 1) == anchor;
                    return new Chunk
                    {
                        Shape = ChunkShape.Zig,
                        Anchor = anchor,
                        Span = span,
                        SkipCount = skipCount,
                        ReverseZig = !anchorAtStart && !anchorAtEnd,
                        SharedFretBits = sharedBits,
                    };
                }
                // else: fall through to Free; the loop will retry shorter
                // ranges and find the underlying RollOn/RollOff repetition.
            }

            return new Chunk { Shape = ChunkShape.Free };
        }

        // ===== Cost helpers =================================================

        /// <summary>
        /// Per-note strain billed on every chunk note after the first.
        /// Flat across the chunk so a long pattern keeps billing strain until
        /// it ends, scaled by shape, span, skip count, and (for trills) the
        /// number of distinct upper targets.
        /// </summary>
        private static double ComputeHoldStrain(Chunk chunk, DifficultyOptions options)
        {
            if (chunk.Shape == ChunkShape.Free) return 0.0;

            var spanIdx = Math.Max(0, Math.Min(chunk.Span, options.ShapeSpanCost.Length - 1));
            var spanFactor = options.ShapeSpanCost[spanIdx];
            var skipFactor = 1.0 + chunk.SkipCount * options.SkipPenalty;

            switch (chunk.Shape)
            {
                case ChunkShape.Trill:
                    // Trills don't price span OR skips: they can be played
                    // two-handed (one finger per fret), so the gap between
                    // the anchor and target doesn't translate into
                    // additional finger-motion cost the way it does for a
                    // single-hand roll. Compound-target bump is preserved
                    // so multi-target alternations still scale.
                    return options.TrillHoldStrain
                         * (1.0 + Math.Max(0, chunk.DistinctTargets - 1) * options.CompoundTrillBump);
                case ChunkShape.RollOn:
                case ChunkShape.RollOff:
                    return options.RollHoldStrain * spanFactor * skipFactor;
                case ChunkShape.Zig:
                    return options.ZigHoldStrain * spanFactor * skipFactor
                         * (chunk.ReverseZig ? options.ReverseZigBump : 1.0);
                default:
                    return 0.0;
            }
        }

        /// <summary>
        /// Pre-pass over the emitted chunks: groups consecutive chunks whose
        /// start-note Type matches into "sections", computes each section's
        /// NPS (notes per second), and returns a per-chunk array where
        /// <c>result[c]</c> is the NPS of the section that contains chunk c.
        /// Sections that span fewer than two notes report 0 NPS — there's
        /// no inter-note delta to measure.
        /// </summary>
        private static double[] ComputeSectionNps(
            IReadOnlyList<FretChunkRecord> chunks, IList<NoteContext> notes)
        {
            var result = new double[chunks.Count];
            if (chunks.Count == 0) return result;

            int sectionStart = 0;
            int sectionType = (int) notes[chunks[0].StartNoteIndex].Source.Type;

            for (int c = 1; c <= chunks.Count; c++)
            {
                var endOfSection = c == chunks.Count;
                if (!endOfSection)
                {
                    var t = (int) notes[chunks[c].StartNoteIndex].Source.Type;
                    endOfSection = t != sectionType;
                }

                if (endOfSection)
                {
                    int firstNote = chunks[sectionStart].StartNoteIndex;
                    int lastNote  = chunks[c - 1].EndNoteIndex;
                    int noteCount = lastNote - firstNote + 1;
                    double duration = notes[lastNote].Source.TimeSeconds
                                    - notes[firstNote].Source.TimeSeconds;
                    double nps = (noteCount > 1 && duration > 0)
                        ? (noteCount - 1) / duration
                        : 0.0;

                    for (int s = sectionStart; s < c; s++)
                        result[s] = nps;

                    if (c < chunks.Count)
                    {
                        sectionStart = c;
                        sectionType = (int) notes[chunks[c].StartNoteIndex].Source.Type;
                    }
                }
            }

            return result;
        }

        private static ChunkSignature MakeSignature(Chunk c)
        {
            return new ChunkSignature
            {
                Shape = c.Shape,
                Anchor = (sbyte)Math.Max(-1, Math.Min(127, c.Anchor)),
                Span = (byte)Math.Max(0, Math.Min(255, c.Span)),
                Length = (byte)Math.Min(255, c.End - c.Start + 1),
                SkipCount = (byte)Math.Min(255, c.SkipCount),
            };
        }

        // ===== Chord intrinsic (preserved) ==================================

        /// <summary>
        /// Chord intrinsic difficulty for the given fret mask. Sums four
        /// physical contributions: per-fret placement (lone vs. extending a
        /// consecutive run), per-position interior gap cost, a flat barre cost
        /// when all 5 frets are held, and an outer-pair reduction when only
        /// the bottom + top frets are held with a gap between them.
        /// </summary>
        private static double ChordIntrinsic(byte frets, DifficultyOptions options)
        {
            if (frets == 0) return 0.0;

            var fingers = PopCount(frets);
            var bottom = BottomBit(frets);
            var top = TopBit(frets);

            var runCount = PopCount((byte)(frets & (frets << 1)));
            var loneCount = fingers - runCount;
            var fretCost = loneCount * options.ChordFretLone
                         + runCount * options.ChordFretRun;

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
            var chordScale = options.ChordIntrinsicScale;
            for (var mask = 0; mask < 32; mask++)
            {
                var intrinsic = ChordIntrinsic((byte)mask, options);
                // Buff multi-fret chords; single-fret intrinsics stay as-is.
                if (PopCount((byte)mask) >= 2)
                    intrinsic *= chordScale;
                table[mask] = intrinsic;
            }
            return table;
        }

        private static int TopBit(byte bits) => TopBitByMask[bits];
        private static int BottomBit(byte bits) => BottomBitByMask[bits];
        private static int PopCount(byte b) => PopCountByMask[b];
    }
}
