using System.Text.Json;
using Tranquility.AcceptanceTests.Fixtures;
using Tranquility.AcceptanceTests.Traceability;
using Xunit;

namespace Tranquility.AcceptanceTests.Commanding;

/// <summary>
/// L2-CMD-002: GIVEN queued commands WHEN queue API methods are invoked THEN
/// queue state and entries are returned and accept/reject actions update
/// queue contents.
/// </summary>
[Requirement("L2-CMD-002")]
public sealed class CommandQueueTests(InProcApiFixture fixture) : IClassFixture<InProcApiFixture>
{
    [Fact]
    public async Task Issued_commands_enter_the_queue_and_accept_reject_update_contents()
    {
        using var admin = await fixture.AdminClientAsync();

        var first = await CommandIssueTests.IssueAsync(admin);
        var queues = await QueuesAsync(admin);
        var queue = Assert.Single(queues);
        Assert.Equal("default", queue.GetProperty("name").GetString());
        Assert.False(string.IsNullOrWhiteSpace(queue.GetProperty("state").GetString()));
        Assert.Contains(queue.GetProperty("entries").EnumerateArray(),
            e => e.GetProperty("id").GetString() == first);

        // accept releases the entry from the queue
        var accept = await admin.PostAsync(
            $"/api/processors/{TestConfig.Instance}/realtime/queues/default/entries/{first}:accept", null);
        Assert.True(accept.IsSuccessStatusCode, $":accept returned {(int)accept.StatusCode}");
        queues = await QueuesAsync(admin);
        Assert.DoesNotContain(queues[0].GetProperty("entries").EnumerateArray(),
            e => e.GetProperty("id").GetString() == first);

        // reject removes the entry without release
        var second = await CommandIssueTests.IssueAsync(admin);
        var reject = await admin.PostAsync(
            $"/api/processors/{TestConfig.Instance}/realtime/queues/default/entries/{second}:reject", null);
        Assert.True(reject.IsSuccessStatusCode, $":reject returned {(int)reject.StatusCode}");
        queues = await QueuesAsync(admin);
        Assert.DoesNotContain(queues[0].GetProperty("entries").EnumerateArray(),
            e => e.GetProperty("id").GetString() == second);
    }

    [Fact]
    public async Task Accepting_an_unknown_entry_is_a_404_envelope()
    {
        using var admin = await fixture.AdminClientAsync();
        var response = await admin.PostAsync(
            $"/api/processors/{TestConfig.Instance}/realtime/queues/default/entries/no-such-id:accept", null);
        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
        await JsonApiAssert.IsErrorEnvelopeAsync(response);
    }

    internal static async Task<List<JsonElement>> QueuesAsync(HttpClient client)
    {
        using var doc = JsonDocument.Parse(await client.GetStringAsync(
            $"/api/processors/{TestConfig.Instance}/realtime/queues"));
        return doc.RootElement.GetProperty("queues").EnumerateArray().Select(e => e.Clone()).ToList();
    }
}
