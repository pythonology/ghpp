using System.Collections.Generic;
using Ghpp.Core.Aggregation;
using Ghpp.Core.Patterns;

namespace Ghpp.Core.Models
{
    /// <summary>
    /// Result of a difficulty calculation: the star rating, the source curve,
    /// and the breakdown stats useful for debugging and visualization.
    /// </summary>
    public class DifficultyReport
    {
        // Headline result
        public DifficultyCurve Curve { get; set; }
        public double StarRating { get; set; }
        public double IntensityStars { get; set; }
        public double LengthBonus { get; set; }
        public double Blended { get; set; }

        // Note counts
        public int TotalNotes { get; set; }
        public int FretLaneNotes { get; set; }
        public int StrumCount { get; set; }
        public int HopoCount { get; set; }
        public int TapCount { get; set; }

        // Time / NPS curve stats
        public double DurationSeconds { get; set; }
        public double MeanNps { get; set; }
        public double MaxNps { get; set; }

        // Aggregator inputs
        public double P83 { get; set; }
        public double P93 { get; set; }
        public double P99 { get; set; }
        public double L5 { get; set; }

        // Per-Bar curves and means
        public DifficultyCurve FretComplexityCurve { get; set; }
        public double FretMean { get; set; }
        public DifficultyCurve StrumComplexityCurve { get; set; }
        public double StrumMean { get; set; }
        public DifficultyCurve SustainComplexityCurve { get; set; }
        public double SustainMean { get; set; }

        /// <summary>Per-note lookup of pattern memberships (trills, zigs, ladders, etc.).</summary>
        public PatternIndex Patterns { get; set; }
    }
}
