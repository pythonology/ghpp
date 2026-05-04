using System.Collections.Generic;
using Ghpp.Core.Abstractions;
using Ghpp.Core.Abstractions.Patterns;
using Ghpp.Core.Models;

namespace Ghpp.Core.Patterns.Detectors
{
    /// <summary>
    /// Detects monotonic single-fret runs of length 3 (Triplet), 4 (Quad),
    /// and 5 (Quint). A run is a sequence of consecutive single-fret notes
    /// whose frets are strictly monotonic in one direction — every successive
    /// step is positive (ascending) or every successive step is negative
    /// (descending). Step size is not required to be 1, so e.g. O-B-Y-G
    /// (steps -1, -1, -2) is still a Quad even though the last step skips a
    /// fret. The "extra" fret at the boundary serves as a natural anchor for
    /// the descent. Gaps between notes still break the run.
    /// </summary>
    /// <remarks>
    /// Longer matching runs subsume shorter ones at the same location: a
    /// 4-note descent is reported only as a Quad, not also as a Triplet.
    /// </remarks>
    internal sealed class MonotonicRunDetector : IPatternDetector
    {
        public IEnumerable<PatternMatch> Scan(IReadOnlyList<Note> notes, DifficultyOptions options)
        {
            var maxGap = options.PatternMaxInterNoteSeconds;
            var i = 0;
            while (i < notes.Count - 1)
            {
                var startFret = PatternHelpers.SingleFretIndex(notes[i]);
                var nextFret = PatternHelpers.SingleFretIndex(notes[i + 1]);
                if (startFret < 0 || nextFret < 0 || nextFret == startFret ||
                    PatternHelpers.GapBreaks(notes[i], notes[i + 1], maxGap))
                {
                    i++;
                    continue;
                }

                var direction = nextFret > startFret ? 1 : -1;

                // Walk forward as long as direction stays consistent. Step
                // size is unconstrained so a run like O-B-Y-G (descending,
                // step skips at the end) still extends.
                var end = i + 1;
                while (end + 1 < notes.Count)
                {
                    var fretHere = PatternHelpers.SingleFretIndex(notes[end]);
                    var fretNext = PatternHelpers.SingleFretIndex(notes[end + 1]);
                    if (fretNext < 0 ||
                        PatternHelpers.GapBreaks(notes[end], notes[end + 1], maxGap))
                    {
                        break;
                    }
                    var step = fretNext - fretHere;
                    if (step == 0) break;
                    var stepDir = step > 0 ? 1 : -1;
                    if (stepDir != direction) break;
                    end++;
                }

                var length = end - i + 1;
                PatternKind? kind = null;
                if (length >= 5) kind = PatternKind.Quint;
                else if (length == 4) kind = PatternKind.Quad;
                else if (length == 3) kind = PatternKind.Triplet;

                if (kind.HasValue)
                {
                    byte low = byte.MaxValue, high = 0;
                    for (var k = i; k <= end; k++)
                    {
                        var f = PatternHelpers.SingleFretIndex(notes[k]);
                        if (f < low) low = (byte)f;
                        if (f > high) high = (byte)f;
                    }
                    yield return new PatternMatch(
                        i, end, kind.Value,
                        direction > 0 ? PatternDirection.Ascending : PatternDirection.Descending,
                        PatternHelpers.AverageNps(notes, i, end), low, high);
                    i = end; // allow overlap by one note so back-to-back runs are findable
                }
                i++;
            }
        }
    }
}
