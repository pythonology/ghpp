using Ghpp.Core.Abstractions;

namespace Ghpp.Parsers.ChartFile;

/// <summary>
/// Resolves a note's <see cref="NoteType"/> using Clone Hero's default
/// HOPO threshold rules.
/// </summary>
internal static class NoteClassifier
{
    public static NoteType Classify(
        int tick,
        Frets frets,
        bool isForced,
        bool isTap,
        Note? previous,
        int hopoThresholdTicks)
    {
        // Tap modifier wins over everything else.
        if (isTap)
        {
            return NoteType.Tap;
        }

        // A note "auto-HOPOs" off its predecessor when it is a single fret note
        // close enough in time to the previous note and using a different pattern.
        var autoHopo = false;
        if (previous is not null)
        {
            var isOpen = frets == Frets.None;
            var isSingleFret = !isOpen && IsSingleBitSet((byte)frets);
            var samePatternAsPrevious = previous.Frets == frets;
            var withinHopoWindow = (tick - previous.TimeTicks) < hopoThresholdTicks;
            autoHopo = withinHopoWindow && isSingleFret && !samePatternAsPrevious;
        }

        // The Force flag inverts the auto classification.
        if (isForced)
        {
            return autoHopo ? NoteType.Strum : NoteType.Hopo;
        }
        return autoHopo ? NoteType.Hopo : NoteType.Strum;
    }

    private static bool IsSingleBitSet(byte bits) => bits != 0 && (bits & (bits - 1)) == 0;
}
