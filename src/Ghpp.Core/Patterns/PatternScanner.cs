using System.Collections.Generic;
using Ghpp.Core.Abstractions;
using Ghpp.Core.Abstractions.Patterns;
using Ghpp.Core.Models;
using Ghpp.Core.Patterns.Detectors;

namespace Ghpp.Core.Patterns
{
    /// <summary>
    /// Runs every registered <see cref="IPatternDetector"/> over the chart
    /// and returns the flattened list of <see cref="PatternMatch"/>es plus
    /// a <see cref="PatternIndex"/> for per-note lookup.
    /// </summary>
    public static class PatternScanner
    {
        private static readonly IPatternDetector[] Detectors =
        {
            new TrillDetector(),
            new MonotonicRunDetector(),
            new LadderDetector(),
            new ZigDetector(),
            new ChimneyDetector(),
        };

        public static PatternIndex Scan(IReadOnlyList<Note> notes, DifficultyOptions options)
        {
            var matches = new List<PatternMatch>();
            foreach (var detector in Detectors)
            {
                foreach (var match in detector.Scan(notes, options))
                {
                    matches.Add(match);
                }
            }
            return new PatternIndex(notes.Count, matches);
        }
    }
}
