using System.Buffers;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Tranquility.Application.Processing;
using Tranquility.Core.Mdb;
using Tranquility.Server.Api;

namespace Tranquility.Server.WebSockets;

/// <summary>
/// WebSocket subscription API on /api/websocket per docs/specs/TRQ-ICD-API.md §3.
/// Native System.Net.WebSockets, JSON subprotocol (ADR-0002; protobuf deferred, TBD-012).
///
/// Protocol:
///   client → {"type": &lt;topic&gt;, "id": n, "options": {...}}
///   server → {"type": "reply", "call": n}                                (correlation)
///   server → {"type": &lt;topic&gt;, "call": n, "seq": k, "data": {...}}       (stream)
/// Built-in topics: "parameters" (subscribe), "cancel", "state".
/// Implements: L2-API-003 (endpoint + subprotocol), L2-RTS-001 (call/seq
/// correlation), L2-RTS-002 (parameters topic), L2-RTS-003 (cancel/state).
/// </summary>
public sealed class WebSocketApiHandler
{
    private readonly SubscriptionManager _subscriptions;
    private readonly MissionDatabase _mdb;
    private readonly ILogger<WebSocketApiHandler> _logger;

    public WebSocketApiHandler(SubscriptionManager subscriptions, MissionDatabase mdb, ILogger<WebSocketApiHandler> logger)
    {
        _subscriptions = subscriptions;
        _mdb = mdb;
        _logger = logger;
    }

