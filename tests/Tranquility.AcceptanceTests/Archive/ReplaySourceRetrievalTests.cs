using System.Net.Http.Json;
using Tranquility.AcceptanceTests.Fixtures;
using Tranquility.AcceptanceTests.Traceability;
using Xunit;

namespace Tranquility.AcceptanceTests.Archive;

/// <summary>
/// L2-ARC-004: GIVEN a history request with replay source option WHEN
/// processed THEN data is returned through replay-mode processing with
/// active MDB semantics — proven by hot-swapping to a recalibrated model:
/// the archive source keeps ingest-time values, the replay source
/// re-decommutates raw packets with the NEW calibration.
/// </summary>
[Requirement("L2-ARC-004")]
public sealed class ReplaySourceRetrievalTests(InProcApiFixture fixture) : IClassFixture<InProcApiFixture>
{
    [Fact]
    public async Task Replay_source_reprocesses_raw_packets_with_the_active_mdb()
    {
        using var admin = await fixture.AdminClientAsync();
        await ArchiveTestData.IngestAsync(fixture, admin,
            ArchiveTestData.PacketWithCounter(41, 21),
            ArchiveTestData.PacketWithCounter(42, 22));

        // Ingest-time calibration: eng = -20 + 0.05 * 1024 = 31.2
        var archived = await ArchiveTestData.HistoryAsync(admin, "/SampleSat/Temperature");
        Assert.True(archived.Count >= 2);
        Assert.All(archived, v => Assert.Equal(31.2,
            v.GetProperty("engValue").GetProperty("doubleValue").GetDouble(), precision: 6));

        // Hot-swap to the recalibrated model: eng = -20 + 0.1 * 1024 = 82.4
        var load = await admin.PostAsJsonAsync(
            $"/api/mdb/{TestConfig.Instance}/load", new { xtceRef = "SampleSatV2.xml" });
        Assert.True(load.IsSuccessStatusCode, $"MDB hot-swap failed: {await load.Content.ReadAsStringAsync()}");

        // Archive source still returns the values as processed at ingest.
        var archivedAfter = await ArchiveTestData.HistoryAsync(admin, "/SampleSat/Temperature");
        Assert.All(archivedAfter, v => Assert.Equal(31.2,
            v.GetProperty("engValue").GetProperty("doubleValue").GetDouble(), precision: 6));

        // Replay source re-decommutates the stored packets with the ACTIVE MDB.
        var replayed = await ArchiveTestData.HistoryAsync(admin, "/SampleSat/Temperature", "?source=replay");
        Assert.True(replayed.Count >= 2, "replay source must reprocess the archived packets");
        Assert.All(replayed, v => Assert.Equal(82.4,
            v.GetProperty("engValue").GetProperty("doubleValue").GetDouble(), precision: 6));
    }
}
