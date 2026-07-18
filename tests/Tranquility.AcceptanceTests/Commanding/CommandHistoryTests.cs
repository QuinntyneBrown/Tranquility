using System.Text.Json;
using Tranquility.AcceptanceTests.Fixtures;
using Tranquility.AcceptanceTests.Traceability;
using Xunit;

namespace Tranquility.AcceptanceTests.Commanding;

/// <summary>
/// L2-CMD-005: GIVEN a command that is queued and released WHEN history is
/// queried THEN lifecycle records show ordered stage progression.
/// </summary>
[Requirement("L2-CMD-005")]
public sealed class CommandHistoryTests(InProcApiFixture fixture) : IClassFixture<InProcApiFixture>
{
    [Fact]
    public async Task Released_command_history_shows_ordered_lifecycle_stages()
    {
        using var admin = await fixture.AdminClientAsync();
        var id = await CommandIssueTests.IssueAsync(admin);
        var accept = await admin.PostAsync(
            $"/api/processors/{TestConfig.Instance}/realtime/queues/default/entries/{id}:accept", null);
        Assert.True(accept.IsSuccessStatusCode);

        await Eventually.Async(async () =>
        {
            var entry = await EntryAsync(admin, id);
            if (entry is null)
            {
                return false;
            }

            var stages = entry.Value.GetProperty("attributes").EnumerateArray()
                .Select(a => a.GetProperty("name").GetString()).ToList();
            return stages.Contains("SENT");
        }, "history reaches the SENT stage after release");

        var final = await EntryAsync(admin, id);
        Assert.NotNull(final);
        var attributes = final.Value.GetProperty("attributes").EnumerateArray().ToList();
        var stageNames = attributes.Select(a => a.GetProperty("name").GetString()).ToList();

        // Ordered progression: each lifecycle stage strictly after its predecessor.
        var expected = new[] { "ISSUED", "QUEUED", "RELEASED", "SENT" };
        var indices = expected.Select(s => stageNames.IndexOf(s)).ToArray();
        Assert.All(indices, i => Assert.True(i >= 0, $"missing stage among [{string.Join(",", stageNames)}]"));
        Assert.True(indices.SequenceEqual(indices.OrderBy(i => i)), "stages must appear in lifecycle order");

        var times = attributes.Select(a => DateTimeOffset.Parse(a.GetProperty("time").GetString()!)).ToList();
        Assert.True(times.SequenceEqual(times.OrderBy(t => t)), "stage timestamps must be nondecreasing");
    }

    [Fact]
    public async Task Rejected_command_history_records_the_rejection_stage()
    {
        using var admin = await fixture.AdminClientAsync();
        var id = await CommandIssueTests.IssueAsync(admin);
        var reject = await admin.PostAsync(
            $"/api/processors/{TestConfig.Instance}/realtime/queues/default/entries/{id}:reject", null);
        Assert.True(reject.IsSuccessStatusCode);

        await Eventually.Async(async () =>
        {
            var entry = await EntryAsync(admin, id);
            return entry is { } e && e.GetProperty("attributes").EnumerateArray()
                .Any(a => a.GetProperty("name").GetString() == "REJECTED");
        }, "history records the REJECTED stage");
    }

    private static async Task<JsonElement?> EntryAsync(HttpClient client, string id)
    {
        using var doc = JsonDocument.Parse(await client.GetStringAsync(
            $"/api/archive/{TestConfig.Instance}/commands"));
        foreach (var entry in doc.RootElement.GetProperty("entries").EnumerateArray())
        {
            if (entry.GetProperty("id").GetString() == id)
            {
                return entry.Clone();
            }
        }

        return null;
    }
}
