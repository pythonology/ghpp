using System.Collections.Generic;
using Ghpp.Core.Abstractions;
using Ghpp.Core.Abstractions.Patterns;
using Ghpp.Core.Models;

namespace Ghpp.Core.Patterns.Detectors
{
    /// <summary>
    /// Detects chimneys and reverse chimneys: repeating zig cycles with a
    /// single connecting note at the peak (chimney) or trough (reverse chimney)
    /// between each cycle.
    ///
    /// Chimney example:        G-R-Y-R-G  Y  G-R-Y-R-G  Y  ...
    ///                         [  cycle  ][c] [  cycle  ][c]
    ///
    /// ReverseChimney example: Y-R-G-R-Y  G  Y-R-G-R-Y  G  ...
    ///                         [  cycle  ][c] [  cycle  ][c]
    ///
    /// Minimum match: 1 complete cycle (5 notes) + 1 connecting note = 6 notes.
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
                var startFret = PatternHelpers.SingleFretIndex(notes[i]);
                if (startFret < 0) { i++; continue; }
                var end = i;
                while (end + 1 < notes.Count
                    && PatternHelpers.SingleFretIndex(notes[end + 1]) >= 0
                    && !PatternHelpers.GapBreaks(notes[end], notes[end + 1], maxGap))
                {
                    end++;
                }

