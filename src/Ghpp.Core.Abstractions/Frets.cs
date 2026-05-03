using System;

namespace Ghpp.Core.Abstractions
{
    /// <summary>
    /// Bitmask of which frets are pressed for a note.
    /// <see cref="None"/> represents an open strum (no frets held).
    /// </summary>
    [Flags]
    public enum Frets : byte
    {
        None = 0,
        Green = 1 << 0,
        Red = 1 << 1,
        Yellow = 1 << 2,
        Blue = 1 << 3,
        Orange = 1 << 4,
    }
}
