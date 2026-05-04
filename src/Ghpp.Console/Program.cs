using AsciiChart.Sharp;
using Ghpp.Core;
using Ghpp.Core.Abstractions;
using Ghpp.Core.Abstractions.Patterns;
using Ghpp.Core.Models;
using Ghpp.Parsers.ChartFile;
using Ghpp.Visualization;
using Spectre.Console;
using AsciiPlot = AsciiChart.Sharp.AsciiChart;

if (args.Length == 0)
{
    AnsiConsole.MarkupLine("[red]usage:[/] ghpp [[--html]] [[--scale <px/sec>]] [[--lane-width <px>]] <chart-file> [[<chart-file> ...]]");
    AnsiConsole.MarkupLine("[dim]       --html                write a visualizer HTML alongside each chart[/]");
    AnsiConsole.MarkupLine("[dim]       --scale <px/sec>      note distance scale, default 60 (higher = taller)[/]");
    AnsiConsole.MarkupLine("[dim]       --lane-width <px>     fret lane width, default 56 (higher = wider track)[/]");
    AnsiConsole.MarkupLine("[dim]       (only .chart files are supported for now)[/]");
    return 1;
}

var emitHtml = false;
double? scaleOverride = null;
int? laneWidthOverride = null;
var pathArgs = new List<string>();
for (var i = 0; i < args.Length; i++)
{
    var a = args[i];
    if (a == "--html")
    {
        emitHtml = true;
    }
    else if (a == "--scale" && i + 1 < args.Length)
    {
        if (!double.TryParse(args[++i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var px) || px <= 0)
        {
            AnsiConsole.MarkupLine("[red]error:[/] --scale expects a positive number (px/sec)");
            return 1;
        }
        scaleOverride = px;
    }
    else if (a == "--lane-width" && i + 1 < args.Length)
    {
        if (!int.TryParse(args[++i], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var w) || w < 16)
        {
            AnsiConsole.MarkupLine("[red]error:[/] --lane-width expects an integer ≥ 16");
            return 1;
        }
        laneWidthOverride = w;
    }
    else
    {
        pathArgs.Add(a);
    }
}

var parser = new ChartFileParser();
var failures = 0;

foreach (var path in pathArgs)
{
    if (!File.Exists(path))
    {
        AnsiConsole.MarkupLine($"[red]error:[/] file not found: [yellow]{Markup.Escape(path)}[/]");
        failures++;
        continue;
    }
    if (!string.Equals(Path.GetExtension(path), ".chart", StringComparison.OrdinalIgnoreCase))
    {
        AnsiConsole.MarkupLine($"[red]error:[/] unsupported file type: [yellow]{Markup.Escape(path)}[/]");
        failures++;
        continue;
    }

    try
    {
        var chart = parser.Parse(path);
        var report = DifficultyCalculator.Calculate(chart);
        PrintReport(path, chart, report);
        if (emitHtml)
        {
            var vizOptions = new VisualizerOptions();
            if (scaleOverride.HasValue) vizOptions.PixelsPerSecond = scaleOverride.Value;
            if (laneWidthOverride.HasValue)
            {
                vizOptions.LaneWidthPx = laneWidthOverride.Value;
                // Auto-scale note radius to match: ~32% of lane width keeps
                // note heads visually proportional regardless of width choice.
                vizOptions.NoteRadiusPx = Math.Max(6, (int)(laneWidthOverride.Value * 0.32));
            }
            var html = HtmlVisualizer.Render(chart, report, vizOptions);
            var outPath = Path.ChangeExtension(path, ".ghpp.html");
            File.WriteAllText(outPath, html);
            AnsiConsole.MarkupLine($"[dim]wrote[/] [yellow]{Markup.Escape(outPath)}[/]");
        }
    }
    catch (Exception exception)
    {
        AnsiConsole.MarkupLine(
            $"[red]error:[/] failed to process [yellow]{Markup.Escape(path)}[/]: {Markup.Escape(exception.Message)}");
        failures++;
    }
}

return failures == 0 ? 0 : 1;

static void PrintReport(string path, Chart chart, DifficultyReport report)
{
    AnsiConsole.WriteLine();
    AnsiConsole.Write(new Rule($"[yellow]{Markup.Escape(path)}[/]").LeftJustified());

    PrintMetadata(chart);
    PrintNoteBreakdown(report);
    PrintBarCurves(report);
    PrintPatterns(report);
    PrintCompositeAndPercentiles(report);
    PrintStarRating(report);
    AnsiConsole.WriteLine();
}

static void PrintMetadata(Chart chart)
{
    var keys = new[] { "Name", "Artist", "Charter", "Year", "Genre" };
    var grid = new Grid()
        .AddColumn(new GridColumn().NoWrap().PadRight(2))
        .AddColumn();
    var rows = 0;
    foreach (var key in keys)
    {
        if (!chart.Metadata.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            continue;
        grid.AddRow($"[dim]{key}[/]", Markup.Escape(value));
        rows++;
    }
    if (rows > 0)
    {
        AnsiConsole.Write(grid);
    }
}

static void PrintNoteBreakdown(DifficultyReport report)
{
    AnsiConsole.WriteLine();
    if (report.TotalNotes > 0)
    {
        var breakdown = new BreakdownChart()
            .Width(60)
            .AddItem("Strum", report.StrumCount, Color.Green)
            .AddItem("HOPO", report.HopoCount, Color.SkyBlue1)
            .AddItem("Tap", report.TapCount, Color.Yellow);
        AnsiConsole.Write(breakdown);
    }
    AnsiConsole.MarkupLine(
        $"[dim]Total[/] {report.TotalNotes}    " +
        $"[dim]Fret-lane[/] {report.FretLaneNotes}    " +
        $"[dim]Duration[/] {report.DurationSeconds:0.00}s");
    AnsiConsole.MarkupLine(
        $"[dim]CBar[/] {report.CBarMean:0.000}    " +
        $"[dim]SBar[/] {report.SBarMean:0.000}    " +
        $"[dim]LBar[/] {report.LBarMean:0.000}");
}

static void PrintBarCurves(DifficultyReport report)
{
    AnsiConsole.WriteLine();
    AnsiConsole.Write(new Rule("[grey]Bar curves[/]").LeftJustified().RuleStyle("grey"));
    PrintMiniCurve("CBar", report.CBarCurve.Values, "skyblue1");
    PrintMiniCurve("SBar", report.SBarCurve.Values, "yellow");
    PrintMiniCurve("LBar", report.LBarCurve.Values, "mediumpurple");
}

static void PrintMiniCurve(string label, double[] values, string color)
{
    if (values.Length == 0) return;
    const int maxPlotWidth = 80;
    var data = DownsampleForWidth(values, maxPlotWidth);
    var plot = AsciiPlot.Plot(data, new Options
    {
        Height = 4,
        AxisLabelFormat = "0.0",
    });
    AnsiConsole.MarkupLine($"[{color}]{label}[/]");
    AnsiConsole.WriteLine(plot);
}

static void PrintCompositeAndPercentiles(DifficultyReport report)
{
    AnsiConsole.WriteLine();
    AnsiConsole.Write(new Rule("[grey]Composite curve[/]").LeftJustified().RuleStyle("grey"));
    AnsiConsole.MarkupLine(
        $"[dim]{report.Curve.Values.Length} samples @ {report.Curve.SampleRateHz} Hz[/]    " +
        $"[dim]mean[/] {report.MeanNps:0.000}    " +
        $"[dim]max[/] {report.MaxNps:0.000}");

    if (report.Curve.Values.Length > 0)
    {
        const int maxPlotWidth = 80;
        var data = DownsampleForWidth(report.Curve.Values, maxPlotWidth);
        var plot = AsciiPlot.Plot(data, new Options
        {
            Height = 8,
            AxisLabelFormat = "0.0",
        });
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine(plot);
    }

    var table = new Table().Border(TableBorder.Rounded).HideHeaders();
    table.AddColumn(new TableColumn(string.Empty));
    table.AddColumn(new TableColumn(string.Empty).RightAligned());
    table.AddRow("P83", $"{report.P83:0.000}");
    table.AddRow("P93", $"{report.P93:0.000}");
    table.AddRow("P99", $"{report.P99:0.000}");
    table.AddRow("L5 [dim](top 5%)[/]", $"{report.L5:0.000}");
    table.AddRow("[bold]Blended[/]", $"[bold]{report.Blended:0.000}[/]");
    AnsiConsole.Write(table);
}

static void PrintStarRating(DifficultyReport report)
{
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine(
        $"[dim]Intensity[/] {report.IntensityStars:0.00}    " +
        $"[dim]+ length bonus[/] {report.LengthBonus:0.00}");
    var color = StarColor(report.StarRating);
    AnsiConsole.MarkupLine($"[bold {color}]★ {report.StarRating:0.00} stars[/]");
}

static string StarColor(double stars)
{
    return stars switch
    {
        < 3 => "green",
        < 6 => "yellow",
        < 10 => "orange1",
        < 15 => "red",
        _ => "mediumpurple"
    };
}

static void PrintPatterns(DifficultyReport report)
{
    if (report.Patterns == null) return;
    AnsiConsole.WriteLine();
    AnsiConsole.Write(new Rule("[grey]Detected patterns[/]").LeftJustified().RuleStyle("grey"));

    var counts = new Dictionary<PatternKind, int>();
    var totalLength = new Dictionary<PatternKind, int>();
    foreach (var match in report.Patterns.Matches)
    {
        counts[match.Kind] = counts.TryGetValue(match.Kind, out var c) ? c + 1 : 1;
        totalLength[match.Kind] = totalLength.TryGetValue(match.Kind, out var l) ? l + match.Length : match.Length;
    }
    if (counts.Count == 0)
    {
        AnsiConsole.MarkupLine("[dim](none detected)[/]");
        return;
    }
    var bar = new BarChart().Width(60);
    foreach (var kv in counts.OrderByDescending(p => p.Value))
    {
        bar.AddItem($"{kv.Key} ({totalLength[kv.Key]} notes)", kv.Value, ColorFor(kv.Key));
    }
    AnsiConsole.Write(bar);
}

static Color ColorFor(PatternKind kind)
{
    return kind switch
    {
        PatternKind.Trill => Color.Yellow,
        PatternKind.Zig => Color.SkyBlue1,
        PatternKind.Ladder => Color.Green,
        PatternKind.Chimney => Color.Orange1,
        PatternKind.ReverseChimney => Color.Red,
        PatternKind.Triplet => Color.MediumPurple,
        PatternKind.Quad => Color.Magenta1,
        PatternKind.Quint => Color.HotPink,
        _ => Color.Grey,
    };
}

static IList<double> DownsampleForWidth(double[] values, int targetWidth)
{
    if (values.Length <= targetWidth)
    {
        return values;
    }
    var result = new double[targetWidth];
    for (var col = 0; col < targetWidth; col++)
    {
        var start = (int)Math.Floor((double)col * values.Length / targetWidth);
        var end = (int)Math.Floor((double)(col + 1) * values.Length / targetWidth);
        if (end <= start)
        {
            end = start + 1;
        }
        if (end > values.Length)
        {
            end = values.Length;
        }
        var max = 0.0;
        for (var i = start; i < end; i++)
        {
            if (values[i] > max)
            {
                max = values[i];
            }
        }
        result[col] = max;
    }
    return result;
}
