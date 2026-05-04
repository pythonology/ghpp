using System;
using Ghpp.Core.Aggregation;
using Ghpp.Core.Models;

namespace Ghpp.Core.Bars
{
    /// <summary>
    /// Combines the per-Bar curves (CBar, SBar, LBar) into a single composite
    /// curve via an Lp-norm. With p=3 (default), simultaneously high values
    /// across multiple Bars dominate over a single high Bar, mirroring osu!'s
    /// aggregation. With p=1, the result is the additive sum used during
    /// development.
    /// </summary>
    internal static class CompositeCurve
    {
        public static DifficultyCurve LpNorm(
            DifficultyCurve cbar,
            DifficultyCurve sbar,
            DifficultyCurve lbar,
            DifficultyOptions options,
            double startTime)
        {
            var n = Math.Max(cbar.Values.Length, Math.Max(sbar.Values.Length, lbar.Values.Length));
            var result = new double[n];
            var p = options.CompositeExponent;
            var invP = 1.0 / p;

            for (var i = 0; i < n; i++)
            {
                var c = i < cbar.Values.Length ? cbar.Values[i] : 0.0;
                var s = i < sbar.Values.Length ? sbar.Values[i] : 0.0;
                var l = i < lbar.Values.Length ? lbar.Values[i] : 0.0;
                var sum = Math.Pow(c, p) + Math.Pow(s, p) + Math.Pow(l, p);
                result[i] = sum > 0 ? Math.Pow(sum, invP) : 0.0;
            }

            return new DifficultyCurve(result, options.SampleRateHz, startTime);
        }
    }
}
