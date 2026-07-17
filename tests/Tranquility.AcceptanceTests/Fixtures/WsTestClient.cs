using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Google.Protobuf;
using Tranquility.Wire.Proto;
using Xunit;

namespace Tranquility.AcceptanceTests.Fixtures;

/// <summary>
/// WebSocket test client speaking the documented framing over either the
/// in-proc TestServer transport or a real (wss) socket, in json or protobuf
/// subprotocol.
/// </summary>
public sealed class WsTestClient(WebSocket socket) : IAsyncDisposable
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);
    private int _nextId;

    public string? SubProtocol => socket.SubProtocol;

    public static async Task<WsTestClient> ConnectInProcAsync(
        Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory,
        string subprotocol = "json")
    {
        var client = factory.Server.CreateWebSocketClient();
        client.SubProtocols.Add(subprotocol);
        var ws = await client.ConnectAsync(new Uri("ws://localhost/api/websocket"), CancellationToken.None);
        return new WsTestClient(ws);
    }

    public async Task<int> SendJsonAsync(string type, object? options = null)
    {
        var id = ++_nextId;
        var payload = JsonSerializer.SerializeToUtf8Bytes(
            new { type, id, options }, Tranquility.Wire.Json.WireJson.Options);
        await socket.SendAsync(payload, WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None);
        return id;
    }

    public async Task SendProtoAsync(ClientMessage message)
    {
        await socket.SendAsync(message.ToByteArray(), WebSocketMessageType.Binary,
            endOfMessage: true, CancellationToken.None);
    }

    public async Task<JsonDocument> ReceiveJsonAsync(TimeSpan? timeout = null)
    {
        var (bytes, type) = await ReceiveFrameAsync(timeout ?? DefaultTimeout);
        Assert.Equal(WebSocketMessageType.Text, type);
        return JsonDocument.Parse(bytes);
    }

    public async Task<ServerMessage> ReceiveProtoAsync(TimeSpan? timeout = null)
    {
        var (bytes, type) = await ReceiveFrameAsync(timeout ?? DefaultTimeout);
        Assert.Equal(WebSocketMessageType.Binary, type);
        return ServerMessage.Parser.ParseFrom(bytes);
    }

    /// <summary>Receives until a message of the given type arrives.</summary>
    public async Task<JsonDocument> ReceiveJsonOfTypeAsync(string type, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? DefaultTimeout);
        while (true)
        {
            var remaining = deadline - DateTime.UtcNow;
            Assert.True(remaining > TimeSpan.Zero, $"No '{type}' message arrived before the deadline.");
            var doc = await ReceiveJsonAsync(remaining);
            if (doc.RootElement.GetProperty("type").GetString() == type)
            {
                return doc;
            }

            doc.Dispose();
        }
    }

    /// <summary>Drains every already-available message within the grace window.</summary>
    public async Task<List<JsonDocument>> DrainJsonAsync(TimeSpan grace)
    {
        var drained = new List<JsonDocument>();
        while (true)
        {
            try
            {
                drained.Add(await ReceiveJsonAsync(grace));
            }
            catch (OperationCanceledException)
            {
                return drained;
            }
            catch (Xunit.Sdk.XunitException)
            {
                return drained;
            }
        }
    }

    private async Task<(byte[] Bytes, WebSocketMessageType Type)> ReceiveFrameAsync(TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        while (true)
        {
            var result = await socket.ReceiveAsync(chunk, cts.Token);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                Assert.Fail("WebSocket closed by server while a message was expected.");
            }

            buffer.Write(chunk, 0, result.Count);
            if (result.EndOfMessage)
            {
                return (buffer.ToArray(), result.MessageType);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (socket.State == WebSocketState.Open)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            try
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", cts.Token);
            }
            catch (Exception e) when (e is OperationCanceledException or WebSocketException)
            {
            }
        }

        socket.Dispose();
    }

    /// <summary>Builds the parameters-subscription options used across tests.</summary>
    public static object ParameterOptions(params string[] names) => new
    {
        instance = TestConfig.Instance,
        processor = "realtime",
        id = names.Select(n => new { name = n }).ToArray(),
    };
}
