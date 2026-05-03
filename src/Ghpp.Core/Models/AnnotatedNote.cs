using Ghpp.Core.Abstractions;

namespace Ghpp.Core.Models
{
    /// <summary>
    /// A <see cref="Note"/> with the per-note pipeline annotations attached:
    /// the topmost-fret hand position and the rhythm and fret multipliers.
    /// Used internally by the calculator; not exposed to callers.
    /// </summary>
    internal sealed class AnnotatedNote
    {
        public AnnotatedNote(Note source)
        {
            Source = source;
            FretPosition = ComputeTopmostFret(source.Frets);
        }

        public Note Source { get; }

        /// <summary>Highest fret pressed (0=Green..4=Orange). -1 for open notes.</summary>
        public int FretPosition { get; }

        /// <summary>Rhythm complexity multiplier from <see cref="Complexity.RhythmComplexity"/>. 1.0 by default.</summary>
        public double RhythmMultiplier { get; set; } = 1.0;

        /// <summary>Fret hand-shift multiplier from <see cref="Complexity.FretComplexity"/>. 1.0 by default.</summary>
        public double FretMultiplier { get; set; } = 1.0;

        /// <summary>
        /// One playable unit per chord stack. Open notes count as 1 even though
        /// no fret bits are set; the NPS curve treats every chord stack as one
        /// strum/HOPO/tap event regardless of how many frets it holds.
        /// </summary>
        public int PlayableUnits => Source.IsOpen ? 1 : Source.FretCount;

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
