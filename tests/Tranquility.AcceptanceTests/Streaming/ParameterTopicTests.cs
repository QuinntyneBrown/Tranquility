using System.Text.Json;
using Tranquility.AcceptanceTests.DataLinks;
using Tranquility.AcceptanceTests.Fixtures;
using Tranquility.AcceptanceTests.Traceability;
using Tranquility.Wire.Proto;
using Xunit;

namespace Tranquility.AcceptanceTests.Streaming;

/// <summary>
/// L2-RTS-002: GIVEN a valid parameters subscription request WHEN telemetry
/// arrives THEN parameter updates are emitted on the subscription stream.
/// L2-PAR-003: updates carry acquisitionTime and generationTime.
/// </summary>
public sealed class ParameterTopicTests(InProcApiFixture fixture) : IClassFixture<InProcApiFixture>
{
    /// <summary>Golden science packet: Temperature raw 1024 -> 31.2 (WARNING).</summary>
    private static readonly byte[] GoldenPacket = SpacePackets.HeaderDecodeTests.GoldenPacket;

    [Fact]
    [Requirement("L2-RTS-002")]
    [Requirement("L2-PAR-003")]
    public async Task Json_subscription_receives_parameter_updates_with_both_time_dimensions()
    {
        using var admin = await fixture.AdminClientAsync();
        var port = await LinkApi.BoundPortAsync(admin);
        await using var ws = await WsTestClient.ConnectInProcAsync(fixture);

        await ws.SendJsonAsync("parameters",
            WsTestClient.ParameterOptions("/SampleSat/Temperature"));
        using var reply = await ws.ReceiveJsonOfTypeAsync("reply");

        await SendPacketAsync(port, GoldenPacket);
        using var update = await ws.ReceiveJsonOfTypeAsync("parameters");

        var value = update.RootElement.GetProperty("data").GetProperty("values").EnumerateArray()
            .Single(v => v.GetProperty("id").GetProperty("name").GetString() == "/SampleSat/Temperature");
        Assert.Equal(31.2, value.GetProperty("engValue").GetProperty("doubleValue").GetDouble(), precision: 6);
        Assert.Equal("WARNING", value.GetProperty("monitoringResult").GetString());

        // L2-PAR-003: both documented time dimensions, RFC 3339 UTC.
        Assert.Matches(JsonApiAssert.Rfc3339Utc(), value.GetProperty("acquisitionTime").GetString()!);
        Assert.Matches(JsonApiAssert.Rfc3339Utc(), value.GetProperty("generationTime").GetString()!);
    }

    [Fact]
    [Requirement("L2-RTS-002")]
    public async Task Protobuf_subscription_receives_the_same_updates_in_binary_encoding()
    {
        using var admin = await fixture.AdminClientAsync();
        var port = await LinkApi.BoundPortAsync(admin);
        await using var ws = await WsTestClient.ConnectInProcAsync(fixture, "protobuf");

        var request = new ClientMessage
        {
            Type = "parameters",
            Id = 1,
            Parameters = new SubscribeParametersRequest
            {
                Instance = TestConfig.Instance,
                Processor = "realtime",
                Id = { new NamedObjectId { Name = "/SampleSat/Temperature" } },
            },
        };
        await ws.SendProtoAsync(request);

        var reply = await ws.ReceiveProtoAsync();
        Assert.Equal("reply", reply.Type);
        Assert.Equal(1, reply.Reply.ReplyTo);
        Assert.True(reply.Reply.Call > 0);

        await SendPacketAsync(port, GoldenPacket);

        ServerMessage update;
        do
        {
            update = await ws.ReceiveProtoAsync();
        }
        while (update.Type != "parameters");

        var value = update.Parameters.Values
            .Single(v => v.Id.Name == "/SampleSat/Temperature");
        Assert.Equal(31.2, value.EngValue.DoubleValue, precision: 6);
        Assert.NotNull(value.AcquisitionTime);
        Assert.NotNull(value.GenerationTime);
    }

    [Fact]
    [Requirement("L2-RTS-002")]
    public async Task Send_from_cache_delivers_the_latest_value_without_new_traffic()
    {
        using var admin = await fixture.AdminClientAsync();
        var port = await LinkApi.BoundPortAsync(admin);

        // Prime the cache through a throwaway subscription.
        await using (var primer = await WsTestClient.ConnectInProcAsync(fixture))
        {
            await primer.SendJsonAsync("parameters",
                WsTestClient.ParameterOptions("/SampleSat/Temperature"));
            using var _ = await primer.ReceiveJsonOfTypeAsync("reply");
            await SendPacketAsync(port, GoldenPacket);
            using var __ = await primer.ReceiveJsonOfTypeAsync("parameters");
        }

        await using var ws = await WsTestClient.ConnectInProcAsync(fixture);
        await ws.SendJsonAsync("parameters", new
        {
            instance = TestConfig.Instance,
            processor = "realtime",
            id = new[] { new { name = "/SampleSat/Temperature" } },
            sendFromCache = true,
        });
        using var reply = await ws.ReceiveJsonOfTypeAsync("reply");

        // No new packet: the cached value must arrive on its own.
        using var cached = await ws.ReceiveJsonOfTypeAsync("parameters");
        var value = cached.RootElement.GetProperty("data").GetProperty("values").EnumerateArray().Single();
        Assert.Equal(31.2, value.GetProperty("engValue").GetProperty("doubleValue").GetDouble(), precision: 6);
    }

    [Fact]
    [Requirement("L2-RTS-002")]
    public async Task Invalid_identifier_with_abort_on_invalid_yields_an_error_reply()
    {
        await using var ws = await WsTestClient.ConnectInProcAsync(fixture);
        await ws.SendJsonAsync("parameters", new
        {
            instance = TestConfig.Instance,
            processor = "realtime",
            id = new[] { new { name = "/SampleSat/NoSuchParameter" } },
            abortOnInvalid = true,
        });

        using var reply = await ws.ReceiveJsonOfTypeAsync("reply");
        var exception = reply.RootElement.GetProperty("data").GetProperty("exception");
        Assert.Contains("NoSuchParameter", exception.GetProperty("msg").GetString(), StringComparison.Ordinal);
    }

    internal static async Task SendPacketAsync(int port, byte[] packet)
    {
        using var udp = new System.Net.Sockets.UdpClient();
        await udp.SendAsync(packet, "127.0.0.1", port);
    }
}
