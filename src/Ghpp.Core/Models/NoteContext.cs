using Ghpp.Core.Abstractions;

namespace Ghpp.Core.Models
{
    /// <summary>
    /// A <see cref="Note"/> with the per-note pipeline annotations attached:
    /// hand-anchor info, pattern memberships, and the per-Bar contributions
    /// (FretComplexity / StrumComplexity / SustainComplexity) that the Bars
    /// pipeline writes during evaluation. Used internally by the calculator;
    /// not exposed to callers.
    /// </summary>
    internal sealed class NoteContext
    {
        public NoteContext(Note source, Frets prevFrets)
        {
            Source = source;
            PrevFrets = prevFrets;
            FretPosition = ComputeTopmostFret(source.Frets);
        }

        public Note Source { get; }

        /// <summary>
        /// The <see cref="Frets"/> of the immediately previous note in time-sorted
        /// order, or <see cref="Frets.None"/> for the first note. FretComplexity
        /// needs the full chord shape to compute anchor and pivot effects, not
        /// just the topmost fret.
        /// </summary>
        public Frets PrevFrets { get; }

        /// <summary>Highest fret pressed (0=Green..4=Orange). -1 for open notes.</summary>
        public int FretPosition { get; }

        /// <summary>
        /// One playable unit per chord stack. Open notes count as 1 even though
        /// no fret bits are set; the NPS curve treats every chord stack as one
        /// strum/HOPO/tap event regardless of how many frets it holds.
        /// </summary>
        public int PlayableUnits => Source.IsOpen ? 1 : Source.FretCount;

        // --- Bars pipeline outputs. Written by the per-Bar evaluators. ------

        /// <summary>Pattern membership bitmask (one bit per <see cref="Patterns.PatternKind"/>). 0 means no pattern.</summary>
        public ulong PatternMask { get; set; }

        /// <summary>Chord/transition cost contributed by this note. Written by FretComplexity.</summary>
        public double FretCost { get; set; }

        /// <summary>Strum-rhythm contribution (only meaningful for effective strums). Written by StrumComplexity.</summary>
        public double StrumContribution { get; set; }

        /// <summary>Sustain-load weight contributed by this note. Written by SustainComplexity (diagnostic).</summary>
        public double SustainLoad { get; set; }

        private static int ComputeTopmostFret(Frets frets)
        {
            if (frets == Frets.None)
            {
                return -1;
            }
            var bits = (byte)frets;
            var position = -1;
            while (bits != 0)
            {
                position++;
                bits >>= 1;
            }
            return position;
        }
    }
}
