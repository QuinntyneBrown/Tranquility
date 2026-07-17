using Tranquility.AcceptanceTests.DataLinks;
using Tranquility.AcceptanceTests.Fixtures;
using Tranquility.AcceptanceTests.Traceability;
using Xunit;

namespace Tranquility.AcceptanceTests.Streaming;

/// <summary>
/// L2-PAR-004: GIVEN a parameter transition from in-limits to out-of-limits
/// WHEN processing runs THEN an alarm transition notification is observable
/// through the API.
/// </summary>
[Requirement("L2-PAR-004")]
public sealed class AlarmTransitionTests(InProcApiFixture fixture) : IClassFixture<InProcApiFixture>
{
    /// <summary>Temperature raw 500 -> eng 5.0, inside every band.</summary>
    private static readonly byte[] InLimitsPacket =
    [
        0x00, 0x64, 0xC0, 0x06, 0x00, 0x07,
        0x01, 0x03, 0x1F, 0x42, 0x3F, 0xC0, 0x00, 0x00,
    ];

    [Fact]
    public async Task Out_of_limits_transition_raises_then_in_limits_clears()
    {
        using var admin = await fixture.AdminClientAsync();
        var port = await LinkApi.BoundPortAsync(admin);
        await using var ws = await WsTestClient.ConnectInProcAsync(fixture);

        await ws.SendJsonAsync("alarms", new { instance = TestConfig.Instance });
        using var reply = await ws.ReceiveJsonOfTypeAsync("reply");

        // 31.2 violates the [-10, 30] warning band -> RAISED.
        await ParameterTopicTests.SendPacketAsync(port, SpacePackets.HeaderDecodeTests.GoldenPacket);
        using var raised = await ws.ReceiveJsonOfTypeAsync("alarms");
        var raisedData = raised.RootElement.GetProperty("data");
        Assert.Equal("RAISED", raisedData.GetProperty("notificationType").GetString());
        Assert.Equal("/SampleSat/Temperature", raisedData.GetProperty("id").GetProperty("name").GetString());
        Assert.Equal("WARNING", raisedData.GetProperty("severity").GetString());
        Assert.True(raisedData.GetProperty("violationCount").GetInt32() >= 1);

        // Back inside every band -> CLEARED.
        await ParameterTopicTests.SendPacketAsync(port, InLimitsPacket);
        using var cleared = await ws.ReceiveJsonOfTypeAsync("alarms");
        Assert.Equal("CLEARED", cleared.RootElement.GetProperty("data").GetProperty("notificationType").GetString());
    }
}
