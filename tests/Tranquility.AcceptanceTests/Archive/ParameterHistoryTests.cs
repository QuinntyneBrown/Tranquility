using Tranquility.AcceptanceTests.Fixtures;
using Tranquility.AcceptanceTests.Traceability;
using Xunit;

namespace Tranquility.AcceptanceTests.Archive;

/// <summary>
/// L2-ARC-001: GIVEN archived parameter data WHEN history endpoint is queried
/// with time bounds THEN returned values are bounded by requested interval.
/// </summary>
[Requirement("L2-ARC-001")]
public sealed class ParameterHistoryTests(InProcApiFixture fixture) : IClassFixture<InProcApiFixture>
{
    [Fact]
    public async Task History_returns_archived_values_bounded_by_the_requested_interval()
    {
        using var admin = await fixture.AdminClientAsync();
        var before = DateTimeOffset.UtcNow.AddSeconds(-5);
        await ArchiveTestData.IngestAsync(fixture, admin,
            ArchiveTestData.PacketWithCounter(11, 1),
            ArchiveTestData.PacketWithCounter(12, 2),
            ArchiveTestData.PacketWithCounter(13, 3));
        var after = DateTimeOffset.UtcNow.AddSeconds(5);

        // Bounded query covering the ingest window returns all three, in order.
        var values = await ArchiveTestData.HistoryAsync(admin, "/SampleSat/Counter",
            $"?start={Uri.EscapeDataString(Rfc3339(before))}&stop={Uri.EscapeDataString(Rfc3339(after))}");
        Assert.Equal(3, values.Count);
        Assert.Equal(new[] { 11L, 12L, 13L },
            values.Select(v => long.Parse(v.GetProperty("engValue").GetProperty("uint64Value").GetString()!)).ToArray());
        foreach (var value in values)
        {
            var t = DateTimeOffset.Parse(value.GetProperty("generationTime").GetString()!);
            Assert.InRange(t, before, after);
        }

        // An interval in the past excludes everything.
        var empty = await ArchiveTestData.HistoryAsync(admin, "/SampleSat/Counter",
            $"?start={Uri.EscapeDataString(Rfc3339(before.AddHours(-2)))}&stop={Uri.EscapeDataString(Rfc3339(before))}");
        Assert.Empty(empty);
    }

    [Fact]
    public async Task History_supports_documented_limit_and_descending_order_controls()
    {
        using var admin = await fixture.AdminClientAsync();
        await ArchiveTestData.IngestAsync(fixture, admin,
            ArchiveTestData.PacketWithCounter(21, 4),
            ArchiveTestData.PacketWithCounter(22, 5),
            ArchiveTestData.PacketWithCounter(23, 6));

        var limited = await ArchiveTestData.HistoryAsync(admin, "/SampleSat/Counter", "?limit=2&order=desc");
        Assert.Equal(2, limited.Count);
        var first = long.Parse(limited[0].GetProperty("engValue").GetProperty("uint64Value").GetString()!);
        var second = long.Parse(limited[1].GetProperty("engValue").GetProperty("uint64Value").GetString()!);
        Assert.True(first >= second, "descending order returns newest first");
    }

    [Fact]
    public async Task Unknown_parameter_returns_404_envelope()
    {
        using var client = fixture.CreateClient();
        var response = await client.GetAsync(
            $"/api/archive/{TestConfig.Instance}/parameters/SampleSat/NoSuch");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
        await JsonApiAssert.IsErrorEnvelopeAsync(response);
    }

    private static string Rfc3339(DateTimeOffset t) =>
        t.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'");
}
