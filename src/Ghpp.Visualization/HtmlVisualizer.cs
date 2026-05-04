using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Ghpp.Core.Abstractions;
using Ghpp.Core.Abstractions.Patterns;
using Ghpp.Core.Models;

namespace Ghpp.Visualization
{
    /// <summary>
    /// Renders a chart and its <see cref="DifficultyReport"/> to a self-contained
    /// HTML document with a vertical Guitar-Hero-style note highway (bottom =
    /// start of song), per-pattern columns labeled with the pattern name, and
    /// vertical Fret / Strum / Sustain / composite curves to the right.
    /// </summary>
    public static class HtmlVisualizer
    {
        // Lane order from left to right of the highway (matches GH/CH controllers).
        private static readonly Frets[] LanesLeftToRight =
        {
            Frets.Green,
            Frets.Red,
            Frets.Yellow,
            Frets.Blue,
            Frets.Orange,
        };

        private static readonly string[] LaneNames = { "G", "R", "Y", "B", "O" };
        private static readonly string[] LaneColors = { "#00C800", "#E03030", "#E0C000", "#2080E0", "#E08020" };
        private const string OpenColor = "#C040E0";

        // Pattern kinds in display order (left-to-right columns in the pattern panel).
        private static readonly PatternKind[] PatternColumns =
        {
            PatternKind.Trill,
            PatternKind.Zig,
            PatternKind.Ladder,
            PatternKind.Chimney,
            PatternKind.ReverseChimney,
            PatternKind.Triplet,
            PatternKind.Quad,
            PatternKind.Quint,
        };

        private static readonly Dictionary<PatternKind, string> PatternColors = new Dictionary<PatternKind, string>
        {
            { PatternKind.Trill, "#E0C000" },
            { PatternKind.Zig, "#5FAFE0" },
            { PatternKind.Ladder, "#00C800" },
            { PatternKind.Chimney, "#E08020" },
            { PatternKind.ReverseChimney, "#E03030" },
            { PatternKind.Triplet, "#A080E0" },
            { PatternKind.Quad, "#E040C0" },
            { PatternKind.Quint, "#FF40A0" },
        };

        public static string Render(Chart chart, DifficultyReport report)
        {
            return Render(chart, report, new VisualizerOptions());
        }

        public static string Render(Chart chart, DifficultyReport report, VisualizerOptions options)
        {
            if (chart == null) throw new ArgumentNullException(nameof(chart));
            if (report == null) throw new ArgumentNullException(nameof(report));
            if (options == null) options = new VisualizerOptions();

            var startTime = chart.Notes.Count > 0 ? chart.Notes[0].TimeSeconds : 0.0;
            var endTime = chart.Notes.Count > 0 ? chart.Notes[chart.Notes.Count - 1].TimeSeconds : 0.0;
            var padded = Math.Max(0.0, startTime - 0.5);
            var totalSeconds = Math.Max(1.0, endTime + 0.5 - padded);
            var bodyHeightPx = (int)Math.Ceiling(totalSeconds * options.PixelsPerSecond);
            var totalHeightPx = bodyHeightPx + options.ColumnHeaderHeightPx;

            var ctx = new RenderContext
            {
                Options = options,
                StartTime = padded,
                EndTime = padded + totalSeconds,
                TotalSeconds = totalSeconds,
                BodyHeightPx = bodyHeightPx,
                TotalHeightPx = totalHeightPx,
                HighwayWidthPx = LanesLeftToRight.Length * options.LaneWidthPx,
                PatternsWidthPx = PatternColumns.Length * options.PatternColumnWidthPx,
            };

            var sb = new StringBuilder(96 * 1024);
            sb.Append("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\">");
            sb.Append("<title>");
            sb.Append(HtmlEscape(SongTitle(chart)));
            sb.Append(" - ghpp visualizer</title>");
            sb.Append("<style>");
            sb.Append(Css);
            sb.Append("</style></head><body>");

            AppendHeader(sb, chart, report);
            AppendPatternLegend(sb, report);

            sb.Append("<div class=\"viz\">");
            AppendTimeAxis(sb, ctx);
            AppendHighway(sb, chart, ctx);
            // Bar curves sit immediately right of the highway so values can be
            // read at-a-glance against the notes they describe.
            AppendBarCurve(sb, "Fret", report.FretCurve, "#5FAFE0", ctx, options.BarCurveWidthPx);
            AppendBarCurve(sb, "Strum", report.StrumCurve, "#E0C000", ctx, options.BarCurveWidthPx);
            AppendBarCurve(sb, "Sustain", report.SustainCurve, "#A080E0", ctx, options.BarCurveWidthPx);
            AppendPatterns(sb, chart, report, ctx);
            AppendBarCurve(sb, "Composite", report.Curve, "#FF6090", ctx, options.CompositeCurveWidthPx);
            sb.Append("</div>");

            // Auto-scroll to bottom on load so the user lands at the START of
            // the song (which lives at the bottom of the page in this layout).
            sb.Append("<script>window.scrollTo(0,document.body.scrollHeight);</script>");

            sb.Append("</body></html>");
            return sb.ToString();
        }

