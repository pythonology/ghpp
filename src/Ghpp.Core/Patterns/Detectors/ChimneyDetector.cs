using System.Collections.Generic;
using Ghpp.Core.Abstractions;
using Ghpp.Core.Abstractions.Patterns;
using Ghpp.Core.Models;

namespace Ghpp.Core.Patterns.Detectors
{
    /// <summary>
    /// Detects chimneys and reverse chimneys: zig-shaped runs whose extreme
    /// (highest for chimney, lowest for reverse chimney) fret only appears at
    /// an endpoint of the run, not in the middle. The continuity rules are
    /// the same as <see cref="ZigDetector"/>; chimneys may overlap with zigs
    /// in the index. The match span tracks the longest contiguous run that
    /// satisfies the apex-or-trough endpoint constraint.
    /// </summary>
    internal sealed class ChimneyDetector : IPatternDetector
    {
        public IEnumerable<PatternMatch> Scan(IReadOnlyList<Note> notes, DifficultyOptions options)
        {
            var maxGap = options.PatternMaxInterNoteSeconds;
            var minLength = options.MinChimneyNotes;

            var i = 0;
            while (i < notes.Count)
            {
                // Walk a single-fret run with no gap break.
                var startFret = PatternHelpers.SingleFretIndex(notes[i]);
                if (startFret < 0) { i++; continue; }
                var end = i;
                while (end + 1 < notes.Count
                    && PatternHelpers.SingleFretIndex(notes[end + 1]) >= 0
                    && !PatternHelpers.GapBreaks(notes[end], notes[end + 1], maxGap))
                {
                    end++;
                }

                if (end - i + 1 >= minLength)
                {
                    EmitIfChimney(notes, i, end, out PatternMatch? chimney);
                    if (chimney.HasValue) yield return chimney.Value;

                    EmitIfReverseChimney(notes, i, end, out PatternMatch? reverse);
                    if (reverse.HasValue) yield return reverse.Value;
                }
                i = end + 1;
            }
        }

        private static void EmitIfChimney(IReadOnlyList<Note> notes, int start, int end, out PatternMatch? match)
        {
            match = null;
            byte high = 0, low = byte.MaxValue;
            var highCount = 0;
            var highIndex = -1;
            for (var k = start; k <= end; k++)
            {
                var f = PatternHelpers.SingleFretIndex(notes[k]);
                if (f < 0) return;
                if (f > high) { high = (byte)f; highCount = 1; highIndex = k; }
                else if (f == high) { highCount++; }
                if (f < low) low = (byte)f;
            }
            if (high == low) return; // flat run, not a chimney
            if (highCount != 1) return;
            if (highIndex != start && highIndex != end) return;

            match = new PatternMatch(
                start, end, PatternKind.Chimney, PatternDirection.None,
                PatternHelpers.AverageNps(notes, start, end), low, high);
        }

        private static void EmitIfReverseChimney(IReadOnlyList<Note> notes, int start, int end, out PatternMatch? match)
        {
            match = null;
            byte high = 0, low = byte.MaxValue;
            var lowCount = 0;
            var lowIndex = -1;
            for (var k = start; k <= end; k++)
            {
                var f = PatternHelpers.SingleFretIndex(notes[k]);
                if (f < 0) return;
                if (f < low) { low = (byte)f; lowCount = 1; lowIndex = k; }
                else if (f == low) { lowCount++; }
                if (f > high) high = (byte)f;
            }
            if (high == low) return;
            if (lowCount != 1) return;
            if (lowIndex != start && lowIndex != end) return;

            match = new PatternMatch(
                start, end, PatternKind.ReverseChimney, PatternDirection.None,
                PatternHelpers.AverageNps(notes, start, end), low, high);
        }
    }
}
