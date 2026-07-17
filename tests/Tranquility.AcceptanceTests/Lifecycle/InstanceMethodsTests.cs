using System.Net;
using System.Text.Json;
using Tranquility.AcceptanceTests.Fixtures;
using Tranquility.AcceptanceTests.Traceability;
using Xunit;

namespace Tranquility.AcceptanceTests.Lifecycle;

/// <summary>
/// L2-LIF-001: GIVEN at least one configured instance WHEN instances methods
/// are called THEN state and metadata are returned in documented structure.
/// Also demonstrates L1-LIF-001 instance lifecycle control
/// (:start / :stop / :restart with conflict handling).
/// </summary>
[Requirement("L2-LIF-001")]
public sealed class InstanceMethodsTests(InProcApiFixture fixture) : IClassFixture<InProcApiFixture>
{
    [Fact]
    public async Task Instance_detail_returns_documented_state_and_metadata()
    {
        using var client = fixture.CreateClient();
        var response = await client.GetAsync($"/api/instances/{TestConfig.Instance}");

        Assert.True(response.IsSuccessStatusCode,
            $"GET instance detail returned {(int)response.StatusCode}.");
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        Assert.Equal(TestConfig.Instance, root.GetProperty("name").GetString());
        Assert.Equal("RUNNING", root.GetProperty("state").GetString());
        Assert.True(root.TryGetProperty("missionTime", out _));
        Assert.Equal(JsonValueKind.Array, root.GetProperty("processors").ValueKind);
    }

    [Fact]
    public async Task Stop_start_restart_transition_instance_state_observably()
    {
        using var admin = await fixture.AdminClientAsync();

        // stop: RUNNING -> OFFLINE, observable via the detail method
        var stop = await admin.PostAsync($"/api/instances/{TestConfig.Instance}:stop", null);
        Assert.True(stop.IsSuccessStatusCode, $":stop returned {(int)stop.StatusCode}");
        Assert.Equal("OFFLINE", await StateAsync(admin));

        // start: OFFLINE -> RUNNING
        var start = await admin.PostAsync($"/api/instances/{TestConfig.Instance}:start", null);
        Assert.True(start.IsSuccessStatusCode, $":start returned {(int)start.StatusCode}");
        Assert.Equal("RUNNING", await StateAsync(admin));

        // start while RUNNING: documented conflict
        var conflict = await admin.PostAsync($"/api/instances/{TestConfig.Instance}:start", null);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        var (type, _) = await JsonApiAssert.IsErrorEnvelopeAsync(conflict);
        Assert.Equal("ConflictException", type);

        // restart: RUNNING -> RUNNING
        var restart = await admin.PostAsync($"/api/instances/{TestConfig.Instance}:restart", null);
        Assert.True(restart.IsSuccessStatusCode, $":restart returned {(int)restart.StatusCode}");
        Assert.Equal("RUNNING", await StateAsync(admin));
    }

    private static async Task<string> StateAsync(HttpClient client)
    {
        using var doc = JsonDocument.Parse(
            await client.GetStringAsync($"/api/instances/{TestConfig.Instance}"));
        return doc.RootElement.GetProperty("state").GetString()!;
    }
}
