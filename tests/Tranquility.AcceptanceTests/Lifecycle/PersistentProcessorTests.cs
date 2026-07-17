using System.Net.Http.Json;
using Tranquility.AcceptanceTests.Archive;
using Tranquility.AcceptanceTests.Fixtures;
using Tranquility.AcceptanceTests.Traceability;
using Xunit;

namespace Tranquility.AcceptanceTests.Lifecycle;

/// <summary>
/// L2-LIF-003: GIVEN a processor created with persistent=true WHEN processor
/// lifecycle transitions occur THEN persistence behavior matches documented
/// semantics: persistent replay processors outlive replay completion,
/// non-persistent ones are removed automatically.
/// </summary>
[Requirement("L2-LIF-003")]
public sealed class PersistentProcessorTests(InProcApiFixture fixture) : IClassFixture<InProcApiFixture>
{
    [Fact]
    public async Task Non_persistent_replay_auto_removes_and_persistent_replay_survives_completion()
    {
        using var admin = await fixture.AdminClientAsync();
        await ArchiveTestData.IngestAsync(fixture, admin,
            ArchiveTestData.PacketWithCounter(61, 41),
            ArchiveTestData.PacketWithCounter(62, 42));

        var window = new
        {
            start = DateTimeOffset.UtcNow.AddMinutes(-10).UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'"),
            stop = DateTimeOffset.UtcNow.AddSeconds(5).UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'"),
        };

        // Non-persistent: runs to completion, then disappears from the list.
        var transient = await admin.PostAsJsonAsync($"/api/processors/{TestConfig.Instance}", new
        {
            name = "transient-replay",
            type = "replay",
            window.start,
            window.stop,
            persistent = false,
        });
        Assert.True(transient.IsSuccessStatusCode, await transient.Content.ReadAsStringAsync());
        await Eventually.Async(async () =>
            (await ProcessorLifecycleTests.ListAsync(admin))
                .All(p => p.GetProperty("name").GetString() != "transient-replay"),
            "non-persistent replay processor is removed after completion");

        // Persistent: completes but remains administratively visible.
        var persistent = await admin.PostAsJsonAsync($"/api/processors/{TestConfig.Instance}", new
        {
            name = "persistent-replay",
            type = "replay",
            window.start,
            window.stop,
            persistent = true,
        });
        Assert.True(persistent.IsSuccessStatusCode, await persistent.Content.ReadAsStringAsync());
        await Eventually.Async(async () =>
            (await ProcessorLifecycleTests.ListAsync(admin)).Any(p =>
                p.GetProperty("name").GetString() == "persistent-replay" &&
                p.GetProperty("replayState").GetString() == "STOPPED"),
            "persistent replay processor survives completion in STOPPED state");
    }
}
