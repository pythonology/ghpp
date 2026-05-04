using System.Collections.Generic;
using Ghpp.Core.Abstractions;
using Ghpp.Core.Abstractions.Patterns;
using Ghpp.Core.Models;

namespace Ghpp.Core.Patterns.Detectors
{
    /// <summary>
    /// Detects ladders: pairs of same-fret notes that step by one fret per
    /// pair, ascending or descending. E.g., G-G-R-R-Y-Y reads as three
    /// "stairs" ascending. Minimum length is 4 notes (two stairs).
    /// </summary>
    internal sealed class LadderDetector : IPatternDetector
    {
        public IEnumerable<PatternMatch> Scan(IReadOnlyList<Note> notes, DifficultyOptions options)
        {
            var maxGap = options.PatternMaxInterNoteSeconds;
            var i = 0;
            while (i + 3 < notes.Count)
            {
                var f0 = PatternHelpers.SingleFretIndex(notes[i]);
                var f1 = PatternHelpers.SingleFretIndex(notes[i + 1]);
                var f2 = PatternHelpers.SingleFretIndex(notes[i + 2]);
                var f3 = PatternHelpers.SingleFretIndex(notes[i + 3]);
                if (f0 < 0 || f1 != f0 || f2 < 0 || f3 != f2 ||
                    PatternHelpers.GapBreaks(notes[i], notes[i + 1], maxGap) ||
                    PatternHelpers.GapBreaks(notes[i + 1], notes[i + 2], maxGap) ||
                    PatternHelpers.GapBreaks(notes[i + 2], notes[i + 3], maxGap))
                {
                    i++;
                    continue;
                }

                var step = f2 - f0;
                if (step != 1 && step != -1)
                {
                    i++;
                    continue;
                }

                // Extend pair-by-pair.
                var end = i + 3;
                var lastPairFret = f2;
                while (end + 2 < notes.Count)
                {
                    var pairFret = PatternHelpers.SingleFretIndex(notes[end + 1]);
                    var pairTail = PatternHelpers.SingleFretIndex(notes[end + 2]);
                    if (pairFret < 0 || pairTail != pairFret || pairFret - lastPairFret != step ||
                        PatternHelpers.GapBreaks(notes[end], notes[end + 1], maxGap) ||
                        PatternHelpers.GapBreaks(notes[end + 1], notes[end + 2], maxGap))
                    {
                        break;
                    }
                    end += 2;
                    lastPairFret = pairFret;
                }

                var direction = step > 0 ? PatternDirection.Ascending : PatternDirection.Descending;
                var low = (byte)(step > 0 ? f0 : lastPairFret);
                var high = (byte)(step > 0 ? lastPairFret : f0);
                yield return new PatternMatch(
                    i, end, PatternKind.Ladder, direction,
                    PatternHelpers.AverageNps(notes, i, end), low, high);
                i = end + 1;
            }
        }
    }
}
