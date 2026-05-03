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

        // Rhythm complexity
        public int RhythmHistoryNotes { get; set; } = 8;
        public double RhythmHistorySeconds { get; set; } = 2.0;
        public double RhythmDeltaTolerance { get; set; } = 0.12;
        public double RhythmBonusMax { get; set; } = 0.5;
        public double RhythmSaturationK { get; set; } = 2.5;

        // Fret complexity
        /// <summary>
        /// Hand-shift cost indexed by fret distance (0..4). Distance 0 = same
        /// fret = no motion; distance 4 = G to O = full hand shift.
        /// </summary>
        public double[] FretJumpCost { get; set; } = new double[] { 0.00, 0.10, 0.25, 0.50, 0.80 };

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
    }
}