    public async Task HandleAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(
                new ErrorEnvelope(new ExceptionDto("BadRequestException", "WebSocket upgrade required.")),
                WireMapper.JsonOptions,
                cancellationToken: context.RequestAborted);
            return;
        }

        string? subprotocol = context.WebSockets.WebSocketRequestedProtocols.Contains("json") ? "json" : null;
        using var socket = await context.WebSockets.AcceptWebSocketAsync(subprotocol);
        await RunSessionAsync(socket, context.RequestAborted);
    }

    private async Task RunSessionAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        var session = new Session(socket);
        try
        {
            while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                string? text = await ReceiveTextAsync(socket, cancellationToken);
                if (text is null)
                {
                    break;
                }

                await DispatchAsync(session, text, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (WebSocketException ex)
        {
            _logger.LogDebug(ex, "WebSocket session ended abnormally");
        }
        finally
        {
            session.CancelAll();
        }
    }

    private async Task DispatchAsync(Session session, string text, CancellationToken cancellationToken)
    {
        int id = 0;
        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            string type = root.GetProperty("type").GetString() ?? string.Empty;
            id = root.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : 0;
            root.TryGetProperty("options", out var options);

            switch (type)
            {
                case "parameters":
                    await SubscribeParametersAsync(session, id, options, cancellationToken);
                    break;
                case "cancel":
                    await CancelAsync(session, id, options, cancellationToken);
                    break;
                case "state":
                    await StateAsync(session, id, cancellationToken);
                    break;
                default:
                    await session.SendAsync(
                        new { type = "reply", call = id, exception = new ExceptionDto("BadRequestException", $"Unknown message type '{type}'.") },
                        cancellationToken);
                    break;
            }
        }
        catch (JsonException ex)
        {
            await session.SendAsync(
                new { type = "reply", call = id, exception = new ExceptionDto("BadRequestException", $"Malformed message: {ex.Message}") },
                cancellationToken);
        }
    }

    private async Task SubscribeParametersAsync(Session session, int id, JsonElement options, CancellationToken cancellationToken)
    {
        List<string>? names = null;
        if (options.ValueKind == JsonValueKind.Object && options.TryGetProperty("id", out var ids))
        {
            names = new List<string>();
            foreach (var element in ids.EnumerateArray())
            {
                string? name = element.GetProperty("name").GetString();
                if (name is null || _mdb.FindParameter(name) is null)
                {
                    await session.SendAsync(
                        new { type = "reply", call = id, exception = new ExceptionDto("BadRequestException", $"Unknown parameter '{name}'.") },
                        cancellationToken);
                    return;
                }

                names.Add(name);
            }
        }

        var subscription = _subscriptions.Subscribe(names);
        session.Register(id, "parameters", subscription, ct => PumpAsync(session, id, subscription, ct));
        await session.SendAsync(new { type = "reply", call = id }, cancellationToken);
    }

    private static async Task PumpAsync(Session session, int call, ParameterSubscription subscription, CancellationToken cancellationToken)
    {
        int seq = 0;
        try
        {
            await foreach (var batch in subscription.Reader.ReadAllAsync(cancellationToken))
            {
                var payload = new
                {
                    type = "parameters",
                    call,
                    seq = seq++,
                    data = new { values = batch.Select(WireMapper.ToDto).ToArray() },
                };
                await session.SendAsync(payload, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (WebSocketException)
        {
        }
    }

    private static async Task CancelAsync(Session session, int id, JsonElement options, CancellationToken cancellationToken)
    {
        if (options.ValueKind != JsonValueKind.Object || !options.TryGetProperty("call", out var callProp))
        {
            await session.SendAsync(
                new { type = "reply", call = id, exception = new ExceptionDto("BadRequestException", "cancel requires options.call.") },
                cancellationToken);
            return;
        }

        bool cancelled = session.Cancel(callProp.GetInt32());
        if (cancelled)
        {
            await session.SendAsync(new { type = "reply", call = id }, cancellationToken);
        }
        else
        {
            await session.SendAsync(
                new { type = "reply", call = id, exception = new ExceptionDto("NotFoundException", $"No active call {callProp.GetInt32()}.") },
                cancellationToken);
        }
    }

    private static async Task StateAsync(Session session, int id, CancellationToken cancellationToken)
    {
        await session.SendAsync(new { type = "reply", call = id }, cancellationToken);
        await session.SendAsync(
            new { type = "state", call = id, seq = 0, data = new { calls = session.ActiveCalls } },
            cancellationToken);
    }

    private static async Task<string?> ReceiveTextAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(16 * 1024);
        try
        {
            using var stream = new MemoryStream();
            while (true)
            {
                var result = await socket.ReceiveAsync(buffer, cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, cancellationToken);
                    return null;
                }

                stream.Write(buffer, 0, result.Count);
                if (result.EndOfMessage)
                {
                    return Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Length);
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>Per-connection state: active calls plus serialized sends.</summary>
    private sealed class Session
    {
        private readonly WebSocket _socket;
        private readonly SemaphoreSlim _sendGate = new(1, 1);
        private readonly ConcurrentDictionary<int, ActiveCall> _calls = new();

        public Session(WebSocket socket)
        {
            _socket = socket;
        }

        public object[] ActiveCalls =>
            _calls.Select(kv => (object)new { call = kv.Key, type = kv.Value.Topic }).ToArray();

        public void Register(int call, string topic, ParameterSubscription subscription, Func<CancellationToken, Task> pump)
        {
            var cts = new CancellationTokenSource();
            var active = new ActiveCall(topic, subscription, cts, pump(cts.Token));
            _calls[call] = active;
        }

        public bool Cancel(int call)
        {
            if (!_calls.TryRemove(call, out var active))
            {
                return false;
            }

            active.Stop();
            return true;
        }

        public void CancelAll()
        {
            foreach (var key in _calls.Keys.ToArray())
            {
                Cancel(key);
            }
        }

        public async Task SendAsync(object payload, CancellationToken cancellationToken)
        {
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(payload, WireMapper.JsonOptions);
            await _sendGate.WaitAsync(cancellationToken);
            try
            {
                if (_socket.State == WebSocketState.Open)
                {
                    await _socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
                }
            }
            finally
            {
                _sendGate.Release();
            }
        }

        private sealed record ActiveCall(string Topic, ParameterSubscription Subscription, CancellationTokenSource Cts, Task Pump)
        {
            public void Stop()
            {
                Cts.Cancel();
                Subscription.Dispose();
                Cts.Dispose();
            }
        }
    }
}
