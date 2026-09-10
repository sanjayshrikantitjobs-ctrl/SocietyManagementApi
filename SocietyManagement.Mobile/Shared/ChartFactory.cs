using Microcharts;
using SkiaSharp;

namespace SocietyManagement.Mobile.Shared;

/// <summary>Builds Microcharts.Maui charts from backend DTOs, shared by the
/// Home dashboard and the Festival Dashboard tab so both use the same
/// palette/label styling instead of each page hand-rolling its own. Kept
/// deliberately simple (single-series bar/donut) since Microcharts has no
/// grouped/stacked series concept — a two-metric comparison (e.g. Approved
/// vs Actual) is built as adjacent same-group bars with distinct colors
/// rather than a true grouped-bar chart.
///
/// Values are plain `double` (not `decimal`) because every numeric DTO
/// field here comes back from the NSwag-generated client as `double?` —
/// Swashbuckle documents C# `decimal` as an untyped OpenAPI "number", which
/// NSwag maps to `double` by default — so callers pass `?? 0` at the
/// binding site rather than this factory silently doing narrowing
/// conversions.</summary>
public static class ChartFactory
{
    public const string ColorInfo = "#4F6EF7";
    public const string ColorSuccess = "#16A34A";
    public const string ColorWarning = "#D97706";
    public const string ColorDanger = "#DC2626";
    public const string ColorPurple = "#7D00FA";
    public const string ColorTeal = "#0D9488";
    public const string ColorMuted = "#9CA3AF";
    private const string LabelColorHex = "#6B7280";

    private static readonly string[] Palette =
    {
        ColorInfo, ColorPurple, ColorTeal, ColorWarning, ColorDanger, ColorSuccess, "#EC4899", "#0EA5E9"
    };

    private static void ApplyCommon(Chart chart)
    {
        chart.BackgroundColor = SKColors.Transparent;
        chart.LabelTextSize = 26;
        chart.LabelColor = SKColor.Parse(LabelColorHex);
        chart.Margin = 12;
    }

    /// <summary>A two-slice donut for a simple proportion — e.g. Occupied vs
    /// Vacant, or Collected vs Remaining.</summary>
    public static DonutChart BuildProportionDonut(string labelA, double valueA, string colorA, string labelB, double valueB, string colorB)
    {
        var entries = new[]
        {
            new ChartEntry((float)Math.Max(valueA, 0)) { Label = labelA, ValueLabel = FormatCount(valueA), Color = SKColor.Parse(colorA) },
            new ChartEntry((float)Math.Max(valueB, 0)) { Label = labelB, ValueLabel = FormatCount(valueB), Color = SKColor.Parse(colorB) },
        };
        var chart = new DonutChart { Entries = entries, HoleRadius = 0.6f };
        ApplyCommon(chart);
        return chart;
    }

    /// <summary>A donut with one slice per named category, colors assigned
    /// round-robin from the shared palette — e.g. Expense by Category.</summary>
    public static DonutChart BuildCategoryDonut(IReadOnlyList<(string? Label, double Value)> points)
    {
        var entries = points.Select((p, i) => new ChartEntry((float)Math.Max(p.Value, 0))
        {
            Label = p.Label ?? "—",
            ValueLabel = FormatCurrency(p.Value),
            Color = SKColor.Parse(Palette[i % Palette.Length])
        }).ToArray();
        var chart = new DonutChart { Entries = entries.Length > 0 ? entries : PlaceholderEntries(), HoleRadius = 0.6f };
        ApplyCommon(chart);
        return chart;
    }

    /// <summary>Single-series bar chart, one bar per named point, colors
    /// round-robin from the shared palette — e.g. Sponsor promised amounts.</summary>
    public static BarChart BuildCategoryBar(IReadOnlyList<(string? Label, double Value)> points)
    {
        var entries = points.Select((p, i) => new ChartEntry((float)Math.Max(p.Value, 0))
        {
            Label = p.Label ?? "—",
            ValueLabel = FormatCurrency(p.Value),
            Color = SKColor.Parse(Palette[i % Palette.Length])
        }).ToArray();
        var chart = new BarChart { Entries = entries.Length > 0 ? entries : PlaceholderEntries() };
        ApplyCommon(chart);
        return chart;
    }

    /// <summary>Single-series line/area chart over an ordered sequence (e.g.
    /// months) — Microcharts draws exactly one connected line through
    /// whatever entries it's given, so a true two-line comparison isn't
    /// possible here; use <see cref="BuildPairedBar"/> for that instead.</summary>
    public static LineChart BuildTrendLine(IReadOnlyList<(string? Label, double Value)> points, string color)
    {
        var entries = points.Select(p => new ChartEntry((float)Math.Max(p.Value, 0))
        {
            Label = p.Label ?? "—",
            ValueLabel = FormatCurrency(p.Value),
            Color = SKColor.Parse(color)
        }).ToArray();
        var chart = new LineChart
        {
            Entries = entries.Length > 0 ? entries : PlaceholderEntries(),
            LineMode = LineMode.Spline,
            LineSize = 4,
            PointMode = PointMode.Circle,
            PointSize = 8,
            LineAreaAlpha = 40
        };
        ApplyCommon(chart);
        return chart;
    }

    /// <summary>Paired bar chart comparing two metrics per group (e.g. month,
    /// or budget category) — adjacent bars colored by metric, not a true
    /// grouped/stacked series (Microcharts has neither).</summary>
    public static BarChart BuildPairedBar(IReadOnlyList<(string? GroupLabel, double ValueA, double ValueB)> points, string colorA, string colorB)
    {
        var entries = new List<ChartEntry>();
        foreach (var p in points)
        {
            entries.Add(new ChartEntry((float)Math.Max(p.ValueA, 0)) { Label = p.GroupLabel ?? "—", ValueLabel = FormatCurrency(p.ValueA), Color = SKColor.Parse(colorA) });
            entries.Add(new ChartEntry((float)Math.Max(p.ValueB, 0)) { Label = "", ValueLabel = FormatCurrency(p.ValueB), Color = SKColor.Parse(colorB) });
        }
        var chart = new BarChart { Entries = entries.Count > 0 ? entries : PlaceholderEntries() };
        ApplyCommon(chart);
        return chart;
    }

    private static ChartEntry[] PlaceholderEntries() =>
        new[] { new ChartEntry(0) { Label = "No data", Color = SKColor.Parse(ColorMuted) } };

    private static string FormatCurrency(double value) => value >= 1000 ? $"₹{value / 1000:0.#}k" : $"₹{value:0}";
    private static string FormatCount(double value) => value.ToString("0");
}
