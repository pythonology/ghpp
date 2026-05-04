using System.Collections.Generic;
using Ghpp.Core.Abstractions;
using Ghpp.Core.Abstractions.Patterns;
using Ghpp.Core.Models;

namespace Ghpp.Core.Patterns
{
    /// <summary>
    /// Scans a time-sorted note list and emits <see cref="PatternMatch"/>es
    /// for one pattern shape. Each detector is independent and may produce
    /// overlapping matches with other detectors.
    /// </summary>
    internal interface IPatternDetector
    {
        IEnumerable<PatternMatch> Scan(IReadOnlyList<Note> notes, DifficultyOptions options);
    }
}
