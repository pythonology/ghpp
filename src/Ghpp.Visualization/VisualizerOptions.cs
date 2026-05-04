namespace Ghpp.Visualization
{
    /// <summary>
    /// Visual layout knobs for <see cref="HtmlVisualizer"/>. Defaults render a
    /// 60 px/sec vertical highway (bottom = start of song) with five 32 px
    /// lanes, eight 30 px pattern columns, and four ~80 px Bar curve columns
    /// to the right.
    /// </summary>
    public sealed class VisualizerOptions
    {
        /// <summary>Note distance scale: how many vertical pixels per second of song time.</summary>
        public double PixelsPerSecond { get; set; } = 60.0;

        /// <summary>Width (px) of each fret lane on the highway. Wider lanes = more horizontal breathing room.</summary>
        public int LaneWidthPx { get; set; } = 56;

        /// <summary>Radius (px) of note heads. Should scale roughly with <see cref="LaneWidthPx"/>; defaults are tuned together.</summary>
        public int NoteRadiusPx { get; set; } = 18;

        /// <summary>Width (px) of each per-pattern column in the patterns panel.</summary>
        public int PatternColumnWidthPx { get; set; } = 28;

        /// <summary>Width (px) of each Bar curve column.</summary>
        public int BarCurveWidthPx { get; set; } = 80;

        /// <summary>Width (px) of the composite curve column.</summary>
        public int CompositeCurveWidthPx { get; set; } = 110;

        /// <summary>Width (px) of the left-hand time axis column.</summary>
        public int TimeAxisWidthPx { get; set; } = 56;

        /// <summary>Major tick interval (seconds) on the time axis.</summary>
        public double TimeAxisTickSeconds { get; set; } = 10.0;

        /// <summary>Reserved height (px) at the top for column headers (lane letters, kind names, curve labels + value-tick labels).</summary>
        public int ColumnHeaderHeightPx { get; set; } = 38;
    }
}