        // ---- Header & legend ------------------------------------------------

        private static void AppendHeader(StringBuilder sb, Chart chart, DifficultyReport report)
        {
            sb.Append("<header><h1>");
            sb.Append(HtmlEscape(SongTitle(chart)));
            sb.Append("</h1>");
            var artist = MetadataOrEmpty(chart, "Artist");
            var charter = MetadataOrEmpty(chart, "Charter");
            if (!string.IsNullOrEmpty(artist) || !string.IsNullOrEmpty(charter))
            {
                sb.Append("<div class=\"sub\">");
                if (!string.IsNullOrEmpty(artist)) { sb.Append("by "); sb.Append(HtmlEscape(artist)); }
                if (!string.IsNullOrEmpty(charter)) { sb.Append("  ·  charted by "); sb.Append(HtmlEscape(charter)); }
                sb.Append("</div>");
            }
            sb.Append("<div class=\"stats\">");
            AppendStat(sb, "★ stars", report.StarRating.ToString("0.00", CultureInfo.InvariantCulture));
            AppendStat(sb, "blended", report.Blended.ToString("0.00", CultureInfo.InvariantCulture));
            AppendStat(sb, "Fret", report.FretMean.ToString("0.000", CultureInfo.InvariantCulture));
            AppendStat(sb, "Strum", report.StrumMean.ToString("0.000", CultureInfo.InvariantCulture));
            AppendStat(sb, "Sustain", report.SustainMean.ToString("0.000", CultureInfo.InvariantCulture));
            AppendStat(sb, "notes", report.TotalNotes.ToString(CultureInfo.InvariantCulture));
            AppendStat(sb, "duration", report.DurationSeconds.ToString("0.0", CultureInfo.InvariantCulture) + "s");
            sb.Append("</div></header>");
        }

        private static void AppendStat(StringBuilder sb, string label, string value)
        {
            sb.Append("<span class=\"stat\"><span class=\"label\">"); sb.Append(label);
            sb.Append("</span><span class=\"value\">"); sb.Append(value); sb.Append("</span></span>");
        }

        private static void AppendPatternLegend(StringBuilder sb, DifficultyReport report)
        {
            if (report.Patterns == null) return;
            var counts = new Dictionary<PatternKind, int>();
            foreach (var match in report.Patterns.Matches)
            {
                counts[match.Kind] = counts.TryGetValue(match.Kind, out var c) ? c + 1 : 1;
            }
            sb.Append("<div class=\"legend\">");
            foreach (var kind in PatternColumns)
            {
                counts.TryGetValue(kind, out var n);
                sb.Append("<span class=\"legend-item\"><span class=\"swatch\" style=\"background:");
                sb.Append(PatternColors[kind]);
                sb.Append("\"></span>");
                sb.Append(kind.ToString());
                sb.Append("  <span class=\"count\">×");
                sb.Append(n.ToString(CultureInfo.InvariantCulture));
                sb.Append("</span></span>");
            }
            sb.Append("</div>");
        }

