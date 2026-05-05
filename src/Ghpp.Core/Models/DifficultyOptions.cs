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
        public double TapWeight { get; set; } = 0.9;

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

        /// <summary>Per-strum baseline contribution. Floor that keeps StrumComplexity non-zero through static-rhythm sections.</summary>
        public double StrumBaselineContribution { get; set; } = 0.15;

        /// <summary>
        /// Per-note bonus cap. The contribution scales between baseline and
        /// this cap as rhythm variety increases. Default 0.85 leaves headroom
        /// over the typical ~0.6 saturation point for genuinely chaotic
        /// rhythms while keeping uniform strumming firmly at baseline.
        /// </summary>
        public double StrumMaxBonus { get; set; } = 0.85;

        /// <summary>
        /// Saturation constant for the bonus curve: bonus = max·(1 − e^(−boundaries/k)).
        /// Higher k = bonus accumulates more slowly so genuinely complex
        /// passages differentiate from mildly-varied ones. Default 5.0 spreads
        /// the response across the full 0..7 boundaries-in-window range
        /// (1 boundary ≈ 18% of max bonus, 7 ≈ 75%).
        /// </summary>
        public double StrumSaturationK { get; set; } = 5.0;

        // The following stream-dampener constants are reserved for future
        // re-introduction once anchors/pivots are calibrated. They are not
        // applied by the current StrumComplexity implementation.

        /// <summary>NPS at which the rake-strum dampener begins. Currently unused; reserved for future re-introduction.</summary>
        public double StrumStreamDampenStartNps { get; set; } = 14.0;

        /// <summary>Maximum fraction the rake-strum dampener can subtract. Currently unused; reserved.</summary>
        public double StrumStreamDampenMax { get; set; } = 0.18;

        /// <summary>Slope of the rake-strum dampener ramp per NPS above the start threshold. Currently unused; reserved.</summary>
        public double StrumStreamDampenSlope { get; set; } = 0.03;

        // ---- Fret complexity (chord/transition cost) ---------------------------
        /// <summary>
        /// Hand-shift cost indexed by fret distance (0..4). Distance 0 = same
        /// fret = no motion; distance 4 = G to O = full hand shift.
        /// </summary>
        public double[] FretJumpCost { get; set; } = new double[] { 0.00, 0.10, 0.25, 0.50, 0.80 };

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
        /// Flat reduction when only the bottom and top frets are held with at
        /// least one gap between them (an "outer-pair" chord, e.g., GO, GB,
        /// RB). Such chords are pure stretch — no interior fingers are doing
        /// coordination work, only the hand opening matters.
        /// </summary>
        public double ChordOuterPairReduction { get; set; } = 0.5;

        /// <summary>
        /// Multiplicative discount applied to action + hand-shift costs
        /// whenever a transition has at least one shared (anchored or
        /// pivoted) finger between prev and curr effective frets. The shared
        /// finger lets the player ignore some of the movement work. Applies
        /// uniformly across Strum/HOPO/Tap.
        /// </summary>
        public double FretPivotDiscount { get; set; } = 0.7;

        /// <summary>
        /// Per-finger action cost. Every press and every release counts as
        /// one action, weighted equally. Anchored frets (held in both prev
        /// and curr effective sets) are not actions.
        /// </summary>
        public double ChordPerFingerActionCost { get; set; } = 0.25;

        // The following dampener / penalty constants are reserved for future
        // re-introduction. They are not applied by the current FretComplexity
        // implementation.

        /// <summary>Additional discount when a HOPO/Tap chord shares an anchor finger. Currently unused; reserved.</summary>
        public double FretHopoChordAnchorBonus { get; set; } = 0.10;

        /// <summary>Additive penalty when the topmost-fret motion direction reverses. Currently unused; reserved.</summary>
        public double FretFingerCrossPenalty { get; set; } = 0.15;

        /// <summary>NPS at which the trill rake-tap dampener begins to apply. Currently unused; reserved.</summary>
        public double FretTrillDampenStartNps { get; set; } = 12.0;

        /// <summary>Maximum fraction the trill dampener can subtract. Currently unused; reserved.</summary>
        public double FretTrillDampenMax { get; set; } = 0.20;

        /// <summary>Slope of the trill dampener ramp per NPS above the start threshold. Currently unused; reserved.</summary>
        public double FretTrillDampenSlope { get; set; } = 0.04;

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
        public double StarsDivisor { get; set; } = 2.7;

        public double LengthLogBase { get; set; } = 200.0;
        public double LengthBonusGain { get; set; } = 1.0;

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
