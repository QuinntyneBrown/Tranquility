using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Tranquility.AcceptanceTests.Traceability;
using Xunit;

namespace Tranquility.AcceptanceTests.Quality;

/// <summary>
/// L2-QLT-004 (declaration): the active baseline declares an explicit numeric
/// target for each required performance metric, and the benchmark thresholds
/// used for verification are the same numbers.
/// </summary>
[Requirement("L2-QLT-004")]
public sealed partial class PerfBaselineDeclarationTests
{
    private static readonly string[] RequiredMetricKeys =
    [
        "ingest-packets-per-second",
        "parameter-updates-per-second",
        "e2e-latency-p95-ms",
        "e2e-latency-p99-ms",
        "archive-write-mbps",
    ];

    private static string BaselineDocPath =>
        Path.Combine(RepoPaths.Root, "docs", "PERFORMANCE-BASELINE.md");

    private static string ThresholdsPath =>
        Path.Combine(RepoPaths.Root, "tests", "Tranquility.Benchmarks", "thresholds.json");

    [GeneratedRegex(@"^\|[^|]+\|\s*(?<key>[a-z0-9-]+)\s*\|\s*(?<target>[0-9]+(?:\.[0-9]+)?)\s*\|", RegexOptions.Multiline)]
    private static partial Regex MetricRow();

    [Fact]
    public void Baseline_declares_a_numeric_target_for_every_required_metric()
    {
        Assert.True(File.Exists(BaselineDocPath),
            $"Missing performance baseline declaration: {BaselineDocPath}");

        var declared = ParseDeclaredTargets();
        foreach (var key in RequiredMetricKeys)
        {
            Assert.True(declared.TryGetValue(key, out var target),
                $"PERFORMANCE-BASELINE.md declares no target for required metric '{key}'.");
            Assert.True(target > 0, $"Metric '{key}' must declare a positive numeric target.");
        }
    }

    [Fact]
    public void Benchmark_thresholds_match_the_declared_baseline()
    {
        Assert.True(File.Exists(BaselineDocPath),
            $"Missing performance baseline declaration: {BaselineDocPath}");
        Assert.True(File.Exists(ThresholdsPath),
            $"Missing benchmark thresholds file: {ThresholdsPath}");

        var declared = ParseDeclaredTargets();
        using var doc = JsonDocument.Parse(File.ReadAllText(ThresholdsPath));
        foreach (var key in RequiredMetricKeys)
        {
            Assert.True(doc.RootElement.TryGetProperty(key, out var value),
                $"thresholds.json is missing metric '{key}'.");
            Assert.True(declared.TryGetValue(key, out var target),
                $"PERFORMANCE-BASELINE.md declares no target for metric '{key}'.");
            Assert.Equal(target, value.GetDouble());
        }
    }

    private static Dictionary<string, double> ParseDeclaredTargets()
    {
        var text = File.ReadAllText(BaselineDocPath);
        return MetricRow().Matches(text).ToDictionary(
            m => m.Groups["key"].Value,
            m => double.Parse(m.Groups["target"].Value, CultureInfo.InvariantCulture),
            StringComparer.Ordinal);
    }
}
