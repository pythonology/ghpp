using System.Collections.Generic;
using Ghpp.Core.Abstractions;
using Ghpp.Core.Abstractions.Patterns;
using Ghpp.Core.Models;

namespace Ghpp.Core.Patterns.Detectors
{
    /// <summary>
    /// Detects zigs: contiguous runs of single-fret notes that loop back and
    /// forth across at least three distinct frets. The minimum shape is
    /// G-R-Y-R-G (5 notes, 3 distinct frets, one direction change). A trill
    /// (e.g., G-R-G-R) doesn't qualify because it only uses 2 distinct frets;
    /// a triplet/quad/quint doesn't qualify because it has no direction change.
    /// </summary>
    /// <remarks>
    /// Implementation walks each maximal single-fret run (broken by chords,
    /// open notes, or gap breaks), then validates against
    /// <see cref="DifficultyOptions.MinZigNotes"/>,
    /// <see cref="DifficultyOptions.MinZigDistinctFrets"/>, and the
    /// requirement of at least one direction change. The whole qualifying run
    /// is emitted as a single match. Overlap with other detectors (Quad,
    /// Triplet, etc.) is intentional — they tag the same notes for different
    /// reasons.
    /// </remarks>
    internal sealed class ZigDetector : IPatternDetector
    {
        public IEnumerable<PatternMatch> Scan(IReadOnlyList<Note> notes, DifficultyOptions options)
        {
            var maxGap = options.PatternMaxInterNoteSeconds;
            var i = 0;
            while (i < notes.Count)
            {
                var startFret = PatternHelpers.SingleFretIndex(notes[i]);
                if (startFret < 0) { i++; continue; }

                // Extend the run as long as notes are single-fret and gaps stay tight.
                var end = i;
                while (end + 1 < notes.Count)
                {
                    if (PatternHelpers.SingleFretIndex(notes[end + 1]) < 0) break;
                    if (PatternHelpers.GapBreaks(notes[end], notes[end + 1], maxGap)) break;
                    end++;
                }

                if (TryMakeZigMatch(notes, i, end, options, out var match))
                {
                    yield return match;
                }
                i = end + 1;
            }
        }

        private static bool TryMakeZigMatch(
            IReadOnlyList<Note> notes, int startIndex, int endIndex,
            DifficultyOptions options, out PatternMatch match)
        {
            match = default;
            var length = endIndex - startIndex + 1;
            if (length < options.MinZigNotes) return false;

            byte low = byte.MaxValue, high = 0;
            int distinctMask = 0;
            int prevFret = -1;
            int prevDir = 0;
            int directionChanges = 0;
            int firstFret = -1;
            bool pastFirstReversal = false;
            bool completedCycle = false;

            for (var k = startIndex; k <= endIndex; k++)
            {
                var f = PatternHelpers.SingleFretIndex(notes[k]);
                if (f < 0) return false;
                if (firstFret < 0) firstFret = f;
                distinctMask |= 1 << f;
                if (f < low) low = (byte)f;
                if (f > high) high = (byte)f;
                if (prevFret >= 0)
                {
                    var step = f - prevFret;
                    var dir = step > 0 ? 1 : (step < 0 ? -1 : 0);
                    if (dir != 0 && prevDir != 0 && dir != prevDir)
                    {
                        directionChanges++;
                        pastFirstReversal = true;
                    }
                    if (dir != 0) prevDir = dir;
                }
                if (pastFirstReversal && f == firstFret) completedCycle = true;
                prevFret = f;
            }

            if (directionChanges < 1) return false;
            if (!completedCycle) return false;

            var distinct = PopCount(distinctMask);
            if (distinct < options.MinZigDistinctFrets) return false;

            match = new PatternMatch(
                startIndex, endIndex, PatternKind.Zig, PatternDirection.None,
                PatternHelpers.AverageNps(notes, startIndex, endIndex), low, high);
            return true;
        }

        private static int PopCount(int x)
        {
            var n = 0;
            while (x != 0) { n += x & 1; x >>= 1; }
            return n;
        }
    }
}
