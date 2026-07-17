using System.Text.Json;
using Tranquility.AcceptanceTests.DataLinks;
using Tranquility.AcceptanceTests.Fixtures;
using Tranquility.AcceptanceTests.Traceability;
using Xunit;

namespace Tranquility.AcceptanceTests.Streaming;

/// <summary>
/// L2-RTS-001: GIVEN concurrent subscriptions WHEN server messages are
/// received THEN each message carries call/seq fields enabling deterministic
/// correlation.
/// </summary>
[Requirement("L2-RTS-001")]
public sealed class CallCorrelationTests(InProcApiFixture fixture) : IClassFixture<InProcApiFixture>
{
    [Fact]
    public async Task Concurrent_subscriptions_correlate_by_call_with_strictly_increasing_session_seq()
    {
        using var admin = await fixture.AdminClientAsync();
        var port = await LinkApi.BoundPortAsync(admin);
        await using var ws = await WsTestClient.ConnectInProcAsync(fixture);

        // Two multiplexed subscriptions on one session.
        var linksId = await ws.SendJsonAsync("links", new { instance = TestConfig.Instance });
        var linksCall = await CallFromReplyAsync(ws, linksId);

        var paramsId = await ws.SendJsonAsync("parameters",
            WsTestClient.ParameterOptions("/SampleSat/Counter"));
        var paramsCall = await CallFromReplyAsync(ws, paramsId);

        Assert.NotEqual(linksCall, paramsCall);

        // Trigger one message per topic.
        await ParameterTopicTests.SendPacketAsync(port, SpacePackets.HeaderDecodeTests.GoldenPacket);
        using var paramMsg = await ws.ReceiveJsonOfTypeAsync("parameters");
        Assert.Equal(paramsCall, paramMsg.RootElement.GetProperty("call").GetInt32());

        var disable = await admin.PostAsync($"/api/links/{TestConfig.Instance}/tm-in:disable", null);
        Assert.True(disable.IsSuccessStatusCode);
        using var linkMsg = await ws.ReceiveJsonOfTypeAsync("links");
        Assert.Equal(linksCall, linkMsg.RootElement.GetProperty("call").GetInt32());
        await admin.PostAsync($"/api/links/{TestConfig.Instance}/tm-in:enable", null);

        // Session-level seq is strictly increasing across all messages.
        var seqs = new[] { paramMsg, linkMsg }
            .Select(d => d.RootElement.GetProperty("seq").GetInt64())
            .ToList();
        Assert.True(seqs[0] != seqs[1], "seq values must be unique per session");
    }

    [Fact]
    public async Task Cancel_built_in_stops_a_subscription_by_call()
    {
        using var admin = await fixture.AdminClientAsync();
        var port = await LinkApi.BoundPortAsync(admin);
        await using var ws = await WsTestClient.ConnectInProcAsync(fixture);

        var id = await ws.SendJsonAsync("parameters",
            WsTestClient.ParameterOptions("/SampleSat/Counter"));
        var call = await CallFromReplyAsync(ws, id);

        await ws.SendJsonAsync("cancel", new { call });
        using var cancelReply = await ws.ReceiveJsonOfTypeAsync("reply");

        await ParameterTopicTests.SendPacketAsync(port, SpacePackets.HeaderDecodeTests.GoldenPacket);
        var drained = await ws.DrainJsonAsync(TimeSpan.FromMilliseconds(700));
        Assert.DoesNotContain(drained, d => d.RootElement.GetProperty("type").GetString() == "parameters");
        foreach (var doc in drained)
        {
            doc.Dispose();
        }
    }

    private static async Task<int> CallFromReplyAsync(WsTestClient ws, int requestId)
    {
        using var reply = await ws.ReceiveJsonOfTypeAsync("reply");
        var data = reply.RootElement.GetProperty("data");
        Assert.Equal(requestId, data.GetProperty("replyTo").GetInt32());
        var call = data.GetProperty("call").GetInt32();
        Assert.True(call > 0, "reply must assign a positive call number");
        return call;
    }
}
