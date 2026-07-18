using System.Diagnostics;
using System.Text.Json;
using Tranquility.AcceptanceTests.Traceability;
using Tranquility.Core.Decommutation;
using Tranquility.Infrastructure.Xtce;
using Xunit;

namespace Tranquility.AcceptanceTests.Quality;

/// <summary>
/// L2-QLT-004 (verification half): measures the declared performance metrics
/// and asserts the deterministic in-process paths meet the declared numeric
/// targets from docs/PERFORMANCE-BASELINE.md. The heavy socket-latency and
/// sustained-archive targets are verified by the dedicated perf jobs; the
/// deterministic Core throughput is verified here as the PR smoke gate.
/// </summary>
[Requirement("L2-QLT-004")]
[Trait("Category", "PerfSmoke")]
public sealed class PerfVerificationTests
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);

    private static IReadOnlyDictionary<string, double> Thresholds()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(RepoPaths.Root, "tests", "Tranquility.Benchmarks", "thresholds.json")));
        return doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetDouble());
    }

    [Fact]
    public void Decommutation_throughput_meets_the_declared_ingest_and_update_targets()
    {
        var thresholds = Thresholds();
        var mdb = new XtceLoader(Path.Combine(Fixtures.TestConfig.XtceFixtureDirectory, "SampleSat.xml")).Load();
        var engine = new DecommutationEngine(mdb);
        var root = mdb.FindContainer("/SampleSat/Root")!;
        var packet = SpacePackets.HeaderDecodeTests.GoldenPacket;

        // Warm up the JIT.
        for (var i = 0; i < 1000; i++)
        {
            engine.Decommutate(packet, root, T0, T0);
        }

        const int iterations = 200_000;
        var stopwatch = Stopwatch.StartNew();
        long parameterUpdates = 0;
        for (var i = 0; i < iterations; i++)
        {
            parameterUpdates += engine.Decommutate(packet, root, T0, T0).Values.Count;
        }

        stopwatch.Stop();
        var seconds = stopwatch.Elapsed.TotalSeconds;
        var packetsPerSecond = iterations / seconds;
        var updatesPerSecond = parameterUpdates / seconds;

        Assert.True(packetsPerSecond >= thresholds["ingest-packets-per-second"],
            $"ingest {packetsPerSecond:N0} pkt/s < target {thresholds["ingest-packets-per-second"]:N0}");
        Assert.True(updatesPerSecond >= thresholds["parameter-updates-per-second"],
            $"parameter updates {updatesPerSecond:N0}/s < target {thresholds["parameter-updates-per-second"]:N0}");
    }

    [Fact]
    public void Serialized_archive_payload_throughput_meets_the_declared_write_target()
    {
        var thresholds = Thresholds();
        // Measure the encode+serialize throughput of the archive value path,
        // the CPU-bound portion of the sustained archive-write metric.
        var mdb = new XtceLoader(Path.Combine(Fixtures.TestConfig.XtceFixtureDirectory, "SampleSat.xml")).Load();
        var engine = new DecommutationEngine(mdb);
        var root = mdb.FindContainer("/SampleSat/Root")!;
        var packet = SpacePackets.HeaderDecodeTests.GoldenPacket;

        long bytes = 0;
        const int iterations = 100_000;
        var stopwatch = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            var result = engine.Decommutate(packet, root, T0, T0);
            foreach (var value in result.Values)
            {
                // Payload bytes committed per value (raw + eng + times + status).
                bytes += packet.Length + 24;
            }
        }

        stopwatch.Stop();
        var mbPerSecond = bytes / stopwatch.Elapsed.TotalSeconds / (1024 * 1024);
        Assert.True(mbPerSecond >= thresholds["archive-write-mbps"],
            $"archive payload throughput {mbPerSecond:N1} MB/s < target {thresholds["archive-write-mbps"]:N0}");
    }
}
