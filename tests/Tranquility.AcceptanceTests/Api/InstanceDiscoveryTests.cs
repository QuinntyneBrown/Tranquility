using System.Text.Json;
using Tranquility.AcceptanceTests.Fixtures;
using Tranquility.AcceptanceTests.Traceability;
using Xunit;

namespace Tranquility.AcceptanceTests.Api;

/// <summary>
/// L2-API-001: GIVEN a running Tranquility API WHEN a client calls
/// GET /api/instances THEN the response includes an `instances` collection
/// with documented instance fields.
/// </summary>
[Requirement("L2-API-001")]
public sealed class InstanceDiscoveryTests(InProcApiFixture fixture) : IClassFixture<InProcApiFixture>
{
    [Fact]
    public async Task List_instances_returns_instances_collection_with_documented_fields()
    {
        using var client = fixture.CreateClient();
        var response = await client.GetAsync("/api/instances");

        Assert.True(response.IsSuccessStatusCode,
            $"GET /api/instances returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.True(doc.RootElement.TryGetProperty("instances", out var instances),
            "Response must contain an 'instances' collection.");
        Assert.Equal(JsonValueKind.Array, instances.ValueKind);

        var sim = instances.EnumerateArray()
            .Single(i => i.GetProperty("name").GetString() == TestConfig.Instance);
        Assert.False(string.IsNullOrEmpty(sim.GetProperty("state").GetString()),
            "Instance entries must expose a state field.");
        Assert.True(sim.TryGetProperty("missionTime", out _),
            "Instance entries must expose a missionTime field.");
    }
}
