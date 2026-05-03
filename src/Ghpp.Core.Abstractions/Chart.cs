using System.Collections.Generic;

namespace Ghpp.Core.Abstractions
{
    /// <summary>
    /// A chart containing time-sorted notes.
    /// </summary>
    public sealed class Chart
    {
        public Chart(IReadOnlyDictionary<string, string> metadata, int resolution, IReadOnlyList<Note> notes)
        {
            Metadata = metadata;
            Resolution = resolution;
            Notes = notes;
        }

        /// <summary>Key/value metadata for this chart.</summary>
        public IReadOnlyDictionary<string, string> Metadata { get; }

        /// <summary>Ticks per quarter note.</summary>
        public int Resolution { get; }

        /// <summary>Time-sorted notes for this chart.</summary>
        public IReadOnlyList<Note> Notes { get; }
    }
}
