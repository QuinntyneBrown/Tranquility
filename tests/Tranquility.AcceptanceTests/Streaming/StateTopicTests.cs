using Tranquility.AcceptanceTests.Fixtures;
using Tranquility.AcceptanceTests.Traceability;
using Xunit;

namespace Tranquility.AcceptanceTests.Streaming;

/// <summary>
/// L2-RTS-003: GIVEN active processor/link subscriptions WHEN runtime state
/// changes THEN state updates are emitted without polling.
/// </summary>
[Requirement("L2-RTS-003")]
public sealed class StateTopicTests(InProcApiFixture fixture) : IClassFixture<InProcApiFixture>
{
    [Fact]
    public async Task Link_state_change_is_pushed_to_link_subscribers()
    {
        using var admin = await fixture.AdminClientAsync();
        await using var ws = await WsTestClient.ConnectInProcAsync(fixture);

        await ws.SendJsonAsync("links", new { instance = TestConfig.Instance });
        using var reply = await ws.ReceiveJsonOfTypeAsync("reply");

        var disable = await admin.PostAsync($"/api/links/{TestConfig.Instance}/tm-in:disable", null);
        Assert.True(disable.IsSuccessStatusCode);

        using var push = await ws.ReceiveJsonOfTypeAsync("links");
        var data = push.RootElement.GetProperty("data");
        Assert.Equal("tm-in", data.GetProperty("name").GetString());
        Assert.True(data.GetProperty("disabled").GetBoolean());

        await admin.PostAsync($"/api/links/{TestConfig.Instance}/tm-in:enable", null);
        using var push2 = await ws.ReceiveJsonOfTypeAsync("links");
        Assert.False(push2.RootElement.GetProperty("data").GetProperty("disabled").GetBoolean());
    }

    [Fact]
    public async Task Processor_state_change_is_pushed_to_processor_subscribers()
    {
        using var admin = await fixture.AdminClientAsync();
        await using var ws = await WsTestClient.ConnectInProcAsync(fixture);

        await ws.SendJsonAsync("processors", new { instance = TestConfig.Instance });
        using var reply = await ws.ReceiveJsonOfTypeAsync("reply");

        var stop = await admin.PostAsync($"/api/instances/{TestConfig.Instance}:stop", null);
        Assert.True(stop.IsSuccessStatusCode);

        using var push = await ws.ReceiveJsonOfTypeAsync("processors");
        var data = push.RootElement.GetProperty("data");
        Assert.Equal("realtime", data.GetProperty("name").GetString());
        Assert.NotEqual("RUNNING", data.GetProperty("state").GetString());

        await admin.PostAsync($"/api/instances/{TestConfig.Instance}:start", null);
        using var push2 = await ws.ReceiveJsonOfTypeAsync("processors");
        Assert.Equal("RUNNING", push2.RootElement.GetProperty("data").GetProperty("state").GetString());
    }
}
