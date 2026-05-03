using System.Globalization;

namespace Ghpp.Parsers.ChartFile;

/// <summary>
/// Reads a .chart file line by line into raw, untyped section data. One
/// instance is one read; the parsed values are exposed as properties for
/// the parser to consume.
/// </summary>
internal sealed class Tokenizer
{
    private const int DefaultResolution = 192;

    public int Resolution { get; private set; } = DefaultResolution;
    public double OffsetSeconds { get; private set; }
    public Dictionary<string, string> Metadata { get; } = new(StringComparer.Ordinal);
    public List<Tempo> Tempos { get; } = [];
    public Dictionary<string, List<Entry>> NoteEntries { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// One tempo (BPM) event from the [SyncTrack] section.
    /// </summary>
    public readonly record struct Tempo(int Tick, double Bpm);

    /// <summary>
    /// One raw N entry from a note section, before chord grouping or classification.
    /// </summary>
    public readonly record struct Entry(int Tick, int Lane, int SustainTicks);

    public void Read(TextReader reader)
    {
        string? section = null;
        var inBlock = false;

        while (reader.ReadLine() is { } line)
        {
            var trimmed = line.AsSpan().Trim();
            if (trimmed.IsEmpty)
            {
                continue;
            }

            if (trimmed[0] == '[' && trimmed[^1] == ']')
            {
                section = trimmed[1..^1].Trim().ToString();
                inBlock = false;
                continue;
            }
            switch (trimmed)
            {
                case "{":
                    inBlock = true;
                    continue;
                case "}":
                    inBlock = false;
                    section = null;
                    continue;
            }

            if (!inBlock || section is null)
            {
                continue;
            }

            var equalsIndex = trimmed.IndexOf('=');
            if (equalsIndex < 0)
            {
                continue;
            }
            var key = trimmed[..equalsIndex].Trim();
            var value = trimmed[(equalsIndex + 1)..].Trim();

            switch (section)
            {
                case "Song":
                    ReadSongLine(key, value);
                    break;
                case "SyncTrack":
                    ReadSyncLine(key, value);
                    break;
                default:
                    ReadNoteLine(section, key, value);
                    break;
            }
        }
    }

    private void ReadSongLine(ReadOnlySpan<char> key, ReadOnlySpan<char> value)
    {
        switch (key)
        {
            case "Resolution" when TryParseInt(value, out var resolution):
                Resolution = resolution;
                break;
            case "Offset" when TryParseDouble(value, out var offset):
                OffsetSeconds = offset;
                break;
        }

        // Stash everything in metadata for debugging purposes
        var unquoted = value;
        if (unquoted.Length >= 2 && unquoted[0] == '"' && unquoted[^1] == '"')
        {
            unquoted = unquoted[1..^1];
        }
        Metadata[key.ToString()] = unquoted.ToString();
    }

    private void ReadSyncLine(ReadOnlySpan<char> key, ReadOnlySpan<char> value)
    {
        if (!TryParseInt(key, out var tick))
        {
            return;
        }

        // Only B (BPM) is needed for now; TS (time signature) and A (anchor) are skipped.
        var tokens = SplitWhitespace(value);
        if (tokens.Count < 2 || value[tokens[0]] is not "B")
        {
            return;
        }
        if (!TryParseInt(value[tokens[1]], out var milliBeatsPerMinute))
        {
            return;
        }

        Tempos.Add(new Tempo(tick, milliBeatsPerMinute / 1000.0));
    }

    private void ReadNoteLine(string section, ReadOnlySpan<char> key, ReadOnlySpan<char> value)
    {
        if (!TryParseInt(key, out var tick))
        {
            return;
        }

        // We only care about N entries; S (star power) and E (event) are skipped.
        var tokens = SplitWhitespace(value);
        if (tokens.Count < 3 || value[tokens[0]] is not "N")
        {
            return;
        }
        if (!TryParseInt(value[tokens[1]], out var lane))
        {
            return;
        }
        if (!TryParseInt(value[tokens[2]], out var sustainTicks))
        {
            return;
        }

        if (!NoteEntries.TryGetValue(section, out var list))
        {
            list = [];
            NoteEntries[section] = list;
        }
        list.Add(new Entry(tick, lane, sustainTicks));
    }

    private static bool TryParseInt(ReadOnlySpan<char> text, out int value) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    private static bool TryParseDouble(ReadOnlySpan<char> text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    private static List<Range> SplitWhitespace(ReadOnlySpan<char> text)
    {
        var ranges = new List<Range>(4);
        var cursor = 0;
        while (cursor < text.Length)
        {
            while (cursor < text.Length && char.IsWhiteSpace(text[cursor]))
            {
                cursor++;
            }
            if (cursor >= text.Length)
            {
                break;
            }
            var tokenStart = cursor;
            while (cursor < text.Length && !char.IsWhiteSpace(text[cursor]))
            {
                cursor++;
            }
            ranges.Add(new Range(tokenStart, cursor));
        }
        return ranges;
    }
}
