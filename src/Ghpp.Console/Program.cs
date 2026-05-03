using AsciiChart.Sharp;
using Ghpp.Core;
using Ghpp.Core.Abstractions;
using Ghpp.Core.Models;
using Ghpp.Parsers.ChartFile;
using Spectre.Console;
using AsciiPlot = AsciiChart.Sharp.AsciiChart;

if (args.Length == 0)
{
    AnsiConsole.MarkupLine("[red]usage:[/] ghpp <chart-file> [[<chart-file> ...]]");
    AnsiConsole.MarkupLine("[dim]       (only .chart files are supported for now)[/]");
    return 1;
}

var parser = new ChartFileParser();
var failures = 0;

foreach (var path in args)
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
    PrintFretJumps(report);
    PrintCurveAndPercentiles(report);
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
        $"[dim]Mean rhythm mult[/] {report.MeanRhythmMultiplier:0.000}    " +
        $"[dim]Mean fret mult[/] {report.MeanFretMultiplier:0.000}");
}

static void PrintFretJumps(DifficultyReport report)
{
    if (report.FretJumpDistribution == null || report.FretJumpDistribution.Count == 0)
    {
        return;
    }
    AnsiConsole.WriteLine();
    AnsiConsole.Write(new Rule("[grey]Fret jumps[/]").LeftJustified().RuleStyle("grey"));

    var labels = new[] { "same fret", "adjacent", "one skip", "G-B / R-O", "G-O (full shift)" };
    var colors = new[] { Color.Green, Color.GreenYellow, Color.Yellow, Color.Orange1, Color.Red };
    var chart = new BarChart().Width(60);
    foreach (var distance in report.FretJumpDistribution.Keys.OrderBy(k => k))
    {
        var count = report.FretJumpDistribution[distance];
        var label = distance >= 0 && distance < labels.Length
            ? $"d={distance} ({labels[distance]})"
            : $"d={distance}";
        var color = distance >= 0 && distance < colors.Length ? colors[distance] : Color.Grey;
        chart.AddItem(label, count, color);
    }
    AnsiConsole.Write(chart);
}

static void PrintCurveAndPercentiles(DifficultyReport report)
{
    AnsiConsole.WriteLine();
    AnsiConsole.Write(new Rule("[grey]Difficulty curve[/]").LeftJustified().RuleStyle("grey"));
    AnsiConsole.MarkupLine(
        $"[dim]{report.Curve.Values.Length} samples @ {report.Curve.SampleRateHz} Hz[/]    " +
        $"[dim]mean NPS[/] {report.MeanNps:0.000}    " +
        $"[dim]max NPS[/] {report.MaxNps:0.000}");

    if (report.Curve.Values.Length > 0)
    {
        // AsciiChart's plot width equals the data length. Cap the width so
        // the chart fits in any reasonable terminal and stays stable across
        // terminal resizes.
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
