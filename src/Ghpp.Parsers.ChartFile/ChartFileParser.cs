using Ghpp.Core.Abstractions;

namespace Ghpp.Parsers.ChartFile;

/// <summary>
/// Reads the legacy <c>notes.chart</c> text format (FoF / Phase Shift / Clone Hero).
/// Returns a <see cref="Chart"/> built from one note section: defaults to the
/// Expert lead-guitar section, falling back through Hard / Medium / Easy.
/// </summary>
public sealed class ChartFileParser : IChartFileParser
{
    private const string DefaultSection = "ExpertSingle";

    private static readonly string[] DefaultSectionPriority =
    [
        "ExpertSingle",
        "HardSingle",
        "MediumSingle",
        "EasySingle",
    ];

    // Lane indices in the .chart file format.
    private const int LaneGreen  = 0;
    private const int LaneRed    = 1;
    private const int LaneYellow = 2;
    private const int LaneBlue   = 3;
    private const int LaneOrange = 4;
    private const int LaneForce  = 5;
    private const int LaneTap    = 6;
    private const int LaneOpen   = 7;

    public Chart Parse(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        using var reader = new StreamReader(path);
        return Parse(reader);
    }

    public Chart Parse(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        var tokenizer = ReadFile(reader);
        return Build(tokenizer, PickDefaultSection(tokenizer));
    }

    /// <summary>Parse a specific section by name (e.g. "ExpertSingle", "HardDoubleBass").</summary>
    public Chart Parse(string path, string section)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(section);
        using var reader = new StreamReader(path);
        return Parse(reader, section);
    }

    /// <inheritdoc cref="Parse(string, string)" />
    public Chart Parse(TextReader reader, string section)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(section);
        return Build(ReadFile(reader), section);
    }

    private static Tokenizer ReadFile(TextReader reader)
    {
        var tokenizer = new Tokenizer();
        tokenizer.Read(reader);
        return tokenizer;
    }

    private static string PickDefaultSection(Tokenizer tokenizer)
    {
        foreach (var sectionName in DefaultSectionPriority)
        {
            if (tokenizer.NoteEntries.TryGetValue(sectionName, out var entries) && entries.Count > 0)
            {
                return sectionName;
            }
        }
        return DefaultSection;
    }

    private static Chart Build(Tokenizer tokenizer, string section)
    {
        tokenizer.Tempos.Sort((left, right) => left.Tick.CompareTo(right.Tick));
        var clock = new TempoClock(tokenizer.Resolution, tokenizer.Tempos, tokenizer.OffsetSeconds);
        var hopoThresholdTicks = tokenizer.Resolution / 3;

        var entries = tokenizer.NoteEntries.TryGetValue(section, out var found) ? found : [];
        entries.Sort((left, right) => left.Tick.CompareTo(right.Tick));

        var notes = GroupNotes(entries, clock, hopoThresholdTicks);
        return new Chart(tokenizer.Metadata, tokenizer.Resolution, notes);
    }

    /// <summary>
    /// Collapses tick-sorted raw entries into chord-aware <see cref="Note"/>s,
    /// folding Force/Tap pseudo-lanes into <see cref="NoteType"/> via
    /// <see cref="NoteClassifier"/>. Ticks with only modifier flags get dropped.
    /// </summary>
    private static List<Note> GroupNotes(List<Tokenizer.Entry> tickSortedEntries, TempoClock clock, int hopoThresholdTicks)
    {
        var notes = new List<Note>();
        Note? previous = null;
        var groupStart = 0;

        while (groupStart < tickSortedEntries.Count)
        {
            var tick = tickSortedEntries[groupStart].Tick;
            var groupEnd = groupStart;

            var frets = Frets.None;
            var sustainTicks = 0;
            var perFretTicks = new int[5];
            var openSustainTicks = 0;
            var hasOpen = false;
            var hasFret = false;
            var isForced = false;
            var isTap = false;

            while (groupEnd < tickSortedEntries.Count && tickSortedEntries[groupEnd].Tick == tick)
            {
                var entry = tickSortedEntries[groupEnd];
                switch (entry.Lane)
                {
                    case LaneGreen:
                        frets |= Frets.Green;
                        hasFret = true;
                        if (entry.SustainTicks > perFretTicks[0]) perFretTicks[0] = entry.SustainTicks;
                        break;
                    case LaneRed:
                        frets |= Frets.Red;
                        hasFret = true;
                        if (entry.SustainTicks > perFretTicks[1]) perFretTicks[1] = entry.SustainTicks;
                        break;
                    case LaneYellow:
                        frets |= Frets.Yellow;
                        hasFret = true;
                        if (entry.SustainTicks > perFretTicks[2]) perFretTicks[2] = entry.SustainTicks;
                        break;
                    case LaneBlue:
                        frets |= Frets.Blue;
                        hasFret = true;
                        if (entry.SustainTicks > perFretTicks[3]) perFretTicks[3] = entry.SustainTicks;
                        break;
                    case LaneOrange:
                        frets |= Frets.Orange;
                        hasFret = true;
                        if (entry.SustainTicks > perFretTicks[4]) perFretTicks[4] = entry.SustainTicks;
                        break;
                    case LaneForce:
                        isForced = true;
                        break;
                    case LaneTap:
                        isTap = true;
                        break;
                    case LaneOpen:
                        hasOpen = true;
                        if (entry.SustainTicks > openSustainTicks) openSustainTicks = entry.SustainTicks;
                        break;
                }
                if (IsPlayableLane(entry.Lane) && entry.SustainTicks > sustainTicks)
                {
                    sustainTicks = entry.SustainTicks;
                }
                groupEnd++;
            }

            groupStart = groupEnd;

            switch (hasFret)
            {
                case false when !hasOpen:
                    // Tick had only modifier flags. Skip.
                    continue;
                case false:
                    // Explicit open: clear stray fret bits and per-fret sustains.
                    frets = Frets.None;
                    Array.Clear(perFretTicks, 0, perFretTicks.Length);
                    sustainTicks = openSustainTicks;
                    break;
            }

            var time = clock.TickToSeconds(tick);
            var sustainSeconds = sustainTicks > 0
                ? clock.TickToSeconds(tick + sustainTicks) - time
                : 0.0;

            var perFretSeconds = new double[5];
            for (var k = 0; k < 5; k++)
            {
                if (perFretTicks[k] > 0)
                {
                    perFretSeconds[k] = clock.TickToSeconds(tick + perFretTicks[k]) - time;
                }
            }

            var type = NoteClassifier.Classify(tick, frets, isForced, isTap, previous, hopoThresholdTicks);

            var note = new Note(tick, time, frets, sustainTicks, sustainSeconds, type, perFretSeconds);
            notes.Add(note);
            previous = note;
        }
        return notes;
    }

    private static bool IsPlayableLane(int lane) =>
        lane is LaneGreen or LaneRed or LaneYellow or LaneBlue or LaneOrange or LaneOpen;
}
