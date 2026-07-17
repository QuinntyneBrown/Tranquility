using System.Text.Json;
using Tranquility.AcceptanceTests.Fixtures;
using Tranquility.AcceptanceTests.Traceability;
using Xunit;

namespace Tranquility.AcceptanceTests.Api;

/// <summary>
/// L2-API-005: GIVEN any response containing time fields WHEN the payload is
/// serialized THEN timestamp strings are UTC ISO 8601 / RFC 3339.
/// </summary>
[Requirement("L2-API-005")]
public sealed class TimestampFormatTests(InProcApiFixture fixture) : IClassFixture<InProcApiFixture>
{
    [Fact]
    public async Task Instance_detail_serializes_all_time_fields_as_rfc3339_utc()
    {
        using var client = fixture.CreateClient();
        var response = await client.GetAsync($"/api/instances/{TestConfig.Instance}");

        Assert.True(response.IsSuccessStatusCode,
            $"GET instance detail returned {(int)response.StatusCode}.");
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.True(doc.RootElement.TryGetProperty("missionTime", out var missionTime),
            "Instance detail must expose missionTime.");
        Assert.Matches(JsonApiAssert.Rfc3339Utc(), missionTime.GetString()!);
        JsonApiAssert.AllTimestampsAreRfc3339Utc(doc.RootElement);
    }

    [Fact]
    public async Task Instance_list_serializes_all_time_fields_as_rfc3339_utc()
    {
        using var client = fixture.CreateClient();
        var response = await client.GetAsync("/api/instances");

        Assert.True(response.IsSuccessStatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonApiAssert.AllTimestampsAreRfc3339Utc(doc.RootElement);
    }
}
