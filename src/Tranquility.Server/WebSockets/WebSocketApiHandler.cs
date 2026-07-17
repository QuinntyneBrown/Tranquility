using System.Net.WebSockets;
using System.Text.Json;
using Tranquility.Application;
using Tranquility.Application.Processing;
using Tranquility.Application.Queries;
using Tranquility.Server.Api;
using Tranquility.Wire.Proto;

namespace Tranquility.Server.WebSockets;

/// <summary>
/// The documented /api/websocket subscription endpoint: json/protobuf
/// subprotocol negotiation (L2-API-003), call/seq correlation (L2-RTS-001),
/// topic subscriptions (L2-RTS-002/003), cancel/state built-ins, and
/// disconnect cleanup.
/// </summary>
public sealed class WebSocketApiHandler(
    InstanceRegistry registry,
    SubscriptionHub hub,
    TranquilityOptions options,
    ILogger<WebSocketApiHandler> logger)
{
    private static readonly string[] SupportedSubprotocols = ["json", "protobuf"];

    public async Task HandleAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            await ApiResults.WriteErrorAsync(context, StatusCodes.Status400BadRequest,
                "BadRequestException", "WebSocket upgrade required");
            return;
        }

        var requested = context.WebSockets.WebSocketRequestedProtocols;
        var chosen = requested.FirstOrDefault(p => SupportedSubprotocols.Contains(p, StringComparer.Ordinal));
        if (requested.Count > 0 && chosen is null)
        {
            await ApiResults.WriteErrorAsync(context, StatusCodes.Status400BadRequest,
                "BadRequestException",
                $"No supported subprotocol offered; supported: {string.Join(", ", SupportedSubprotocols)}");
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync(chosen);
        var binary = chosen == "protobuf";
        await using var session = new WsSession(socket, binary,
            options.WebSocket.SessionBufferSize, context.RequestAborted);

        await ReceiveLoopAsync(socket, session, binary, context.RequestAborted);
    }

    private async Task ReceiveLoopAsync(WebSocket socket, WsSession session, bool binary, CancellationToken ct)
    {
        var chunk = new byte[16 * 1024];
        using var buffer = new MemoryStream();
        try
        {
            while (socket.State == WebSocketState.Open)
            {
                buffer.SetLength(0);
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(chunk, ct);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        // Complete the close handshake before tearing down.
                        await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "closing", ct);
                        return;
                    }

                    buffer.Write(chunk, 0, result.Count);
                }
                while (!result.EndOfMessage);

                Dispatch(session, binary, buffer.ToArray());
            }
        }
        catch (Exception e) when (e is OperationCanceledException or WebSocketException)
        {
            // Disconnect: session disposal unhooks every subscription.
        }
    }

    private void Dispatch(WsSession session, bool binary, byte[] frame)
    {
        string type;
        int id;
        JsonElement jsonOptions = default;
        ClientMessage? proto = null;

        try
        {
            if (binary)
            {
                proto = ClientMessage.Parser.ParseFrom(frame);
                type = proto.Type;
                id = proto.Id;
            }
            else
            {
                using var doc = JsonDocument.Parse(frame);
                type = doc.RootElement.GetProperty("type").GetString() ?? "";
                id = doc.RootElement.TryGetProperty("id", out var idEl) ? idEl.GetInt32() : 0;
                jsonOptions = doc.RootElement.TryGetProperty("options", out var opt)
                    ? opt.Clone()
                    : default;
            }
        }
        catch (Exception e) when (e is JsonException or Google.Protobuf.InvalidProtocolBufferException)
        {
            session.Enqueue("reply", 0, ErrorReply(0, "BadRequestException", "Malformed client message"));
            return;
        }

        try
        {
            switch (type)
            {
                case "parameters":
                    SubscribeParameters(session, id, binary, proto, jsonOptions);
                    break;
                case "links":
                    SubscribeTopic(session, id, "links", InstanceOf(jsonOptions),
                        (call, instance) => hub.SubscribeLinks(e =>
                        {
                            if (e.Instance == instance)
                            {
                                session.Enqueue("links", call, ProtoMapper.ToProto(e));
                            }
                        }));
                    break;
                case "processors":
                    SubscribeTopic(session, id, "processors", InstanceOf(jsonOptions),
                        (call, instance) => hub.SubscribeProcessors(e =>
                        {
                            if (e.Instance == instance)
                            {
                                session.Enqueue("processors", call, ProtoMapper.ToProto(e));
                            }
                        }));
                    break;
                case "alarms":
                    SubscribeTopic(session, id, "alarms", InstanceOf(jsonOptions),
                        (call, instance) => hub.SubscribeAlarms(e =>
                        {
                            if (e.Instance == instance)
                            {
                                session.Enqueue("alarms", call, ProtoMapper.ToProto(e));
                            }
                        }));
                    break;
                case "cancel":
                {
                    var call = proto?.Cancel?.Call
                        ?? (jsonOptions.ValueKind == JsonValueKind.Object ? jsonOptions.GetProperty("call").GetInt32() : 0);
                    if (session.CancelSubscription(call))
                    {
                        session.Enqueue("reply", 0, new Reply { ReplyTo = id, Call = call });
                    }
                    else
                    {
                        session.Enqueue("reply", 0, ErrorReply(id, "NotFoundException", $"Unknown call {call}"));
                    }

                    break;
                }

                case "state":
                {
                    var state = new SessionState { DroppedMessages = session.DroppedMessages };
                    state.Calls.AddRange(session.ActiveCalls);
                    session.Enqueue("state", 0, state);
                    break;
                }

                default:
                    session.Enqueue("reply", 0, ErrorReply(id, "BadRequestException", $"Unknown topic '{type}'"));
                    break;
            }
        }
        catch (Application.Abstractions.ServiceException e)
        {
            session.Enqueue("reply", 0, ErrorReply(id, e.WireType, e.Message));
        }
        catch (Exception e)
        {
            logger.LogError(e, "Unhandled WebSocket dispatch failure for message type '{Type}'", type);
            session.Enqueue("reply", 0, ErrorReply(id, "InternalServerErrorException", "Internal error"));
        }
    }

    private void SubscribeParameters(
        WsSession session, int id, bool binary, ClientMessage? proto, JsonElement jsonOptions)
    {
        string? instanceName;
        string processor = RealtimeProcessor.RealtimeName;
        List<string> requestedNames = [];
        bool sendFromCache = false;
        bool abortOnInvalid = false;

        if (binary)
        {
            var o = proto?.Parameters
                ?? throw new Application.Abstractions.BadRequestServiceException("parameters options are required");
            instanceName = o.Instance;
            processor = string.IsNullOrEmpty(o.Processor) ? processor : o.Processor;
            requestedNames = o.Id.Select(i => i.Name).ToList();
            sendFromCache = o.SendFromCache;
            abortOnInvalid = o.AbortOnInvalid;
        }
        else
        {
            if (jsonOptions.ValueKind != JsonValueKind.Object)
            {
                throw new Application.Abstractions.BadRequestServiceException("parameters options are required");
            }

            instanceName = jsonOptions.TryGetProperty("instance", out var inst) ? inst.GetString() : null;
            if (jsonOptions.TryGetProperty("processor", out var proc) && proc.GetString() is { Length: > 0 } p)
            {
                processor = p;
            }

            if (jsonOptions.TryGetProperty("id", out var ids))
            {
                requestedNames = ids.EnumerateArray()
                    .Select(i => i.GetProperty("name").GetString()!)
                    .ToList();
            }

            sendFromCache = jsonOptions.TryGetProperty("sendFromCache", out var sfc) && sfc.GetBoolean();
            abortOnInvalid = jsonOptions.TryGetProperty("abortOnInvalid", out var aoi) && aoi.GetBoolean();
        }

        var instance = registry.Get(instanceName ?? DefaultInstance());
        var mdb = instance.RequireMdb();

        var resolved = new HashSet<string>(StringComparer.Ordinal);
        var invalid = new List<string>();
        foreach (var name in requestedNames)
        {
            var parameter = mdb.ResolveParameter(name) ?? mdb.ResolveParameter($"/{name}");
            if (parameter is null)
            {
                invalid.Add(name);
            }
            else
            {
                resolved.Add(parameter.QualifiedName);
            }
        }

        if (abortOnInvalid && invalid.Count > 0)
        {
            session.Enqueue("reply", 0, ErrorReply(id, "InvalidIdentification",
                $"Invalid parameter identifiers: {string.Join(", ", invalid)}"));
            return;
        }

        var call = session.NextCall();
        var subscription = hub.SubscribeParameters(batch =>
        {
            if (batch.Instance != instance.Name || batch.Processor != processor)
            {
                return;
            }

            var data = ProtoMapper.ToProto(batch, resolved.Count > 0 ? resolved : null);
            if (data.Values.Count > 0)
            {
                session.Enqueue("parameters", call, data);
            }
        });
        session.RegisterSubscription(call, "parameters", subscription);
        session.Enqueue("reply", 0, new Reply { ReplyTo = id, Call = call });

        if (sendFromCache && instance.Processor is { } realtimeProcessor)
        {
            var cached = new Wire.Proto.ParameterData();
            foreach (var name in resolved)
            {
                if (realtimeProcessor.Cache.GetLatest(name) is { } value)
                {
                    cached.Values.Add(ProtoMapper.ToProto(value));
                }
            }

            if (cached.Values.Count > 0)
            {
                session.Enqueue("parameters", call, cached);
            }
        }
    }

    private void SubscribeTopic(
        WsSession session, int id, string topic, string? instanceName,
        Func<int, string, IDisposable> subscribe)
    {
        var instance = registry.Get(instanceName ?? DefaultInstance());
        var call = session.NextCall();
        session.RegisterSubscription(call, topic, subscribe(call, instance.Name));
        session.Enqueue("reply", 0, new Reply { ReplyTo = id, Call = call });
    }

    private static string? InstanceOf(JsonElement jsonOptions) =>
        jsonOptions.ValueKind == JsonValueKind.Object && jsonOptions.TryGetProperty("instance", out var i)
            ? i.GetString()
            : null;

    private string DefaultInstance() =>
        registry.Instances.Select(i => i.Name).OrderBy(n => n, StringComparer.Ordinal).FirstOrDefault()
            ?? throw new Application.Abstractions.NotFoundServiceException("No instances are configured");

    private static Reply ErrorReply(int replyTo, string type, string msg) => new()
    {
        ReplyTo = replyTo,
        Exception = new Wire.Proto.ExceptionInfo { Type = type, Msg = msg },
    };
}
