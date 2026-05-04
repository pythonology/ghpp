using Ghpp.Core.Abstractions;

namespace Ghpp.Core.Patterns
{
    /// <summary>
    /// Shared helpers for pattern detectors: single-fret extraction, gap
    /// gating, and NPS calculation across a contiguous note run.
    /// </summary>
    internal static class PatternHelpers
    {
        /// <summary>
        /// Returns the topmost fret index for a single-fret note, or -1 if
        /// the note is open or a chord. Pattern detectors operate exclusively
        /// on single-fret runs; chords break a pattern.
        /// </summary>
        public static int SingleFretIndex(Note note)
        {
            if (note.IsOpen || note.FretCount != 1)
            {
                return -1;
            }
            var bits = (byte)note.Frets;
            var position = -1;
            while (bits != 0)
            {
                position++;
                bits >>= 1;
            }
            return position;
        }

        /// <summary>
        /// True if the inter-note time gap from <paramref name="prev"/> to
        /// <paramref name="curr"/> exceeds the pattern continuity threshold.
        /// A pattern run is broken whenever notes are too far apart in time
        /// to be perceived as a single pattern.
        /// </summary>
        public static bool GapBreaks(Note prev, Note curr, double maxInterNoteSeconds)
        {
            return (curr.TimeSeconds - prev.TimeSeconds) > maxInterNoteSeconds;
        }

        /// <summary>
        /// Average notes-per-second across the run [<paramref name="startIndex"/>,
        /// <paramref name="endIndex"/>] (inclusive). For a single-note run,
        /// returns 0. The window is the time elapsed across the run.
        /// </summary>
        public static double AverageNps(
            System.Collections.Generic.IReadOnlyList<Note> notes,
            int startIndex,
            int endIndex)
        {
            var span = notes[endIndex].TimeSeconds - notes[startIndex].TimeSeconds;
            if (span <= 0)
            {
                return 0.0;
            }
            var gaps = endIndex - startIndex;
            return gaps / span;
        }
    }
}
