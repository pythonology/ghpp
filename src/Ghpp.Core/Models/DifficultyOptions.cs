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

        /// <summary>Per-note bonus cap (0..1).</summary>
        public double StrumMaxBonus { get; set; } = 1.0;

        /// <summary>Saturation constant for the bonus curve: bonus = max·(1 − e^(−boundaries/k)).</summary>
        public double StrumSaturationK { get; set; } = 2.0;

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

        /// <summary>
        /// Per-held-fret intrinsic cost contribution (frets-and-gaps model).
        /// A chord's intrinsic difficulty is <c>PerHeld × popcount(F) + PerGap × gapCount(F)</c>.
        /// </summary>
        public double FretIntrinsicPerHeld { get; set; } = 0.05;

        /// <summary>
        /// Per-gap intrinsic cost contribution. A "gap" is a fret index between
        /// the lowest and highest held that is itself not held (e.g., GO has
        /// 3 gaps: R, Y, B). Higher than <see cref="FretIntrinsicPerHeld"/>
        /// because gaps stretch the hand more than additional adjacent frets.
        /// </summary>
        public double FretIntrinsicPerGap { get; set; } = 0.10;

        /// <summary>
        /// Multiplicative discount applied to hand-shift + chord-toggle costs
        /// when a chord-to-chord transition has at least one shared finger
        /// (a pivot). Default 0.5 = 50% discount when a pivot is available.
        /// HOPO/Tap notes never pivot (anchors only).
        /// </summary>
        public double FretPivotDiscount { get; set; } = 0.5;

        /// <summary>
        /// Per-extra-finger-toggle cost for chord transitions. A simple
        /// single-fret change always involves 2 finger toggles (one off, one
        /// on); this constant adds cost for each toggle beyond that.
        /// </summary>
        public double FretPerExtraToggleCost { get; set; } = 0.12;

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
