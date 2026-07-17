using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text.Json;
using Tranquility.AcceptanceTests.Fixtures;
using Tranquility.AcceptanceTests.Traceability;
using Xunit;

namespace Tranquility.AcceptanceTests.Streaming;

/// <summary>
/// Kestrel host with a deliberately tiny per-session outbound buffer so a
/// slow consumer forces message drops.
/// </summary>
public sealed class SmallBufferKestrelFixture : SecureKestrelFixture
{
    protected override Dictionary<string, string?> ExtraSettings() => new()
    {
        ["Tranquility:WebSocket:SessionBufferSize"] = "4",
    };
}

/// <summary>
/// L2-RTS-004: GIVEN an induced drop condition WHEN subscription traffic is
/// analyzed THEN sequence discontinuity is observable by the client. Requires
/// real sockets: TCP backpressure is what lets the outbound buffer fill.
/// </summary>
[Requirement("L2-RTS-004")]
public sealed class SequenceDropDetectabilityTests(SmallBufferKestrelFixture fixture)
    : IClassFixture<SmallBufferKestrelFixture>
{
    [Fact]
    public async Task Slow_consumer_observes_seq_discontinuity_and_drop_count()
    {
        using var http = fixture.CreateClient();

        // Authenticate and find the UDP ingest port on this dedicated host.
        var token = await TokenAsync(http);
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var port = await BoundPortAsync(http);

        using var socket = new ClientWebSocket();
        socket.Options.AddSubProtocol("json");
        socket.Options.RemoteCertificateValidationCallback = (_, cert, _, _) =>
            cert is not null && cert.GetCertHashString() == fixture.Certificate.GetCertHashString();
        await socket.ConnectAsync(
            new UriBuilder(fixture.BaseAddress) { Scheme = "wss", Path = "/api/websocket" }.Uri,
            CancellationToken.None);
        await using var ws = new WsTestClient(socket);

        await ws.SendJsonAsync("parameters", WsTestClient.ParameterOptions("/SampleSat/Temperature"));
        using var reply = await ws.ReceiveJsonOfTypeAsync("reply");

        // Flood while NOT reading: TCP windows fill, then the 4-slot session
        // buffer, then messages drop.
        using (var udp = new System.Net.Sockets.UdpClient())
        {
            for (var i = 0; i < 3000; i++)
            {
                await udp.SendAsync(SpacePackets.HeaderDecodeTests.GoldenPacket, "127.0.0.1", port);
            }
        }

        await Task.Delay(1500); // let the flood finish while we stay slow

        // Drain sequentially (never cancel a pending receive: that aborts a
        // ClientWebSocket) until a discontinuity is observed.
        var seqs = new List<long>();
        var gapObserved = false;
        var receiveTimeout = TimeSpan.FromSeconds(30);
        while (!gapObserved && seqs.Count < 5000)
        {
            using var doc = await ws.ReceiveJsonAsync(receiveTimeout);
            if (doc.RootElement.GetProperty("type").GetString() != "parameters")
            {
                continue;
            }

            var seq = doc.RootElement.GetProperty("seq").GetInt64();
            gapObserved = seqs.Count > 0 && seq - seqs[^1] > 1;
            seqs.Add(seq);
        }

        Assert.True(gapObserved,
            $"No seq discontinuity observed across {seqs.Count} delivered messages " +
            $"(min {(seqs.Count > 0 ? seqs.Min() : 0)}, max {(seqs.Count > 0 ? seqs.Max() : 0)}).");

        // Between sequential receives the socket is idle, so the state
        // built-in can be requested; it lands after the remaining backlog.
        await ws.SendJsonAsync("state");
        using var state = await ws.ReceiveJsonOfTypeAsync("state", TimeSpan.FromSeconds(30));
        // proto3 JSON mapping renders int64 fields as strings.
        var dropped = long.Parse(state.RootElement.GetProperty("data").GetProperty("droppedMessages").GetString()!);
        Assert.True(dropped > 0, "state must report droppedMessages > 0 after an induced drop");
    }

    private static async Task<string> TokenAsync(HttpClient http)
    {
        var response = await http.PostAsJsonAsync("/auth/token",
            new { username = TestConfig.AdminUser, password = TestConfig.AdminPassword });
        Assert.True(response.IsSuccessStatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("access_token").GetString()!;
    }

    private static async Task<int> BoundPortAsync(HttpClient http)
    {
        using var doc = JsonDocument.Parse(await http.GetStringAsync($"/api/links/{TestConfig.Instance}"));
        return doc.RootElement.GetProperty("links").EnumerateArray()
            .Single(l => l.GetProperty("name").GetString() == "tm-in")
            .GetProperty("boundPort").GetInt32();
    }
}