                var length = end - i + 1;
                if (length >= minLength)
                {
                    if (TryParseChimney(notes, i, end, out var chimneyEnd))
                        yield return MakeMatch(notes, i, chimneyEnd, PatternKind.Chimney);

                    if (TryParseReverseChimney(notes, i, end, out var revEnd))
                        yield return MakeMatch(notes, i, revEnd, PatternKind.ReverseChimney);
                }
                i = end + 1;
            }
        }

        /// <summary>
        /// Try to parse as chimney: cycles ascend from base to peak then descend
        /// back to base, with single connecting notes at the peak between cycles.
        /// </summary>
        private static bool TryParseChimney(
            IReadOnlyList<Note> notes, int start, int runEnd, out int matchEnd)
        {
            matchEnd = -1;
            var baseFret = PatternHelpers.SingleFretIndex(notes[start]);
            if (baseFret < 0) return false;

            var pos = start;
            var cycles = 0;
            var peakFret = -1;
            var lastGoodEnd = -1;
            var hasConnectingNote = false;

            while (pos <= runEnd)
            {
                var cycleEnd = FindAscendDescendCycle(notes, pos, runEnd, baseFret, out var cyclePeak);
                if (cycleEnd < 0) break;

                if (peakFret < 0) peakFret = cyclePeak;
                else if (cyclePeak != peakFret) break;

                cycles++;
                lastGoodEnd = cycleEnd;
                pos = cycleEnd + 1;

                if (pos <= runEnd)
                {
                    var connectFret = PatternHelpers.SingleFretIndex(notes[pos]);
                    if (connectFret == peakFret)
                    {
                        hasConnectingNote = true;
                        lastGoodEnd = pos;
                        pos++;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            if (cycles >= 1 && hasConnectingNote)
            {
                matchEnd = lastGoodEnd;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Try to parse as reverse chimney: cycles descend from peak to base then
        /// ascend back to peak, with single connecting notes at the base between cycles.
        /// </summary>
        private static bool TryParseReverseChimney(
            IReadOnlyList<Note> notes, int start, int runEnd, out int matchEnd)
        {
            matchEnd = -1;
            var peakFret = PatternHelpers.SingleFretIndex(notes[start]);
            if (peakFret < 0) return false;

            var pos = start;
            var cycles = 0;
            var baseFret = -1;
            var lastGoodEnd = -1;
            var hasConnectingNote = false;

            while (pos <= runEnd)
            {
                var cycleEnd = FindDescendAscendCycle(notes, pos, runEnd, peakFret, out var cycleBase);
                if (cycleEnd < 0) break;

                if (baseFret < 0) baseFret = cycleBase;
                else if (cycleBase != baseFret) break;

                cycles++;
                lastGoodEnd = cycleEnd;
                pos = cycleEnd + 1;

                if (pos <= runEnd)
                {
                    var connectFret = PatternHelpers.SingleFretIndex(notes[pos]);
                    if (connectFret == baseFret)
                    {
                        hasConnectingNote = true;
                        lastGoodEnd = pos;
                        pos++;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            if (cycles >= 1 && hasConnectingNote)
            {
                matchEnd = lastGoodEnd;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Find a complete ascend-then-descend cycle: starts at baseFret, strictly
        /// ascends (≥3 notes) to a peak, then strictly descends back to baseFret.
        /// Returns the end index of the cycle, or -1 if not found.
        /// </summary>
        private static int FindAscendDescendCycle(
            IReadOnlyList<Note> notes, int start, int runEnd, int baseFret, out int peakFret)
        {
            peakFret = -1;
            if (start > runEnd) return -1;
            if (PatternHelpers.SingleFretIndex(notes[start]) != baseFret) return -1;

            var pos = start;
            var prevFret = baseFret;

            // Phase 1: strictly ascending
            while (pos + 1 <= runEnd)
            {
                var nextFret = PatternHelpers.SingleFretIndex(notes[pos + 1]);
                if (nextFret < 0 || nextFret <= prevFret) break;
                prevFret = nextFret;
                pos++;
            }
            var ascentNotes = pos - start + 1;
            if (ascentNotes < 3) return -1; // need at least 3 distinct ascending frets
            peakFret = prevFret;

            // Phase 2: strictly descending back to base
            while (pos + 1 <= runEnd)
            {
                var nextFret = PatternHelpers.SingleFretIndex(notes[pos + 1]);
                if (nextFret < 0 || nextFret >= prevFret) break;
                prevFret = nextFret;
                pos++;
                if (prevFret == baseFret) return pos;
            }

            return -1; // didn't return to base
        }

        /// <summary>
        /// Find a complete descend-then-ascend cycle: starts at peakFret, strictly
        /// descends (≥3 notes) to a trough, then strictly ascends back to peakFret.
        /// Returns the end index of the cycle, or -1 if not found.
        /// </summary>
        private static int FindDescendAscendCycle(
            IReadOnlyList<Note> notes, int start, int runEnd, int peakFret, out int baseFret)
        {
            baseFret = -1;
            if (start > runEnd) return -1;
            if (PatternHelpers.SingleFretIndex(notes[start]) != peakFret) return -1;

            var pos = start;
            var prevFret = peakFret;

            // Phase 1: strictly descending
            while (pos + 1 <= runEnd)
            {
                var nextFret = PatternHelpers.SingleFretIndex(notes[pos + 1]);
                if (nextFret < 0 || nextFret >= prevFret) break;
                prevFret = nextFret;
                pos++;
            }
            var descentNotes = pos - start + 1;
            if (descentNotes < 3) return -1; // need at least 3 distinct descending frets
            baseFret = prevFret;

            // Phase 2: strictly ascending back to peak
            while (pos + 1 <= runEnd)
            {
                var nextFret = PatternHelpers.SingleFretIndex(notes[pos + 1]);
                if (nextFret < 0 || nextFret <= prevFret) break;
                prevFret = nextFret;
                pos++;
                if (prevFret == peakFret) return pos;
            }

            return -1; // didn't return to peak
        }

        private static PatternMatch MakeMatch(
            IReadOnlyList<Note> notes, int start, int end, PatternKind kind)
        {
            byte low = byte.MaxValue, high = 0;
            for (var k = start; k <= end; k++)
            {
                var f = PatternHelpers.SingleFretIndex(notes[k]);
                if (f < low) low = (byte)f;
                if (f > high) high = (byte)f;
            }
            return new PatternMatch(
                start, end, kind, PatternDirection.None,
                PatternHelpers.AverageNps(notes, start, end), low, high);
        }
    }
}
