using Tranquility.AcceptanceTests.Fixtures;
using Tranquility.AcceptanceTests.Traceability;
using Xunit;

namespace Tranquility.AcceptanceTests.FileTransfer;

/// <summary>
/// L2-FDP-003: GIVEN an active transfer subscription WHEN transfer state
/// changes THEN a status update is delivered on the subscription stream.
/// </summary>
[Requirement("L2-FDP-003")]
public sealed class TransferSubscriptionTests(InProcApiFixture fixture) : IClassFixture<InProcApiFixture>
{
    [Fact]
    public async Task File_transfer_topic_pushes_status_updates()
    {
        using var admin = await fixture.AdminClientAsync();
        await using var ws = await WsTestClient.ConnectInProcAsync(fixture);

        await ws.SendJsonAsync("file-transfers", new { instance = TestConfig.Instance });
        using var reply = await ws.ReceiveJsonOfTypeAsync("reply");

        var payload = new byte[8000];
        new Random(3).NextBytes(payload);
        var id = await TransferApiTests.CreateAsync(admin, payload, reliable: true);

        // At least one pushed status update for this transfer reaching a terminal state.
        var reachedTerminal = false;
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (!reachedTerminal && DateTime.UtcNow < deadline)
        {
            using var msg = await ws.ReceiveJsonOfTypeAsync("file-transfers", TimeSpan.FromSeconds(20));
            var data = msg.RootElement.GetProperty("data");
            if (data.GetProperty("id").GetString() == id)
            {
                var state = data.GetProperty("state").GetString();
                reachedTerminal = state is "COMPLETED" or "FAILED" or "CANCELLED";
            }
        }

        Assert.True(reachedTerminal, "a status update for the transfer's terminal state must be pushed");
    }
}
