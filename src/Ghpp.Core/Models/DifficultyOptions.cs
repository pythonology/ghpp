namespace Ghpp.Core.Models
{
    /// <summary>
    /// Tunable constants for the difficulty pipeline. Defaults reproduce the
    /// Python prototype's calibration; expose these as sliders in the editor.
    /// </summary>
    public class DifficultyOptions
    {
        // Type weights
        public double StrumWeight { get; set; } = 1.3;
        public double HopoWeight { get; set; } = 1.0;
        public double TapWeight { get; set; } = 0.9;

        // SBar (rhythm-complexity signal). Each note's per-note contribution
        // is purely a rhythm-bonus computed from Xexxar island boundaries in
        // a small look-back window. NPS emerges implicitly through the
        // sliding-window sum: a fast section with varied rhythm sums many
        // bonuses together; a fast section with uniform rhythm sums zeros.
        // All notes (strums, HOPOs, taps) participate equally — HOPOs/Taps
        // can still be strummed, they just don't *have* to be.
        public int RhythmHistoryNotes { get; set; } = 8;
        public double RhythmHistorySeconds { get; set; } = 2.0;

        /// <summary>
        /// Tolerance for treating two consecutive deltas as the same rhythm
        /// class. Default 0.20 (±20%) accounts for rhythm-perception leniency:
        /// a triplet vs a slightly swung triplet feels the same to the player.
        /// </summary>
        public double RhythmDeltaTolerance { get; set; } = 0.20;

        /// <summary>
        /// Per-strum baseline contribution. Uniform fast strumming still has
        /// some difficulty (the picking arm has to sustain rate); this floor
        /// keeps SBar non-zero through static-rhythm sections. Default 0.15.
        /// </summary>
        public double SBarBaselineContribution { get; set; } = 0.15;

        /// <summary>Per-note bonus cap (0..1). The contribution scales between baseline and this cap.</summary>
        public double SBarMaxBonus { get; set; } = 1.0;

        /// <summary>
        /// Saturation constant for the bonus curve: bonus = max·(1 − e^(−boundaries/k)).
        /// Lower k = bonus saturates faster as boundaries accumulate. Default 2.0
        /// makes "vastly varying" passages (triple→quad→double) reach near-cap quickly.
        /// </summary>
        public double SBarSaturationK { get; set; } = 2.0;

        /// <summary>NPS at which the rake-strum dampener begins on detected uniform streams (Quad+).</summary>
        public double SBarStreamDampenStartNps { get; set; } = 14.0;

        /// <summary>Maximum fraction the rake-strum dampener can subtract.</summary>
        public double SBarStreamDampenMax { get; set; } = 0.18;

        /// <summary>Slope of the rake-strum dampener ramp per NPS above the start threshold.</summary>
        public double SBarStreamDampenSlope { get; set; } = 0.03;

        // CBar (chord/transition cost)
        /// <summary>
        /// Hand-shift cost indexed by fret distance (0..4). Distance 0 = same
        /// fret = no motion; distance 4 = G to O = full hand shift. CBar uses
        /// this as the baseline transition cost before anchor and finger-cross
        /// adjustments.
        /// </summary>
        public double[] FretJumpCost { get; set; } = new double[] { 0.00, 0.10, 0.25, 0.50, 0.80 };

        /// <summary>
        /// Maximum multiplicative discount when chords are fully overlapping
        /// (one is a subset of the other). The actual discount is scaled by
        /// the shared-fret ratio: <c>discount = AnchorDiscount × (shared / max(prev,curr))</c>.
        /// Default 0.40 with full overlap = 40% off; partial overlap gets less.
        /// </summary>
        public double CBarAnchorDiscount { get; set; } = 0.40;

        /// <summary>
        /// Per-extra-finger-toggle cost for chord transitions. A simple
        /// single-fret change always involves 2 finger toggles (one off, one
        /// on); this constant adds cost for each toggle beyond that. Default
        /// 0.12 means GRY→YBO (4 toggles, 2 extra) adds 0.24 on top of the
        /// hand-shift cost.
        /// </summary>
        public double CBarPerExtraToggleCost { get; set; } = 0.12;

        /// <summary>Additional discount when a HOPO/Tap chord shares an anchor finger. Default 0.10.</summary>
        public double CBarHopoChordAnchorBonus { get; set; } = 0.10;

        /// <summary>Additive penalty when the topmost-fret motion direction reverses relative to the prior step. Default 0.15.</summary>
        public double CBarFingerCrossPenalty { get; set; } = 0.15;

        /// <summary>NPS at which the trill rake-tap dampener begins to apply. Default 12.0.</summary>
        public double CBarTrillDampenStartNps { get; set; } = 12.0;

        /// <summary>Maximum fraction the trill dampener can subtract. Default 0.20 = 20% reduction at extreme NPS.</summary>
        public double CBarTrillDampenMax { get; set; } = 0.20;

        /// <summary>Slope of the trill dampener ramp per NPS above the start threshold. Default 0.04.</summary>
        public double CBarTrillDampenSlope { get; set; } = 0.04;

        // NPS sampling
        public double SampleRateHz { get; set; } = 10.0;
        public double NpsWindowSeconds { get; set; } = 1.0;

        // Percentile blend
        public double PercentileMid { get; set; } = 0.83;
        public double PercentileHigh { get; set; } = 0.93;
        public double PercentilePeak { get; set; } = 0.99;
        public double TopFraction { get; set; } = 0.05;

        public double WeightHigh { get; set; } = 0.45;
        public double WeightMid { get; set; } = 0.25;
        public double WeightTop { get; set; } = 0.20;
        public double WeightPeak { get; set; } = 0.10;

        // Star rating
        public double StarsExponent { get; set; } = 1.7;
        public double StarsDivisor { get; set; } = 17.0;

        public double LengthLogBase { get; set; } = 200.0;
        public double LengthBonusGain { get; set; } = 1.0;

        // LBar (sustain difficulty)
        /// <summary>Per-note baseline sustain weight, applied to active sustain seconds. Default 0.5.</summary>
        public double LBarSustainWeight { get; set; } = 0.5;

        /// <summary>
        /// Saturation constant for density damping. As <c>load</c> grows past
        /// this value, the damped load tends toward <see cref="LBarMaxLoad"/>
        /// via tanh. Default 4.0 reaches ~76% saturation at load = 4.
        /// </summary>
        public double LBarSaturationK { get; set; } = 4.0;

        /// <summary>Cap on damped sustain load per sample. Default 8.0.</summary>
        public double LBarMaxLoad { get; set; } = 8.0;

        // Composite (Lp-norm aggregation of CBar/SBar/LBar)
        /// <summary>Lp-norm exponent used to combine the three Bars per sample. p=3 ≈ osu!-style aggregation.</summary>
        public double CompositeExponent { get; set; } = 3.0;

        // Pattern detection
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
