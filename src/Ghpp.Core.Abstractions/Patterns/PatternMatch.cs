namespace Ghpp.Core.Abstractions.Patterns
{
    /// <summary>
    /// A detected pattern occurrence: a contiguous run of notes
    /// (<see cref="StartIndex"/> through <see cref="EndIndex"/> inclusive,
    /// indexing into the chart's time-sorted note list) that matches a
    /// particular <see cref="PatternKind"/>.
    /// </summary>
    public readonly struct PatternMatch
    {
        public PatternMatch(
            int startIndex,
            int endIndex,
            PatternKind kind,
            PatternDirection direction,
            double averageNps,
            byte lowFret,
            byte highFret)
        {
            StartIndex = startIndex;
            EndIndex = endIndex;
            Kind = kind;
            Direction = direction;
            AverageNps = averageNps;
            LowFret = lowFret;
            HighFret = highFret;
        }

        /// <summary>First note in the matched run (inclusive).</summary>
        public int StartIndex { get; }

        /// <summary>Last note in the matched run (inclusive).</summary>
        public int EndIndex { get; }

        public PatternKind Kind { get; }

        /// <summary>Orientation of the pattern. <see cref="PatternDirection.None"/> for non-directional kinds.</summary>
        public PatternDirection Direction { get; }

        /// <summary>Average notes-per-second across the matched run.</summary>
        public double AverageNps { get; }

        /// <summary>Lowest fret involved (0=Green..4=Orange).</summary>
        public byte LowFret { get; }

        /// <summary>Highest fret involved (0=Green..4=Orange).</summary>
        public byte HighFret { get; }

        public int Length => EndIndex - StartIndex + 1;
    }

    public enum PatternDirection : byte
    {
        None = 0,
        Ascending = 1,
        Descending = 2,
    }
}
