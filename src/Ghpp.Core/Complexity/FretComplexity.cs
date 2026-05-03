using System;
using System.Collections.Generic;
using Ghpp.Core.Models;

namespace Ghpp.Core.Complexity
{
    /// <summary>
    /// Assigns a fret-shift multiplier to each note based on the distance
    /// between this note's hand-anchor fret and the previous note's. Applies
    /// to all note types: a HOPO from G to O is just as much of a hand shift
    /// as a strum from G to O.
    /// </summary>
    /// <remarks>
    /// Hand position is the topmost fret pressed (a GR chord anchors at Red,
    /// a YBO chord anchors at Orange). Open notes contribute no cost on either
    /// side; the multiplier across an open note is always 1.0 because there is
    /// no fretting hand position to measure a shift from.
    /// </remarks>
    internal static class FretComplexity
    {
        public static IReadOnlyDictionary<int, int> Apply(IList<AnnotatedNote> notes, DifficultyOptions options)
        {
            var distribution = new Dictionary<int, int>();

            for (var i = 1; i < notes.Count; i++)
            {
                var current = notes[i];
                var previous = notes[i - 1];

                if (current.Source.IsOpen || current.FretPosition < 0)
                {
                    continue;
                }
                if (previous.Source.IsOpen || previous.FretPosition < 0)
                {
                    continue;
                }

                var distance = Math.Abs(current.FretPosition - previous.FretPosition);

                if (distribution.TryGetValue(distance, out var existing))
                {
                    distribution[distance] = existing + 1;
                }
                else
                {
                    distribution[distance] = 1;
                }

                var cost = LookupJumpCost(distance, options.FretJumpCost);
                current.FretMultiplier = 1.0 + cost;
            }

            return distribution;
        }

        private static double LookupJumpCost(int distance, double[] table)
        {
            if (distance < 0 || table == null || distance >= table.Length)
            {
                return 0.0;
            }
            return table[distance];
        }
    }
}
