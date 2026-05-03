namespace Ghpp.Core.Abstractions
{
    /// <summary>
    /// The strum-style classification of a note.
    /// </summary>
    public enum NoteType
    {
        /// <summary>
        /// Standard note. Always requires strumming.
        /// </summary>
        Strum,

        /// <summary>
        /// Hammer-on / pull-off. Only requires strumming after a combo break.
        /// </summary>
        Hopo,

        /// <summary>
        /// Tap note. Never requires strumming.
        /// </summary>
        Tap,
    }
}
