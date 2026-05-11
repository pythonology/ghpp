namespace Ghpp.Core.Models
{
    /// <summary>
    /// Classification of a fret-hand motion chunk produced by the
    /// <c>FretComplexity</c> Bar. Mirrors the internal chunk shape enum but is
    /// exposed publicly so visualizers can color notes by chunk membership.
    /// </summary>
    public enum FretChunkShape : byte
    {
        /// <summary>Single isolated note that does not belong to any recognized motion chunk.</summary>
        Free,
        /// <summary>Anchor alternates with one or more upper targets.</summary>
        Trill,
        /// <summary>Strictly ascending top-frets, optional skips.</summary>
        RollOn,
        /// <summary>Strictly descending top-frets, optional skips.</summary>
        RollOff,
        /// <summary>Exactly one direction reversal.</summary>
        Zig,
        /// <summary>
        /// Run of notes that can be played by holding a single union chord —
        /// every note in the run fires off the same held set with no fret
        /// motion required (e.g. GRY tap → Y tap → GRY tap, or GRYBO → BO).
        /// </summary>
        Held,
    }

    /// <summary>
    /// A single chunk produced by the FretComplexity walker, exposed for
    /// visualization. Indices reference the source <c>Chart.Notes</c> list 1-1.
    /// <see cref="InRepeat"/> reflects the K-period repeat detector at the
    /// time the chunk was emitted (true means the most recent K classified
    /// chunks exactly matched the K chunks before them, for some K up to
    /// <c>DifficultyOptions.ChunkPatternMaxLength</c>).
    /// </summary>
    public readonly struct FretChunkRecord
    {
        public FretChunkRecord(int startNoteIndex, int endNoteIndex, FretChunkShape shape, bool inRepeat)
        {
            StartNoteIndex = startNoteIndex;
            EndNoteIndex = endNoteIndex;
            Shape = shape;
            InRepeat = inRepeat;
        }

        public int StartNoteIndex { get; }
        public int EndNoteIndex { get; }
        public FretChunkShape Shape { get; }
        public bool InRepeat { get; }
    }
}
