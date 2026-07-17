using System.Net.Http.Json;
using System.Text.Json;
using Tranquility.AcceptanceTests.Archive;
using Tranquility.AcceptanceTests.Fixtures;
using Tranquility.AcceptanceTests.Traceability;
using Xunit;

namespace Tranquility.AcceptanceTests.Lifecycle;

/// <summary>
/// L2-LIF-004: GIVEN a replay processor WHEN replay starts, pauses, and stops
/// THEN exposed replay-state fields reflect those transitions.
/// </summary>
[Requirement("L2-LIF-004")]
public sealed class ReplayStateTransitionTests(InProcApiFixture fixture) : IClassFixture<InProcApiFixture>
{
    [Fact]
    public async Task Replay_state_fields_reflect_pause_resume_and_completion()
    {
        using var admin = await fixture.AdminClientAsync();
        await ArchiveTestData.IngestAsync(fixture, admin,
            ArchiveTestData.PacketWithCounter(71, 51),
            ArchiveTestData.PacketWithCounter(72, 52));

        var create = await admin.PostAsJsonAsync($"/api/processors/{TestConfig.Instance}", new
        {
            name = "staged-replay",
            type = "replay",
            start = DateTimeOffset.UtcNow.AddMinutes(-10).UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'"),
            stop = DateTimeOffset.UtcNow.AddSeconds(5).UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'"),
            paused = true,
            persistent = true,
        });
        Assert.True(create.IsSuccessStatusCode, await create.Content.ReadAsStringAsync());

        // Created paused: replayState is PAUSED.
        Assert.Equal("PAUSED", await ReplayStateAsync(admin, "staged-replay"));

        // Resume: the response reflects the RUNNING transition synchronously.
        var resume = await admin.PostAsync(
            $"/api/processors/{TestConfig.Instance}/staged-replay:resume", null);
        Assert.True(resume.IsSuccessStatusCode, $":resume returned {(int)resume.StatusCode}");
        using (var doc = JsonDocument.Parse(await resume.Content.ReadAsStringAsync()))
        {
            Assert.Equal("RUNNING", doc.RootElement.GetProperty("replayState").GetString());
        }

        // Completion: persistent processor remains, STOPPED.
        await Eventually.Async(async () => await ReplayStateAsync(admin, "staged-replay") == "STOPPED",
            "replay reaches STOPPED after consuming its interval");

        // Pause on a fresh paused processor round-trips through :pause too.
        var create2 = await admin.PostAsJsonAsync($"/api/processors/{TestConfig.Instance}", new
        {
            name = "paused-replay",
            type = "replay",
            start = DateTimeOffset.UtcNow.AddMinutes(-10).UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'"),
            stop = DateTimeOffset.UtcNow.AddMinutes(10).UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'"),
            paused = false,
            persistent = true,
            speed = 0.001, // slow pacing keeps it observable
        });
        Assert.True(create2.IsSuccessStatusCode, await create2.Content.ReadAsStringAsync());
        var pause = await admin.PostAsync(
            $"/api/processors/{TestConfig.Instance}/paused-replay:pause", null);
        Assert.True(pause.IsSuccessStatusCode);
        Assert.Equal("PAUSED", await ReplayStateAsync(admin, "paused-replay"));
    }

    private static async Task<string?> ReplayStateAsync(HttpClient client, string processor)
    {
        using var doc = JsonDocument.Parse(
            await client.GetStringAsync($"/api/processors/{TestConfig.Instance}/{processor}"));
        return doc.RootElement.GetProperty("replayState").GetString();
    }
}
