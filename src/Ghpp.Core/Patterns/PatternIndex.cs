using System.Collections.Generic;
using Ghpp.Core.Abstractions.Patterns;

namespace Ghpp.Core.Patterns
{
    /// <summary>
    /// Per-note lookup of pattern memberships. For each note index, exposes
    /// (a) a <see cref="ulong"/> bitmask with one bit set per
    /// <see cref="PatternKind"/> that the note participates in, and
    /// (b) the list of full <see cref="PatternMatch"/> records covering it.
    /// </summary>
    public sealed class PatternIndex
    {
        private readonly ulong[] _masks;
        private readonly List<int>[] _matchIndices;
        private readonly IReadOnlyList<PatternMatch> _matches;

        internal PatternIndex(int noteCount, IReadOnlyList<PatternMatch> matches)
        {
            _masks = new ulong[noteCount];
            _matchIndices = new List<int>[noteCount];
            _matches = matches;

            for (var m = 0; m < matches.Count; m++)
            {
                var match = matches[m];
                var bit = 1UL << (int)match.Kind;
                for (var i = match.StartIndex; i <= match.EndIndex && i < noteCount; i++)
                {
                    _masks[i] |= bit;
                    if (_matchIndices[i] == null)
                    {
                        _matchIndices[i] = new List<int>(2);
                    }
                    _matchIndices[i].Add(m);
                }
            }
        }

        public IReadOnlyList<PatternMatch> Matches => _matches;

        /// <summary>Bitmask of <see cref="PatternKind"/>s covering note <paramref name="noteIndex"/>.</summary>
        public ulong KindMaskAt(int noteIndex) => _masks[noteIndex];

        /// <summary>Indices into <see cref="Matches"/> for matches that cover note <paramref name="noteIndex"/>.</summary>
        public IReadOnlyList<int> MatchIndicesAt(int noteIndex)
        {
            var list = _matchIndices[noteIndex];
            return list ?? (IReadOnlyList<int>)System.Array.Empty<int>();
        }

        public bool NoteIsIn(int noteIndex, PatternKind kind)
        {
            return (_masks[noteIndex] & (1UL << (int)kind)) != 0;
        }
    }
}