        // ---- Time axis (left column) ----------------------------------------

        private static void AppendTimeAxis(StringBuilder sb, RenderContext ctx)
        {
            sb.Append("<svg class=\"axis\" width=\"").Append(ctx.Options.TimeAxisWidthPx)
              .Append("\" height=\"").Append(ctx.TotalHeightPx).Append("\">");

            // Header label
            sb.Append("<text x=\"6\" y=\"16\" class=\"col-header\">time</text>");

            var tick = ctx.Options.TimeAxisTickSeconds;
            for (var t = Math.Ceiling(ctx.StartTime / tick) * tick; t <= ctx.EndTime; t += tick)
            {
                var y = ctx.Y(t);
                sb.Append("<line x1=\"").Append(ctx.Options.TimeAxisWidthPx - 6)
                  .Append("\" x2=\"").Append(ctx.Options.TimeAxisWidthPx)
                  .Append("\" y1=\"").Append(y.ToString("0.0", CultureInfo.InvariantCulture))
                  .Append("\" y2=\"").Append(y.ToString("0.0", CultureInfo.InvariantCulture))
                  .Append("\" class=\"tick\"/>");
                sb.Append("<text x=\"6\" y=\"").Append((y + 4).ToString("0.0", CultureInfo.InvariantCulture))
                  .Append("\" class=\"tick-label\">")
                  .Append(t.ToString("0", CultureInfo.InvariantCulture)).Append("s</text>");
            }

            sb.Append("</svg>");
        }

        // ---- Note highway (vertical, 5 lanes left-to-right) -----------------

        private static void AppendHighway(StringBuilder sb, Chart chart, RenderContext ctx)
        {
            var w = ctx.HighwayWidthPx;
            var lane = ctx.Options.LaneWidthPx;
            sb.Append("<svg class=\"highway\" width=\"").Append(w)
              .Append("\" height=\"").Append(ctx.TotalHeightPx).Append("\">");

            // Lane backgrounds + headers
            for (var i = 0; i < LanesLeftToRight.Length; i++)
            {
                var x = i * lane;
                sb.Append("<rect x=\"").Append(x).Append("\" y=\"0\" width=\"").Append(lane)
                  .Append("\" height=\"").Append(ctx.TotalHeightPx).Append("\" class=\"lane\"/>");
                sb.Append("<text x=\"").Append(x + lane / 2)
                  .Append("\" y=\"16\" class=\"col-header lane-header\" style=\"fill:")
                  .Append(LaneColors[i]).Append("\">").Append(LaneNames[i]).Append("</text>");
            }

            // Time gridlines
            var tick = ctx.Options.TimeAxisTickSeconds;
            for (var t = Math.Ceiling(ctx.StartTime / tick) * tick; t <= ctx.EndTime; t += tick)
            {
                var y = ctx.Y(t);
                sb.Append("<line x1=\"0\" x2=\"").Append(w)
                  .Append("\" y1=\"").Append(y.ToString("0.0", CultureInfo.InvariantCulture))
                  .Append("\" y2=\"").Append(y.ToString("0.0", CultureInfo.InvariantCulture))
                  .Append("\" class=\"gridline\"/>");
            }

            // Sustains drawn first (under the note heads). A sustain extends
            // upward in screen space because later-in-time = higher up.
            const int sustainWidthPx = 6;
            foreach (var note in chart.Notes)
            {
                if (note.SustainSeconds <= 0) continue;
                var yBottom = ctx.Y(note.TimeSeconds);
                var yTop = ctx.Y(note.TimeSeconds + note.SustainSeconds);
                if (note.IsOpen)
                {
                    sb.Append("<rect x=\"").Append(w / 2 - sustainWidthPx / 2)
                      .Append("\" y=\"").Append(yTop.ToString("0.0", CultureInfo.InvariantCulture))
                      .Append("\" width=\"").Append(sustainWidthPx)
                      .Append("\" height=\"").Append((yBottom - yTop).ToString("0.0", CultureInfo.InvariantCulture))
                      .Append("\" fill=\"").Append(OpenColor).Append("\" opacity=\"0.7\"/>");
                    continue;
                }
                for (var li = 0; li < LanesLeftToRight.Length; li++)
                {
                    if ((note.Frets & LanesLeftToRight[li]) == 0) continue;
                    var laneCenterX = li * lane + lane / 2;
                    sb.Append("<rect x=\"").Append(laneCenterX - sustainWidthPx / 2)
                      .Append("\" y=\"").Append(yTop.ToString("0.0", CultureInfo.InvariantCulture))
                      .Append("\" width=\"").Append(sustainWidthPx)
                      .Append("\" height=\"").Append((yBottom - yTop).ToString("0.0", CultureInfo.InvariantCulture))
                      .Append("\" fill=\"").Append(LaneColors[li]).Append("\" opacity=\"0.7\"/>");
                }
            }

            // Note heads
            foreach (var note in chart.Notes)
            {
                var y = ctx.Y(note.TimeSeconds);
                if (note.IsOpen)
                {
                    sb.Append("<rect x=\"6\" y=\"").Append((y - 6).ToString("0.0", CultureInfo.InvariantCulture))
                      .Append("\" width=\"").Append(w - 12)
                      .Append("\" height=\"12\" rx=\"3\" ry=\"3\" fill=\"").Append(OpenColor).Append("\"")
                      .Append(NoteStyleAttrs(note.Type)).Append("/>");
                    continue;
                }
                for (var li = 0; li < LanesLeftToRight.Length; li++)
                {
                    if ((note.Frets & LanesLeftToRight[li]) == 0) continue;
                    var cx = li * lane + lane / 2;
                    sb.Append("<circle cx=\"").Append(cx)
                      .Append("\" cy=\"").Append(y.ToString("0.0", CultureInfo.InvariantCulture))
                      .Append("\" r=\"").Append(ctx.Options.NoteRadiusPx)
                      .Append("\" fill=\"").Append(LaneColors[li]).Append("\"")
                      .Append(NoteStyleAttrs(note.Type)).Append("/>");
                }
            }

            sb.Append("</svg>");
        }

