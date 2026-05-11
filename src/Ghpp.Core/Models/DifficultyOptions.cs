namespace Ghpp.Core.Models
{
    /// <summary>
    /// Tunable constants for the difficulty pipeline. Defaults reproduce the
    /// Python prototype's calibration; expose these as sliders in the editor.
    /// </summary>
    public class DifficultyOptions
    {
        // ---- Type weights ------------------------------------------------------
        public double StrumWeight { get; set; } = 1.3;
        public double HopoWeight { get; set; } = 1.0;
        public double TapWeight { get; set; } = 1.0;

        // ---- Strum complexity (rhythm-complexity signal) -----------------------
        // Each effective-strum's per-note contribution is a baseline plus a
        // rhythm-driven bonus computed from Xexxar island boundaries in a small
        // look-back window. Uniform fast strumming registers as the baseline;
        // vastly-varying patterns saturate near the per-note cap.
        public int RhythmHistoryNotes { get; set; } = 8;
        public double RhythmHistorySeconds { get; set; } = 2.0;

        /// <summary>
        /// Tolerance for treating two consecutive deltas as the same rhythm
        /// class. Default 0.20 (±20%) accounts for rhythm-perception leniency.
        /// </summary>
        public double RhythmDeltaTolerance { get; set; } = 0.20;

        /// <summary>Per-strum baseline contribution. One strum = one unit of cost, mirroring the per-finger action cost in FretComplexity. Constant-rhythm picking sits on the same scale as fret-hand action work.</summary>
        public double StrumBaselineContribution { get; set; } = 1.0;

        /// <summary>
        /// Per-strum additive bonus for strums whose frets differ from the
        /// previous note's. Pure picking on the same fret pays only the
        /// baseline; a strum stream that constantly changes frets pays
        /// baseline + this bonus on every fret-changing note. More
        /// fret-switches in a short time window stack via the sliding-sum,
        /// so dense fret-changing strumming registers materially higher
        /// than dense same-fret strumming at the same NPS.
        /// </summary>
        public double StrumFretChangeBonus { get; set; } = 0.6;

        /// <summary>
        /// Per-note bonus cap. The contribution scales between baseline and
        /// this cap as rhythm variety increases. Sized so the strum bar's
        /// per-note range covers similar magnitudes to the fret bar.
        /// </summary>
        public double StrumMaxBonus { get; set; } = 1.5;

        /// <summary>
        /// Saturation constant for the bonus curve: bonus = max·(1 − e^(−boundaries/k)).
        /// Higher k = bonus accumulates more slowly so genuinely complex
        /// passages differentiate from mildly-varied ones. Lower k makes
        /// minor rhythm variations register as visible spikes.
        /// </summary>
        public double StrumSaturationK { get; set; } = 10.0;

        /// <summary>
        /// Low-NPS rolloff constant for the strum curve. After the sliding
        /// sum, each raw value is passed through raw·(1 − e^(−raw/k)), which
        /// dampens low values (quadratic at the bottom). The output is then
        /// further amplified by the StrumNpsExponent power curve so high
        /// NPS exceeds the raw input, captured by the formula in
        /// <see cref="StrumNpsExponent"/>. Higher k pushes more of the
        /// strum-stream-rate range into the dampened zone.
        /// </summary>
        public double StrumNpsSaturationK { get; set; } = 15.0;

        /// <summary>
        /// Power exponent applied to the strum curve after the saturation
        /// rolloff. Final shape is
        ///   output = saturated · (raw / StrumNpsMidpoint)^(exponent − 1)
        /// so that low-NPS values get squashed twice (saturation + power
        /// damping below the midpoint) while high-NPS values get amplified
        /// (super-linear above the midpoint). Set to 1.0 to disable the
        /// boost (saturation only). Default 1.3 produces a noticeably
        /// concave curve at low NPS and a near-linear-with-bonus shape past
        /// the midpoint, matching how players experience strum streams:
        /// slow strumming is forgiving, fast strumming compounds in
        /// difficulty faster than NPS alone suggests.
        /// </summary>
        public double StrumNpsExponent { get; set; } = 2.2;

        /// <summary>
        /// Midpoint NPS for the strum curve power amplification. The shape
        /// of <see cref="StrumNpsExponent"/> crosses 1.0× multiplier at
        /// raw=midpoint — below midpoint the multiplier is &lt;1 (extra
        /// damping), above it is &gt;1 (amplification).
        /// </summary>
        public double StrumNpsMidpoint { get; set; } = 18.0;

        /// <summary>
        /// Low-end floor added to the strum curve, saturating: the added
        /// amount is floor·(1 − e^(−raw/floorK)). Approaches the full floor
        /// value past raw ≈ 3·floorK. This buffs the bottom of the curve so
        /// slow strumming sections register at ~1 instead of ~0, without
        /// stacking onto the already-amplified high-NPS values (it adds the
        /// same fixed amount everywhere past low NPS).
        /// </summary>
        public double StrumNpsFloor { get; set; } = 1.0;

        /// <summary>
        /// Time-constant for the strum floor saturation (units = raw curve
        /// value, not seconds). At raw=floorK the floor is at ~63% of its
        /// maximum; at raw=3·floorK it's at ~95%. Lower for a sharper ramp,
        /// higher for a gentler one.
        /// </summary>
        public double StrumNpsFloorK { get; set; } = 2.0;

        // ---- Fret complexity (chunk-based motion cost) ------------------------
        // FretComplexity walks the note stream and segments it into motion
        // chunks (Trill / RollOn / RollOff / Zig / Free). Each non-Free chunk
        // pays a single PatternOnsetCost on its first note plus a small
        // hold-strain on every subsequent note. ChordIntrinsic shape cost is
        // added on top of every note regardless of chunk membership.

        /// <summary>
        /// Maximum allowed inter-note delta (seconds) before a chunk is forced
        /// to close. Beyond this, even a "shape-coherent" sequence can't be
        /// counted as one wrist motion.
        /// </summary>
        public double ChunkMaxInterNoteSeconds { get; set; } = 0.35;

        /// <summary>
        /// Allowed deviation from the chunk's tempo baseline (the first
        /// inter-note delta). Subsequent deltas must sit within ±this fraction
        /// or the chunk closes. Default 0.25 = ±25%.
        /// </summary>
        public double ChunkDeltaTolerance { get; set; } = 0.25;

        /// <summary>Hard cap on chunk length in notes. Bounds the greedy classifier's work.</summary>
        public int ChunkMaxLength { get; set; } = 16;

        /// <summary>
        /// Per-note hold-strain billed on every chunk note after the first
        /// for Trill chunks. Compound trills (rotating multiple upper targets)
        /// scale this by <see cref="CompoundTrillBump"/> per extra distinct
        /// target.
        /// </summary>
        public double TrillHoldStrain { get; set; } = 0.2;

        /// <summary>
        /// Per-note hold-strain for RollOn / RollOff chunks. Rolls are
        /// slightly more taxing than trills because every note targets a new
        /// fret position.
        /// </summary>
        public double RollHoldStrain { get; set; } = 0.2;

        /// <summary>
        /// Per-note hold-strain for Zig chunks. Highest of the three because
        /// the wrist reverses direction inside the motion.
        /// </summary>
        public double ZigHoldStrain { get; set; } = 0.2;

        /// <summary>
        /// Span scaling factor for hold-strain, indexed by span = top fret −
        /// anchor + 1 (so RY = span 2, RB = span 3, GO = span 5). Indices 0
        /// and 1 are unreachable (a chunk needs ≥2 distinct fret positions).
        /// </summary>
        public double[] ShapeSpanCost { get; set; } = new double[] { 0.0, 0.0, 0.6, 0.8, 1.0, 1.2 };

        /// <summary>
        /// Multiplicative bump to TrillHoldStrain per extra distinct upper
        /// target beyond the first. Captures compound trills like
        /// R-Y-R-B-R-Y-R-B ramping in difficulty over a simple R-Y-R-Y trill.
        /// </summary>
        public double CompoundTrillBump { get; set; } = 0.20;

        /// <summary>
        /// Multiplicative bump applied when a Zig chunk's anchor sits in the
        /// chunk's interior (e.g. YRY — wrist dips through the anchor) rather
        /// than at an endpoint (e.g. RYR).
        /// </summary>
        public double ReverseZigBump { get; set; } = 1.15;

        /// <summary>
        /// Multiplicative bump per skipped fret position inside a roll or zig
        /// shape. RYO has 1 skip (Y → O jumps over B), RBO has 1 skip
        /// (R → B jumps over Y), RYBO has 0 skips. Trills don't price skips
        /// or span at all — they can be played two-handed, so the gap
        /// between the anchor and target doesn't translate into per-tap
        /// finger-motion cost.
        /// </summary>
        public double SkipPenalty { get; set; } = 0.15;

        /// <summary>
        /// Onset cost paid once on the first note of every non-Free chunk.
        /// This is the wrist-motion startup cost — beginning a pattern is the
        /// expensive part; sustaining one is cheap-per-note.
        /// </summary>
        public double PatternOnsetCost { get; set; } = 1.50;

        /// <summary>
        /// Small constant added to the curve weight at the start of every
        /// pattern chunk (Trill / RollOn / RollOff / Zig). Models the "extra
        /// cognitive bump" of switching into a new motion shape — distinct
        /// from <see cref="PatternOnsetCost"/>, which is a per-chunk
        /// hand-startup cost that's already absorbed into the chunk's summed
        /// weight. Free chunks don't pay this — they aren't transitions.
        /// </summary>
        public double TransitionBoost { get; set; } = 0.2;

        /// <summary>
        /// Multiplier applied to the max chord intrinsic when computing a
        /// Trill chunk's weight. Trills hold the lower-fret anchor and only
        /// alternate the upper-fret target, so the held chord shape doesn't
        /// translate into proportional difficulty the way it does for a
        /// single-hand walking Roll/Zig. A GRYB → O Trill should be only
        /// modestly harder than a G → R Trill — the rhythm/alternation cost
        /// dominates either way. Default 0.4 keeps fret-shape contribution
        /// noticeable but well below 1.0× for big-anchor 2-note Trills.
        /// Only applied to Trills; Roll/Zig still use full max chord
        /// intrinsic since those motions are walked finger-by-finger.
        /// </summary>
        public double TrillChordWeight { get; set; } = 0.4;

        /// <summary>
        /// Additive bonus added to a Trill chunk's weight when its anchor
        /// (the lowest fret in the trill) differs from the immediately
        /// previous Trill chunk's anchor. Trills are 2-note units in this
        /// chunker, so a sequence of trill pairs whose anchor shifts
        /// between pairs costs more than a trill stream that keeps the
        /// same anchor — the player has to re-anchor their fret hand
        /// each time the anchor moves. Only applied on Trill→Trill
        /// transitions; mode-change boosts (Strum↔Tap, etc.) and the
        /// general TransitionBoost handle other cases.
        /// </summary>
        public double TrillAnchorTransitionBoost { get; set; } = 0.5;

        /// <summary>
        /// Multiplier applied to a motion chunk's combined "shape complexity"
        /// term — i.e. (chord intrinsic + hold-strain × non-anchor count) —
        /// before adding the onset cost. Lowering this scales down the
        /// influence of both note count AND intrinsic chord cost on a
        /// chunk's weight without zeroing them out. The structural
        /// complexity of a motion (chord size, span, distinct targets) and
        /// its length still differentiate chunks, just with reduced overall
        /// magnitude. Onset is unaffected — every motion chunk still pays
        /// the wrist-startup cost.
        /// </summary>
        public double ShapeWeightScale { get; set; } = 0.6;

        /// <summary>
        /// Power exponent applied to the non-anchor count in motion-chunk
        /// weight: hold_strain × pow(nonAnchorCount, exponent). exponent=1
        /// reproduces the legacy linear scaling. exponent > 1 super-linearly
        /// rewards longer runs — buffs quints and longer rolls relative to
        /// triples without changing the cost of single-tap-target chunks
        /// (nonAnchorCount=1 → pow(1,p)=1 always).
        /// </summary>
        public double NonAnchorExponent { get; set; } = 1.2;

        /// <summary>
        /// Multiplier applied to a strum-typed Free chunk's chord intrinsic
        /// when computing its fret-curve weight. Captures the fact that the
        /// fret hand is fully committed to holding the chord throughout the
        /// strum (you can't free it up to do anything else), so a strum
        /// note's fret-side cost is the same as a tap/HOPO at the same
        /// chord. The simultaneous strum-curve contribution on the strum
        /// side then captures the picking work, and the Lp-norm composite
        /// naturally weights strums higher than taps overall because two
        /// axes register at once. Tap/HOPO Free chunks pay full intrinsic
        /// unconditionally and don't have a parallel strum-curve event,
        /// reflecting the one-hand-only difficulty.
        /// </summary>
        public double StrumFretWeight { get; set; } = 1.0;

        /// <summary>
        /// Extra boost added to a chunk's weight on a Tap↔Hopo type change
        /// (no Strum involved on either side). The cognitive switch between
        /// these two tap-driven mechanics is real but mild — both rely on
        /// fret-side action, just with different trigger semantics. Applied
        /// regardless of shape; stacks with <see cref="TransitionBoost"/>.
        /// </summary>
        public double TypeTransitionBoost { get; set; } = 0.3;

        /// <summary>
        /// Maximum boost added when a transition involves a Strum on either
        /// side — i.e. Strum↔Tap or Strum↔Hopo. Captures the
        /// second-hand-engagement cost of switching between picking-driven
        /// and tap-driven mechanics. The actual paid amount is scaled by
        /// the time gap between the two chunks via
        /// <see cref="StrumTransitionGapK"/>: a tight gap means the player
        /// has no time to bring the second hand up (or release it) and
        /// pays close to the full boost; a generous gap means there's
        /// time to switch hands deliberately and the boost decays toward
        /// zero. Both directions cost the same — entering a fast tap
        /// burst from strums and returning to strums afterward are
        /// equally disruptive at the same NPS.
        /// </summary>
        public double StrumModeTransitionBoost { get; set; } = 1.5;

        /// <summary>
        /// Multiplier applied to a chunk's curve weight when it's flagged
        /// InRepeat by the K-period chunk-pattern detector. Repeated chunks
        /// (e.g. the 2nd, 3rd, etc. trills in a sustained trill section, or
        /// the body of a repeated A-B-A-B compound) are mechanically easier
        /// than fresh-pattern chunks because the player's hand is already
        /// in the right shape and rhythm. The transition-boost system
        /// already pays a *bonus* on fresh chunks; this discount nerfs the
        /// continuation chunks on top of that, so a 30-note sustained
        /// trill is meaningfully easier than the same notes broken into
        /// short trill fragments separated by rests.
        /// </summary>
        public double RepeatDiscount { get; set; } = 0.7;

        /// <summary>
        /// Time-constant (seconds) for the strum-mode transition boost
        /// gap-decay. The boost actually paid is
        ///   StrumModeTransitionBoost · exp(−gap / StrumTransitionGapK)
        /// where gap is the time between the previous chunk's last note
        /// and the current chunk's first note. With K=0.15:
        ///   gap=0.05s (~20 NPS) → 72% of boost
        ///   gap=0.10s (~10 NPS) → 51% of boost
        ///   gap=0.30s (~3 NPS)  → 14% of boost
        ///   gap=0.50s           →  4% of boost
        /// So a strum verse leading into a fast tap chorus pays heavily,
        /// but a strum verse with a slow tap fill (where the player has
        /// time to deliberately bring the second hand up) pays little.
        /// </summary>
        public double StrumTransitionGapK { get; set; } = 0.15;

        /// <summary>
        /// Tempo-gap threshold (seconds) at which the K-period chunk-repeat
        /// detector resets its history. When two consecutive chunks are
        /// separated by a gap larger than this, the second chunk is treated
        /// as a fresh transition and pays <see cref="TransitionBoost"/>
        /// regardless of whether its signature matches a previous chunk.
        /// Models the cognitive reset cost of re-entering a motion after a
        /// pause — a chart that interleaves trill bursts with rests is
        /// materially harder than the same trill sustained continuously.
        /// </summary>
        public double TransitionGapSeconds { get; set; } = 0.5;

        /// <summary>
        /// Low-NPS rolloff constant for the fret curve. After the sliding
        /// sum, each raw value is passed through raw·(1 − e^(−raw/k)), which
        /// dampens low values (quadratic at the bottom) but converges to
        /// raw at high values (no cap). Models the idea that a few isolated
        /// fret events shouldn't register as much difficulty per-event as a
        /// dense stream — the player can absorb sparse motion with little
        /// strain, but sustained motion compounds. The curve hits ~63% of
        /// raw at value k, ~86% at 2k, ~95% at 3k, and is effectively
        /// linear past 3k. Lower k = sharper rolloff (more low-NPS
        /// suppression); higher k = gentler.
        /// </summary>
        public double FretNpsSaturationK { get; set; } = 6.0;

        /// <summary>
        /// Low-end floor added to the fret curve, saturating: the added
        /// amount is floor·(1 − e^(−raw/floorK)). Buffs the bottom of the
        /// curve so sparse fret motion registers a small positive
        /// contribution instead of near-zero, without stacking onto the
        /// high-NPS values.
        /// </summary>
        public double FretNpsFloor { get; set; } = 0.5;

        /// <summary>
        /// Time-constant for the fret floor saturation (units = raw curve
        /// value, not seconds). At raw=floorK the floor is at ~63% of its
        /// maximum.
        /// </summary>
        public double FretNpsFloorK { get; set; } = 2.0;

        /// <summary>
        /// Maximum multi-chunk period the chunk-pattern detector will check.
        /// (e.g. [A][B][A][B] = K=2; [A][B][C][D][A][B][C][D] = K=4) without
        /// producing false positives on long near-random runs. Buffer size
        /// scales as 2 × this value.
        /// </summary>
        public int ChunkPatternMaxLength { get; set; } = 12;

        // ---- Chord intrinsic difficulty (shape cost, no transition) -----------
        // The intrinsic cost of a chord shape is the sum of:
        //   * per-fret placement (lone vs run-extending),
        //   * per-position interior gap cost,
        //   * a barre cost when all 5 frets are held,
        //   * an outer-pair reduction when only the bottom + top frets are
        //     held with at least one gap between them.
        // FretComplexity calls this with <c>currChanged</c> (the newly-pressed
        // subset) so anchored frets contribute nothing to formation cost.

        /// <summary>
        /// Per-fret cost when the held fret has no held neighbor immediately
        /// below it — i.e., the fret is the bottom of a new "run" of consecutive
        /// held frets.
        /// </summary>
        public double ChordFretLone { get; set; } = 1.0;

        /// <summary>
        /// Per-fret cost when the held fret extends a consecutive run (its
        /// lower neighbor is also held). Lower than <see cref="ChordFretLone"/>
        /// because adjacent fingers form a natural compact hand position.
        /// </summary>
        public double ChordFretRun { get; set; } = 0.5;

        /// <summary>
        /// Per-position cost of an interior unheld fret (a "gap"). Indexed by
        /// fret position 0..4 (G, R, Y, B, O). Center gap (Y) is hardest to
        /// span; G and O are endpoints and cannot be gaps.
        /// </summary>
        public double[] ChordGapCost { get; set; } = new double[] { 0.0, 0.5, 1.0, 0.5, 0.0 };

        /// <summary>
        /// Flat addition when all 5 frets are held. The player has only 4
        /// fingers, so a 5-fret chord requires barring or thumb use on top of
        /// the per-fret math.
        /// </summary>
        public double ChordBarreCost { get; set; } = 1.0;

        /// <summary>
        /// Multiplier applied to the chord intrinsic for multi-fret chords
        /// (PopCount(frets) >= 2). Single-fret notes are unaffected.
        /// Bumps the per-note difficulty of chord patterns across the board
        /// — both tap and strum sections — to reflect that forming and
        /// hitting chord shapes is harder than the additive sum of the
        /// individual frets suggests.
        /// </summary>
        public double ChordIntrinsicScale { get; set; } = 1.5;

        /// <summary>
        /// Flat reduction when only the bottom and top frets are held with at
        /// least one gap between them (an "outer-pair" chord, e.g., GO, GB,
        /// RB). Such chords are pure stretch — no interior fingers are doing
        /// coordination work, only the hand opening matters.
        /// </summary>
        public double ChordOuterPairReduction { get; set; } = 0.5;

        // ---- NPS sampling ------------------------------------------------------
        public double SampleRateHz { get; set; } = 10.0;
        public double NpsWindowSeconds { get; set; } = 1.0;

        // ---- Percentile blend --------------------------------------------------
        public double PercentileMid { get; set; } = 0.83;
        public double PercentileHigh { get; set; } = 0.93;
        public double PercentilePeak { get; set; } = 0.99;
        public double TopFraction { get; set; } = 0.05;

        public double WeightHigh { get; set; } = 0.45;
        public double WeightMid { get; set; } = 0.25;
        public double WeightTop { get; set; } = 0.20;
        public double WeightPeak { get; set; } = 0.10;

        // ---- Star rating -------------------------------------------------------
        public double StarsExponent { get; set; } = 1.2;
        public double StarsDivisor { get; set; } = 5.0;

        public double LengthLogBase { get; set; } = 200.0;
        public double LengthBonusGain { get; set; } = 0.4;

        /// <summary>
        /// Multiplier on (l5 × length_factor) — rewards charts that combine a
        /// long note count with high-peak difficulty. Captures the "choke
        /// factor": missing one of N hard peaks scales linearly with N, so a
        /// long map that sustains the same peak intensity is materially
        /// harder to clear than a short one. Smaller than
        /// <see cref="SustainedLengthGain"/> because peaks are by definition
        /// rare — the bonus shouldn't dominate from a single hard burst.
        /// </summary>
        public double PeakLengthGain { get; set; } = 0.05;

        /// <summary>
        /// Multiplier on (p93 × length_factor) — heavily rewards charts that
        /// hold a high sustained difficulty (top ~7%) over a long note count.
        /// Sustained density is the hardest single property to fake; a 10-min
        /// chart at p93 ≈ 6 is dramatically harder than a 2-min chart at the
        /// same p93, and the bonus reflects that. Larger than
        /// <see cref="PeakLengthGain"/> because sustained intensity
        /// compounds over the chart's full duration.
        /// </summary>
        public double SustainedLengthGain { get; set; } = 0.15;

        // ---- Sustain complexity ------------------------------------------------
        /// <summary>
        /// Per-fret baseline sustain weight. Each held-fret sustain contributes
        /// this weight to every sample bin it covers. Disjointed sustains
        /// compound naturally because overlapping bins accumulate before
        /// saturation.
        /// </summary>
        public double SustainPerFretWeight { get; set; } = 0.5;

        /// <summary>Open-note sustain weight, applied to bins covered by an open sustain.</summary>
        public double SustainOpenWeight { get; set; } = 0.5;

        /// <summary>
        /// Saturation constant for density damping. As <c>load</c> grows past
        /// this value, the damped load tends toward <see cref="SustainMaxLoad"/>
        /// via tanh.
        /// </summary>
        public double SustainSaturationK { get; set; } = 4.0;

        /// <summary>Cap on damped sustain load per sample.</summary>
        public double SustainMaxLoad { get; set; } = 8.0;

        // ---- Composite ---------------------------------------------------------
        /// <summary>Lp-norm exponent used to combine the three Bars per sample. p=3 ≈ osu!-style aggregation.</summary>
        public double CompositeExponent { get; set; } = 3.0;

        // ---- Pattern detection -------------------------------------------------
        /// <summary>
        /// Maximum inter-note time (seconds) before a pattern run is broken.
        /// At 240 BPM 16ths this is ~62.5 ms; the default 0.35 s tolerates
        /// down to about 86 BPM 16ths or 172 BPM 8ths before splitting the run.
        /// </summary>
        public double PatternMaxInterNoteSeconds { get; set; } = 0.35;

        /// <summary>Minimum total note count to register a trill (default 4 = two full alternations).</summary>
        public int MinTrillNotes { get; set; } = 4;

        /// <summary>Minimum total note count to register a chimney (default 5).</summary>
        public int MinChimneyNotes { get; set; } = 5;

        /// <summary>
        /// Minimum total note count to register a zig (default 5). The minimum
        /// "back-and-forth" shape (e.g., G-R-Y-R-G) needs 5 notes to complete one
        /// out-and-back cycle across 3 distinct frets.
        /// </summary>
        public int MinZigNotes { get; set; } = 5;

        /// <summary>Minimum number of distinct frets required for a zig (default 3, which excludes trills).</summary>
        public int MinZigDistinctFrets { get; set; } = 3;
    }
}
