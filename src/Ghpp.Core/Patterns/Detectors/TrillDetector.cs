using System.Collections.Generic;
using Ghpp.Core.Abstractions;
using Ghpp.Core.Abstractions.Patterns;
using Ghpp.Core.Models;

namespace Ghpp.Core.Patterns.Detectors
{
    /// <summary>
    /// Detects trills: a repeating two-note alternation between exactly two
    /// distinct frets, e.g., R-Y-R-Y-R-Y. Requires at least
    /// <see cref="DifficultyOptions.MinTrillNotes"/> consecutive single-fret
    /// notes whose frets alternate between exactly two values, with each
    /// inter-note gap inside the continuity window.
    /// </summary>
    internal sealed class TrillDetector : IPatternDetector
    {
        public IEnumerable<PatternMatch> Scan(IReadOnlyList<Note> notes, DifficultyOptions options)
        {
            var minLength = options.MinTrillNotes;
            var i = 0;
            while (i < notes.Count - 1)
            {
                var fretA = PatternHelpers.SingleFretIndex(notes[i]);
                var fretB = PatternHelpers.SingleFretIndex(notes[i + 1]);
                if (fretA < 0 || fretB < 0 || fretA == fretB ||
                    PatternHelpers.GapBreaks(notes[i], notes[i + 1], options.PatternMaxInterNoteSeconds))
                {
                    i++;
                    continue;
                }

                // Walk forward as long as the alternation A,B,A,B... continues.
                var end = i + 1;
                while (end + 1 < notes.Count)
                {
                    var nextFret = PatternHelpers.SingleFretIndex(notes[end + 1]);
                    var expected = ((end + 1 - i) % 2 == 0) ? fretA : fretB;
                    if (nextFret != expected ||
                        PatternHelpers.GapBreaks(notes[end], notes[end + 1], options.PatternMaxInterNoteSeconds))
                    {
                        break;
                    }
                    end++;
                }

                var length = end - i + 1;
                if (length >= minLength)
                {
                    var low = (byte)(fretA < fretB ? fretA : fretB);
                    var high = (byte)(fretA > fretB ? fretA : fretB);
                    yield return new PatternMatch(
                        i, end, PatternKind.Trill, PatternDirection.None,
                        PatternHelpers.AverageNps(notes, i, end), low, high);
                    i = end + 1;
                }
                else
                {
                    i++;
                }
            }
        }
    }
}
