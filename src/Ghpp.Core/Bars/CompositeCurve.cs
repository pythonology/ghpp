using System;
using Ghpp.Core.Aggregation;
using Ghpp.Core.Models;

namespace Ghpp.Core.Bars
{
    /// <summary>
    /// Combines the per-Bar curves (FretComplexity, StrumComplexity,
    /// SustainComplexity) into a single composite curve via an Lp-norm.
    /// With p=3 (default), simultaneously high values across multiple Bars
    /// dominate over a single high Bar, mirroring osu!'s aggregation. With
    /// p=1, the result is the additive sum used during development.
    /// </summary>
    internal static class CompositeCurve
    {
        public static DifficultyCurve LpNorm(
            DifficultyCurve fret,
            DifficultyCurve strum,
            DifficultyCurve sustain,
            DifficultyOptions options,
            double startTime)
        {
            // Sustain complexity is currently disabled in the composite; only
            // fret and strum complexity contribute to the star rating. The
            // sustain curve is still produced and surfaced in the report for
            // inspection.
            _ = sustain;
            var n = Math.Max(fret.Values.Length, strum.Values.Length);
            var result = new double[n];
            var p = options.CompositeExponent;
            var invP = 1.0 / p;

            for (var i = 0; i < n; i++)
            {
                var c = i < fret.Values.Length ? fret.Values[i] : 0.0;
                var s = i < strum.Values.Length ? strum.Values[i] : 0.0;
                var sum = Math.Pow(c, p) + Math.Pow(s, p);
                result[i] = sum > 0 ? Math.Pow(sum, invP) : 0.0;
            }

            return new DifficultyCurve(result, options.SampleRateHz, startTime);
        }
    }
}