        private static string NoteStyleAttrs(NoteType type)
        {
            switch (type)
            {
                case NoteType.Hopo:
                    return " stroke=\"#FFFFFF\" stroke-width=\"2\"";
                case NoteType.Tap:
                    return " stroke=\"#FFFFFF\" stroke-width=\"2\" fill-opacity=\"0.25\"";
                default:
                    return string.Empty;
            }
        }

        // ---- Pattern panel (per-kind columns with labeled bars) -------------

        private static void AppendPatterns(StringBuilder sb, Chart chart, DifficultyReport report, RenderContext ctx)
        {
            var w = ctx.PatternsWidthPx;
            var col = ctx.Options.PatternColumnWidthPx;
            sb.Append("<svg class=\"patterns\" width=\"").Append(w)
              .Append("\" height=\"").Append(ctx.TotalHeightPx).Append("\">");

            // Column headers + dividers
            for (var i = 0; i < PatternColumns.Length; i++)
            {
                var kind = PatternColumns[i];
                var x = i * col;
                if (i > 0)
                {
                    sb.Append("<line x1=\"").Append(x).Append("\" x2=\"").Append(x)
                      .Append("\" y1=\"0\" y2=\"").Append(ctx.TotalHeightPx)
                      .Append("\" class=\"col-divider\"/>");
                }
                // Header text — short name written vertically at the top.
                sb.Append("<text x=\"").Append(x + col / 2)
                  .Append("\" y=\"16\" class=\"col-header\" style=\"fill:")
                  .Append(PatternColors[kind]).Append("\">")
                  .Append(ShortName(kind)).Append("</text>");
            }

            // Match bars: one per detected pattern, in its kind's column.
            // The pattern name is rotated 90° and drawn near the bottom of the
            // bar (start of the match) so it reads naturally.
            if (report.Patterns != null)
            {
                foreach (var match in report.Patterns.Matches)
                {
                    var col_idx = IndexOfPatternKind(match.Kind);
                    if (col_idx < 0) continue;
                    if (match.StartIndex < 0 || match.EndIndex >= chart.Notes.Count) continue;

                    var x = col_idx * col;
                    var yBottom = ctx.Y(chart.Notes[match.StartIndex].TimeSeconds);
                    var yTop = ctx.Y(chart.Notes[match.EndIndex].TimeSeconds);
                    var height = Math.Max(2, yBottom - yTop);

                    sb.Append("<rect x=\"").Append(x + 3)
                      .Append("\" y=\"").Append(yTop.ToString("0.0", CultureInfo.InvariantCulture))
                      .Append("\" width=\"").Append(col - 6)
                      .Append("\" height=\"").Append(height.ToString("0.0", CultureInfo.InvariantCulture))
                      .Append("\" fill=\"").Append(PatternColors[match.Kind])
                      .Append("\" opacity=\"0.85\" rx=\"2\" ry=\"2\"/>");

                    // Rotated label inside the bar (only if the bar is tall enough).
                    if (height >= 28)
                    {
                        var labelX = x + col / 2;
                        var labelY = yBottom - 6;
                        sb.Append("<text x=\"").Append(labelX)
                          .Append("\" y=\"").Append(labelY.ToString("0.0", CultureInfo.InvariantCulture))
                          .Append("\" class=\"pattern-label\" transform=\"rotate(-90 ")
                          .Append(labelX).Append(',').Append(labelY.ToString("0.0", CultureInfo.InvariantCulture))
                          .Append(")\">")
                          .Append(match.Kind.ToString()).Append("</text>");
                    }
                }
            }

            sb.Append("</svg>");
        }

