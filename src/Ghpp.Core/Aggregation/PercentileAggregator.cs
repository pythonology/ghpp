using System;
using Ghpp.Core.Models;

namespace Ghpp.Core.Aggregation
{
    /// <summary>
    /// Aggregate the difficulty curve into a single star rating via a 4-term percentile blend,
    /// then apply a power curve and additive length bonus.
    /// </summary>
    /// <remarks>
    /// The four blend terms each capture a different facet of difficulty.
    /// P93 captures sustained high intensity; P83 captures the bulk of the
    /// hard sections; L5 (mean of top 5%) captures the absolute peaks while
    /// averaging out single-sample spikes; P99 explicitly catches charts
    /// with truly extreme bursts that consistent metrics would miss.
    /// </remarks>
    internal static class PercentileAggregator
    {
        public sealed class Result
        {
            public double P83 { get; set; }
            public double P93 { get; set; }
            public double P99 { get; set; }
            public double L5 { get; set; }
            public double Blended { get; set; }
            public double IntensityStars { get; set; }
            public double LengthBonus { get; set; }
            // Diagnostic breakdown of LengthBonus into its three components.
            public double LengthBonusBase { get; set; }
            public double LengthBonusPeak { get; set; }
            public double LengthBonusSustained { get; set; }
            public double StarRating { get; set; }
        }

        public static Result Aggregate(double[] curveValues, int fretLaneNotes, DifficultyOptions options)
        {
            var sorted = SortCopy(curveValues);

            var p83 = Percentile(sorted, options.PercentileMid);
            var p93 = Percentile(sorted, options.PercentileHigh);
            var p99 = Percentile(sorted, options.PercentilePeak);
            var l5 = MeanTopFraction(sorted, options.TopFraction);

            var blended =
                options.WeightHigh * p93 +
                options.WeightMid * p83 +
                options.WeightTop * l5 +
                options.WeightPeak * p99;

            var intensityStars = blended > 0
                ? Math.Pow(blended, options.StarsExponent) / options.StarsDivisor
                : 0.0;

            // Length factor: shared log of note count, used by all three
            // length-bonus components. Zero notes → zero bonus.
            var lengthFactor = fretLaneNotes > 0
                ? Math.Log10(1.0 + fretLaneNotes / options.LengthLogBase)
                : 0.0;

            // Three-component length bonus:
            //   Base       — pure note-count signal (chart length alone).
            //   Peak       — l5 × length: rewards holding high peaks across
            //                a long chart. Models choke factor: one missed
            //                peak in N is worse when N is large.
            //   Sustained  — p93 × length: heavily rewards sustained high
            //                density across long charts. p93 is the hardest
            //                facet to fake — only charts that genuinely stay
            //                hard for a long time get this bonus.
            var lengthBonusBase      = options.LengthBonusGain     * lengthFactor;
            var lengthBonusPeak      = options.PeakLengthGain      * l5  * lengthFactor;
            var lengthBonusSustained = options.SustainedLengthGain * p93 * lengthFactor;
            var lengthBonus = lengthBonusBase + lengthBonusPeak + lengthBonusSustained;

            return new Result
            {
                P83 = p83,
                P93 = p93,
                P99 = p99,
                L5 = l5,
                Blended = blended,
                IntensityStars = intensityStars,
                LengthBonus = lengthBonus,
                LengthBonusBase = lengthBonusBase,
                LengthBonusPeak = lengthBonusPeak,
                LengthBonusSustained = lengthBonusSustained,
                StarRating = intensityStars + lengthBonus,
            };
        }

        /// <summary>
        /// Linear-interpolated percentile. <paramref name="p"/> in [0,1].
        /// Returns 0 for empty input.
        /// </summary>
        public static double Percentile(double[] sortedValues, double p)
        {
            switch (sortedValues.Length)
            {
                case 0:
                    return 0.0;
                case 1:
                    return sortedValues[0];
            }

            var index = p * (sortedValues.Length - 1);
            var lo = (int)Math.Floor(index);
            var hi = (int)Math.Ceiling(index);
            if (lo == hi)
            {
                return sortedValues[lo];
            }
            var fraction = index - lo;
            return sortedValues[lo] * (1 - fraction) + sortedValues[hi] * fraction;
        }

        /// <summary>Mean of the top <paramref name="fraction"/> of an ascending-sorted array.</summary>
        public static double MeanTopFraction(double[] sortedValues, double fraction)
        {
            if (sortedValues.Length == 0)
            {
                return 0.0;
            }
            var n = sortedValues.Length;
            var k = Math.Max(1, (int)Math.Ceiling(n * fraction));
            var sum = 0.0;
            for (var i = n - k; i < n; i++)
            {
                sum += sortedValues[i];
            }
            return sum / k;
        }

        private static double[] SortCopy(double[] values)
        {
            var copy = new double[values.Length];
            Array.Copy(values, copy, values.Length);
            Array.Sort(copy);
            return copy;
        }
    }
}
