namespace Ghpp.Core.Abstractions
{
    /// <summary>
    /// A single playable note (or chord) on the chart timeline.
    /// </summary>
    public sealed class Note
    {
        public Note(
            int timeTicks,
            double timeSeconds,
            Frets frets,
            int sustainTicks,
            double sustainSeconds,
            NoteType type)
        {
            TimeTicks = timeTicks;
            TimeSeconds = timeSeconds;
            Frets = frets;
            SustainTicks = sustainTicks;
            SustainSeconds = sustainSeconds;
            Type = type;
        }

        /// <summary>Position on the chart's tick grid.</summary>
        public int TimeTicks { get; }

        /// <summary>
        /// Position on the chart's tick grid in seconds, with the chart's tempo map and offset already applied.
        /// </summary>
        public double TimeSeconds { get; }

        /// <summary>Frets held for this note. <see cref="Frets.None"/> means open.</summary>
        public Frets Frets { get; }

        /// <summary>Sustain length in ticks.</summary>
        public int SustainTicks { get; }

        /// <summary>Sustain length in seconds.</summary>
        public double SustainSeconds { get; }

        /// <summary>The strum-style classification of the note.</summary>
        public NoteType Type { get; }

        /// <summary><see langword="true"/> when no frets are held.</summary>
        public bool IsOpen => FretCount == 0;

        /// <summary>Number of frets held.</summary>
        public int FretCount
        {
            get
            {
                var count = 0;
                var bits = (byte)Frets;
                while (bits != 0)
                {
                    count += bits & 1;
                    bits >>= 1;
                }
                return count;
            }
        }
    }
}
