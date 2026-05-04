namespace Ghpp.Core.Abstractions.Patterns
{
    /// <summary>
    /// Named pattern shapes from the Clone Hero Wiki dictionary
    /// (https://wiki.clonehero.net/books/general-info/page/dictionary).
    /// Reverse variants (e.g., reverse chimneys, descending ladders) are not
    /// distinct kinds; <see cref="PatternMatch.Direction"/> records orientation.
    /// </summary>
    public enum PatternKind : byte
    {
        /// <summary>Repeating two-note alternation (e.g., R-Y-R-Y-R-Y).</summary>
        Trill = 0,

        /// <summary>3-or-more-note pattern that loops back and forth (e.g., G-R-Y-R-G-R-Y-R).</summary>
        Zig = 1,

        /// <summary>Pattern that ascends or descends by 2-note steps at adjacent frets.</summary>
        Ladder = 2,

        /// <summary>Zig-shaped pattern beginning or ending with its highest note.</summary>
        Chimney = 3,

        /// <summary>Reverse chimney: zig-shaped pattern beginning or ending with its lowest note.</summary>
        ReverseChimney = 4,

        /// <summary>Three consecutive notes monotonically ascending or descending by one fret each.</summary>
        Triplet = 5,

        /// <summary>Four consecutive notes monotonically ascending or descending by one fret each.</summary>
        Quad = 6,

        /// <summary>Five consecutive notes monotonically ascending or descending by one fret each.</summary>
        Quint = 7,
    }
}