        private static string ShortName(PatternKind kind)
        {
            switch (kind)
            {
                case PatternKind.Trill: return "Tr";
                case PatternKind.Zig: return "Zg";
                case PatternKind.Ladder: return "Ld";
                case PatternKind.Chimney: return "Ch";
                case PatternKind.ReverseChimney: return "rC";
                case PatternKind.Triplet: return "T3";
                case PatternKind.Quad: return "Q4";
                case PatternKind.Quint: return "Q5";
                default: return "?";
            }
        }

        private static int IndexOfPatternKind(PatternKind kind)
        {
            for (var i = 0; i < PatternColumns.Length; i++)
            {
                if (PatternColumns[i] == kind) return i;
            }
            return -1;
        }

        // ---- Bar/composite curves (vertical, x = value, y = time) -----------

        private static void AppendBarCurve(
            StringBuilder sb, string label, Core.Aggregation.DifficultyCurve curve,
            string color, RenderContext ctx, int width)
        {
            var values = curve.Values;
            var max = 0.0;
            var sum = 0.0;
            var peakIndex = -1;
            for (var i = 0; i < values.Length; i++)
            {
                var v = values[i];
                sum += v;
                if (v > max)
                {
                    max = v;
                    peakIndex = i;
                }
            }
            var mean = values.Length > 0 ? sum / values.Length : 0.0;
            var displayMax = max > 0 ? max : 1.0;

            sb.Append("<svg class=\"curve\" width=\"").Append(width)
              .Append("\" height=\"").Append(ctx.TotalHeightPx).Append("\">");

            // Header (top of column): name + mean + max.
            sb.Append("<text x=\"").Append(width / 2)
              .Append("\" y=\"14\" class=\"col-header\" style=\"fill:")
              .Append(color).Append("\">").Append(label).Append("</text>");
            sb.Append("<text x=\"").Append(width / 2)
              .Append("\" y=\"28\" class=\"col-stat\">")
              .Append("μ ").Append(mean.ToString("0.00", CultureInfo.InvariantCulture))
              .Append("  ↑").Append(max.ToString("0.00", CultureInfo.InvariantCulture))
              .Append("</text>");

            // Value-axis reference rules (vertical) at 25/50/75/100% of max,
            // with numeric labels along the top of the body so users can read
            // a sample's magnitude from its x-position.
            var bodyTop = ctx.Options.ColumnHeaderHeightPx;
            if (max > 0)
            {
                var fractions = new[] { 0.25, 0.50, 0.75, 1.0 };
                foreach (var frac in fractions)
                {
                    var x = frac * (width - 2);
                    sb.Append("<line x1=\"").Append(x.ToString("0.0", CultureInfo.InvariantCulture))
                      .Append("\" x2=\"").Append(x.ToString("0.0", CultureInfo.InvariantCulture))
                      .Append("\" y1=\"").Append(bodyTop)
                      .Append("\" y2=\"").Append(ctx.TotalHeightPx)
                      .Append("\" class=\"value-rule\"/>");
                    var labelValue = frac * max;
                    var labelX = frac < 1.0
                        ? x + 2
                        : x - 2; // last label hugs the right edge
                    var anchor = frac < 1.0 ? "start" : "end";
                    sb.Append("<text x=\"").Append(labelX.ToString("0.0", CultureInfo.InvariantCulture))
                      .Append("\" y=\"").Append(bodyTop - 2)
                      .Append("\" class=\"value-tick\" style=\"text-anchor:").Append(anchor).Append("\">")
                      .Append(labelValue.ToString("0.0", CultureInfo.InvariantCulture))
                      .Append("</text>");
                }
            }

            // Time gridlines + per-gridline numeric value annotations.
            // Showing values at each time tick lets the user scroll the track
            // and read the magnitude of each axis at a glance — no hover.
            var tick = ctx.Options.TimeAxisTickSeconds;
            var gridSampleRate = curve.SampleRateHz > 0 ? curve.SampleRateHz : 10.0;
            var gridCurveStart = curve.StartTimeSeconds;
            for (var t = Math.Ceiling(ctx.StartTime / tick) * tick; t <= ctx.EndTime; t += tick)
            {
                var y = ctx.Y(t);
                sb.Append("<line x1=\"0\" x2=\"").Append(width)
                  .Append("\" y1=\"").Append(y.ToString("0.0", CultureInfo.InvariantCulture))
                  .Append("\" y2=\"").Append(y.ToString("0.0", CultureInfo.InvariantCulture))
                  .Append("\" class=\"gridline\"/>");

                if (values.Length > 0)
                {
                    var sampleIndex = (int)Math.Round((t - gridCurveStart) * gridSampleRate);
                    if (sampleIndex >= 0 && sampleIndex < values.Length)
                    {
                        var v = values[sampleIndex];
                        sb.Append("<text x=\"").Append(width - 4)
                          .Append("\" y=\"").Append((y - 2).ToString("0.0", CultureInfo.InvariantCulture))
                          .Append("\" class=\"grid-value\" style=\"fill:").Append(color).Append("\">")
                          .Append(v.ToString("0.0", CultureInfo.InvariantCulture))
                          .Append("</text>");
                    }
                }
            }

            if (values.Length > 0)
            {
                var sampleRate = curve.SampleRateHz > 0 ? curve.SampleRateHz : 10.0;
                var curveStart = curve.StartTimeSeconds;

                // Build the curve as a polyline in (x, y) where x ∈ [0, width-2]
                // is value-scaled and y is time-positioned. Filled area to the
                // left, stroked line on top.

                // Filled area: start at (0, yTop), trace down through samples,
                // close at (0, yBottom).
                sb.Append("<path d=\"");
                var firstY = ctx.Y(curveStart + (values.Length - 1) / sampleRate);
                sb.Append("M0,").Append(firstY.ToString("0.0", CultureInfo.InvariantCulture));
                for (var i = values.Length - 1; i >= 0; i--)
                {
                    var x = (values[i] / displayMax) * (width - 2);
                    var y = ctx.Y(curveStart + i / sampleRate);
                    sb.Append(" L").Append(x.ToString("0.0", CultureInfo.InvariantCulture))
                      .Append(',').Append(y.ToString("0.0", CultureInfo.InvariantCulture));
                }
                var lastY = ctx.Y(curveStart);
                sb.Append(" L0,").Append(lastY.ToString("0.0", CultureInfo.InvariantCulture));
                sb.Append(" Z\" fill=\"").Append(color).Append("\" fill-opacity=\"0.18\"/>");

                // Stroke
                sb.Append("<path d=\"");
                for (var i = values.Length - 1; i >= 0; i--)
                {
                    var x = (values[i] / displayMax) * (width - 2);
                    var y = ctx.Y(curveStart + i / sampleRate);
                    sb.Append(i == values.Length - 1 ? "M" : " L")
                      .Append(x.ToString("0.0", CultureInfo.InvariantCulture))
                      .Append(',').Append(y.ToString("0.0", CultureInfo.InvariantCulture));
                }
                sb.Append("\" stroke=\"").Append(color).Append("\" stroke-width=\"1.5\" fill=\"none\"/>");

                // Per-sample hover targets — one transparent horizontal strip
                // per sample so the browser shows a native tooltip with the
                // exact (time, value) on mouseover. Cheap, no JS required.
                var stripHeight = Math.Max(1.0, ctx.Options.PixelsPerSecond / sampleRate);
                for (var i = 0; i < values.Length; i++)
                {
                    var sampleTime = curveStart + i / sampleRate;
                    var y = ctx.Y(sampleTime);
                    sb.Append("<rect x=\"0\" y=\"")
                      .Append((y - stripHeight / 2).ToString("0.0", CultureInfo.InvariantCulture))
                      .Append("\" width=\"").Append(width)
                      .Append("\" height=\"").Append(stripHeight.ToString("0.0", CultureInfo.InvariantCulture))
                      .Append("\" fill=\"transparent\"><title>")
                      .Append(label).Append(' ')
                      .Append(values[i].ToString("0.000", CultureInfo.InvariantCulture))
                      .Append(" @ ")
                      .Append(sampleTime.ToString("0.00", CultureInfo.InvariantCulture))
                      .Append("s</title></rect>");
                }

                // Peak annotation: small dot + numeric label at the highest sample.
                if (peakIndex >= 0 && max > 0)
                {
                    var px = (values[peakIndex] / displayMax) * (width - 2);
                    var py = ctx.Y(curveStart + peakIndex / sampleRate);
                    sb.Append("<circle cx=\"").Append(px.ToString("0.0", CultureInfo.InvariantCulture))
                      .Append("\" cy=\"").Append(py.ToString("0.0", CultureInfo.InvariantCulture))
                      .Append("\" r=\"2.5\" fill=\"").Append(color).Append("\"/>");
                    // Place the label to the left of the peak so it doesn't
                    // overflow the column when the peak hugs the right edge.
                    var labelX = Math.Max(2.0, px - 4);
                    sb.Append("<text x=\"").Append(labelX.ToString("0.0", CultureInfo.InvariantCulture))
                      .Append("\" y=\"").Append((py - 4).ToString("0.0", CultureInfo.InvariantCulture))
                      .Append("\" class=\"peak-label\" style=\"fill:").Append(color).Append("\">")
                      .Append(max.ToString("0.00", CultureInfo.InvariantCulture))
                      .Append("</text>");
                }
            }

            sb.Append("</svg>");
        }

