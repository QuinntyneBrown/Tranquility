using System.Text.Json;
using Tranquility.AcceptanceTests.Fixtures;
using Tranquility.AcceptanceTests.Traceability;
using Xunit;

namespace Tranquility.AcceptanceTests.Archive;

/// <summary>
/// L2-ARC-003: GIVEN a valid parameter ID WHEN segment endpoint is queried
/// THEN segment metadata is returned with start, end, and count fields.
/// </summary>
[Requirement("L2-ARC-003")]
public sealed class SegmentListingTests(InProcApiFixture fixture) : IClassFixture<InProcApiFixture>
{
    [Fact]
    public async Task Segments_report_start_end_and_count_for_an_archived_parameter()
    {
        using var admin = await fixture.AdminClientAsync();
        await ArchiveTestData.IngestAsync(fixture, admin,
            ArchiveTestData.PacketWithCounter(31, 11),
            ArchiveTestData.PacketWithCounter(32, 12),
            ArchiveTestData.PacketWithCounter(33, 13));

        // Discover the pid for Counter through the pid registry.
        using var pidsDoc = JsonDocument.Parse(
            await admin.GetStringAsync($"/api/parameter-archive/{TestConfig.Instance}/pids"));
        var pid = pidsDoc.RootElement.GetProperty("pids").EnumerateArray()
            .Single(p => p.GetProperty("name").GetString() == "/SampleSat/Counter")
            .GetProperty("pid").GetInt32();

        using var doc = JsonDocument.Parse(await admin.GetStringAsync(
            $"/api/parameter-archive/{TestConfig.Instance}/pids/{pid}/segments"));
        var segments = doc.RootElement.GetProperty("segments").EnumerateArray().ToList();
        Assert.True(segments.Count >= 1, "at least one segment must cover the ingested values");

        var total = 0;
        foreach (var segment in segments)
        {
            var start = DateTimeOffset.Parse(segment.GetProperty("start").GetString()!);
            var end = DateTimeOffset.Parse(segment.GetProperty("end").GetString()!);
            var count = segment.GetProperty("count").GetInt32();
            Assert.True(start <= end, "segment start must not exceed its end");
            Assert.True(count > 0);
            total += count;
        }

        Assert.True(total >= 3, $"segments must account for the ingested values, got {total}");
    }

    [Fact]
    public async Task Unknown_pid_returns_404_envelope()
    {
        using var admin = await fixture.AdminClientAsync();
        var response = await admin.GetAsync(
            $"/api/parameter-archive/{TestConfig.Instance}/pids/999999/segments");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
        await JsonApiAssert.IsErrorEnvelopeAsync(response);
    }
}
