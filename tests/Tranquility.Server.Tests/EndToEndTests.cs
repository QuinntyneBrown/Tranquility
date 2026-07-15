using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Tranquility.Server.Tests;

/// <summary>
/// End-to-end demonstration of the vertical slice: UDP ingest → decommutation →
/// calibration → alarms → HTTP value retrieval and WebSocket push.
/// Verifies: L2-LNK-001, L2-SPP-003, L2-PAR-001/002, L2-API-003/004,
/// L2-RTS-001/002/003.
/// </summary>
public sealed class EndToEndTests : IClassFixture<TranquilityWebApplicationFactory>
{
    private readonly TranquilityWebApplicationFactory _factory;

    public EndToEndTests(TranquilityWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GoldenPacket_OverUdp_IsQueryableViaHttp()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var client = _factory.CreateClient();

        await _factory.SendPacketAsync(TranquilityWebApplicationFactory.GoldenPacket, cts.Token);

        JsonDocument? doc = null;
        try
        {
            while (true)
            {
                using var response = await client.GetAsync(
                    "/api/processors/sample/realtime/parameters/SampleSat/Temperature", cts.Token);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cts.Token));
                    break;
                }

                await Task.Delay(50, cts.Token);
            }

            var root = doc.RootElement;
            Assert.Equal("/SampleSat/Temperature", root.GetProperty("id").GetProperty("name").GetString());
            Assert.Equal(1024ul, root.GetProperty("rawValue").GetProperty("uint64Value").GetUInt64());
            Assert.Equal(31.2, root.GetProperty("engValue").GetProperty("doubleValue").GetDouble(), precision: 6);
            Assert.Equal("WARNING", root.GetProperty("monitoringResult").GetString());

            // RFC 3339 UTC timestamp shape (L2-API-004).
            string? generationTime = root.GetProperty("generationTime").GetString();
            Assert.NotNull(generationTime);
            Assert.Matches(new Regex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$"), generationTime);
        }
        finally
        {
            doc?.Dispose();
        }
    }

    [Fact]
    public async Task WebSocket_ParameterSubscription_ReceivesPushedValues()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        _ = _factory.CreateClient();

        var wsClient = _factory.Server.CreateWebSocketClient();
        wsClient.SubProtocols.Add("json");
        var ws = await wsClient.ConnectAsync(
            new Uri(_factory.Server.BaseAddress, "/api/websocket"), cts.Token);

        Assert.Equal("json", ws.SubProtocol);

        await SendAsync(ws, """
            {"type":"parameters","id":1,"options":{"id":[{"name":"/SampleSat/BusVoltage"}]}}
            """, cts.Token);

        using (var reply = await ReceiveJsonAsync(ws, cts.Token))
        {
            Assert.Equal("reply", reply.RootElement.GetProperty("type").GetString());
            Assert.Equal(1, reply.RootElement.GetProperty("call").GetInt32());
        }

        await _factory.SendPacketAsync(TranquilityWebApplicationFactory.GoldenPacket, cts.Token);

        using (var data = await ReceiveJsonAsync(ws, cts.Token))
        {
            var root = data.RootElement;
            Assert.Equal("parameters", root.GetProperty("type").GetString());
            Assert.Equal(1, root.GetProperty("call").GetInt32());
            Assert.Equal(0, root.GetProperty("seq").GetInt32());

            var value = Assert.Single(root.GetProperty("data").GetProperty("values").EnumerateArray().ToArray());
            Assert.Equal("/SampleSat/BusVoltage", value.GetProperty("id").GetProperty("name").GetString());
            Assert.Equal(1.5, value.GetProperty("engValue").GetProperty("doubleValue").GetDouble(), precision: 6);
        }

        // Built-in cancel/state (L2-RTS-003).
        await SendAsync(ws, """{"type":"cancel","id":2,"options":{"call":1}}""", cts.Token);
        using (var reply = await ReceiveJsonAsync(ws, cts.Token))
        {
            Assert.Equal("reply", reply.RootElement.GetProperty("type").GetString());
            Assert.Equal(2, reply.RootElement.GetProperty("call").GetInt32());
        }

        await SendAsync(ws, """{"type":"state","id":3}""", cts.Token);
        using (var reply = await ReceiveJsonAsync(ws, cts.Token))
        {
            Assert.Equal("reply", reply.RootElement.GetProperty("type").GetString());
        }

        using (var state = await ReceiveJsonAsync(ws, cts.Token))
        {
            Assert.Equal("state", state.RootElement.GetProperty("type").GetString());
            Assert.Empty(state.RootElement.GetProperty("data").GetProperty("calls").EnumerateArray().ToArray());
        }

        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, null, cts.Token);
    }

    private static Task SendAsync(WebSocket socket, string json, CancellationToken cancellationToken) =>
        socket.SendAsync(
            Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, endOfMessage: true, cancellationToken);

    private static async Task<JsonDocument> ReceiveJsonAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[64 * 1024];
        using var stream = new MemoryStream();
        while (true)
        {
            var result = await socket.ReceiveAsync(buffer, cancellationToken);
            Assert.NotEqual(WebSocketMessageType.Close, result.MessageType);
            stream.Write(buffer, 0, result.Count);
            if (result.EndOfMessage)
            {
                return JsonDocument.Parse(Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Length));
            }
        }
    }
}