        // ---- Helpers --------------------------------------------------------

        private static string MetadataOrEmpty(Chart chart, string key)
        {
            return chart.Metadata != null && chart.Metadata.TryGetValue(key, out var v) ? v : string.Empty;
        }

        private static string SongTitle(Chart chart)
        {
            var name = MetadataOrEmpty(chart, "Name");
            return string.IsNullOrWhiteSpace(name) ? "(untitled chart)" : name;
        }

        private static string HtmlEscape(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;");
        }

        private sealed class RenderContext
        {
            public VisualizerOptions Options;
            public double StartTime;
            public double EndTime;
            public double TotalSeconds;

            /// <summary>Pixel height of the actual content (notes, sustains, curves) without the column-header band.</summary>
            public int BodyHeightPx;

            /// <summary>Pixel height of the full SVG including the column-header band at the top.</summary>
            public int TotalHeightPx;

            public int HighwayWidthPx;
            public int PatternsWidthPx;

            /// <summary>
            /// Map a song time (seconds) to a y coordinate. The column-header
            /// band occupies y ∈ [0, ColumnHeaderHeightPx]; below that, time
            /// runs from <c>EndTime</c> at the top of the body to <c>StartTime</c>
            /// at the bottom (so bottom = start of song).
            /// </summary>
            public double Y(double timeSeconds)
            {
                return Options.ColumnHeaderHeightPx + (EndTime - timeSeconds) * Options.PixelsPerSecond;
            }
        }

        // ---- CSS ------------------------------------------------------------

        private const string Css = @"
* { box-sizing: border-box; }
body { margin: 0; background: #0F0F14; color: #E0E0E5;
       font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; font-size: 13px; }
header { padding: 16px 20px; border-bottom: 1px solid #25252A; position: sticky; top: 0;
         background: #0F0F14; z-index: 10; }
header h1 { margin: 0; font-size: 18px; font-weight: 600; }
header .sub { color: #8A8A92; margin-top: 2px; }
.stats { margin-top: 10px; display: flex; flex-wrap: wrap; gap: 16px; }
.stat .label { color: #8A8A92; margin-right: 6px; }
.stat .value { font-weight: 600; color: #FFD566; }
.legend { padding: 8px 20px; display: flex; flex-wrap: wrap; gap: 14px;
          border-bottom: 1px solid #25252A; position: sticky; top: 110px; background: #0F0F14; z-index: 9; }
.legend-item { color: #C0C0C8; display: inline-flex; align-items: center; }
.legend-item .swatch { display: inline-block; width: 12px; height: 12px;
                       border-radius: 2px; margin-right: 6px; }
.legend-item .count { color: #8A8A92; margin-left: 4px; }

/* Vertical visualization: side-by-side columns (axis | highway | patterns | curves) */
.viz { display: flex; align-items: flex-start; padding: 12px 20px 24px; gap: 0; }
.viz svg { display: block; flex-shrink: 0; }
.axis { background: #14141A; border-right: 1px solid #25252A; }
.highway { background: #16161D; border-right: 1px solid #25252A; }
.patterns { background: #14141A; border-right: 1px solid #25252A; }
.curve { background: #12121A; border-right: 1px solid #25252A; }

.lane { fill: #16161D; stroke: #25252A; stroke-width: 0.5; }
.gridline { stroke: #20202A; stroke-width: 0.5; }
.col-divider { stroke: #25252A; stroke-width: 0.5; }
.tick { stroke: #4A4A55; }
.tick-label { fill: #8A8A92; font-size: 10px; }
.col-header { fill: #C0C0C8; font-size: 11px; font-weight: 600; text-anchor: middle; }
.lane-header { font-size: 13px; }
.col-header .max { fill: #6A6A72; font-weight: normal; font-size: 10px; }
.col-stat { fill: #8A8A92; font-size: 10px; text-anchor: middle; }
.value-rule { stroke: #20202A; stroke-width: 0.5; stroke-dasharray: 2 3; }
.value-tick { fill: #6A6A72; font-size: 9px; }
.grid-value { font-size: 10px; font-weight: 600; text-anchor: end; paint-order: stroke;
              stroke: #0F0F14; stroke-width: 3; stroke-linejoin: round; opacity: 0.92; }
.peak-label { font-size: 10px; font-weight: 600; text-anchor: end; paint-order: stroke;
              stroke: #0F0F14; stroke-width: 3; stroke-linejoin: round; }
.pattern-label { fill: #0F0F14; font-size: 10px; font-weight: 600; text-anchor: end; }
";
    }
}
