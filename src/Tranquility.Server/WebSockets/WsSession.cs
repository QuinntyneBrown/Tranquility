using System.Net.WebSockets;
using System.Text.Json;
using System.Threading.Channels;
using Google.Protobuf;
using Tranquility.Wire.Proto;

namespace Tranquility.Server.WebSockets;

/// <summary>
/// One WebSocket session: per-session call counter, strictly-increasing seq
/// assigned atomically with enqueue order, a bounded outbound buffer whose
/// overflow drops messages (leaving the seq gap deliberately unrepaired,
/// L2-RTS-004), and a single writer task — the only socket writer.
/// </summary>
public sealed class WsSession : IAsyncDisposable
{
    private static readonly JsonFormatter Formatter =
        new(JsonFormatter.Settings.Default.WithFormatDefaultValues(true));

    private readonly WebSocket _socket;
    private readonly bool _binary;
    private readonly Channel<byte[]> _out;
    private readonly Lock _enqueueGate = new();
    private readonly Dictionary<int, (string Type, IDisposable Subscription)> _active = new();
    private readonly Task _writer;
    private long _seq;
    private long _dropped;
    private int _callCounter;

    public WsSession(WebSocket socket, bool binary, int bufferSize, CancellationToken cancellationToken)
    {
        _socket = socket;
        _binary = binary;
        // FullMode.Wait so TryWrite reports overflow (false) instead of
        // dropping silently — the drop and its count stay in our hands.
        _out = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(bufferSize)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
        });
        _writer = WriteLoopAsync(cancellationToken);
    }

    public long DroppedMessages => Interlocked.Read(ref _dropped);

    public int NextCall() => Interlocked.Increment(ref _callCounter);

    public IReadOnlyList<CallInfo> ActiveCalls
    {
        get
        {
            lock (_enqueueGate)
            {
                return _active.Select(kv => new CallInfo { Call = kv.Key, Type = kv.Value.Type }).ToList();
            }
        }
    }

    public void RegisterSubscription(int call, string type, IDisposable subscription)
    {
        lock (_enqueueGate)
        {
            _active[call] = (type, subscription);
        }
    }

    public bool CancelSubscription(int call)
    {
        (string, IDisposable) entry;
        lock (_enqueueGate)
        {
            if (!_active.Remove(call, out entry))
            {
                return false;
            }
        }

        entry.Item2.Dispose();
        return true;
    }

    /// <summary>Encodes and enqueues one server message; drops on overflow.</summary>
    public void Enqueue(string type, int call, IMessage payload)
    {
        lock (_enqueueGate)
        {
            var seq = ++_seq;
            var bytes = _binary
                ? EncodeBinary(type, call, seq, payload)
                : EncodeJson(type, call, seq, payload);
            if (!_out.Writer.TryWrite(bytes))
            {
                _dropped++;
            }
        }
    }

    private static byte[] EncodeBinary(string type, int call, long seq, IMessage payload)
    {
        var message = new ServerMessage { Type = type, Call = call, Seq = seq };
        switch (payload)
        {
            case Reply r: message.Reply = r; break;
            case ParameterData p: message.Parameters = p; break;
            case LinkEvent l: message.LinkEvent = l; break;
            case ProcessorInfo pr: message.Processor = pr; break;
            case AlarmData a: message.Alarm = a; break;
            case SessionState s: message.State = s; break;
            case TransferInfo t: message.Transfer = t; break;
            default: throw new InvalidOperationException($"Unmapped payload type {payload.GetType().Name}");
        }

        return message.ToByteArray();
    }

    private static byte[] EncodeJson(string type, int call, long seq, IMessage payload)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("type", type);
            if (call > 0)
            {
                writer.WriteNumber("call", call);
            }

            writer.WriteNumber("seq", seq);
            writer.WritePropertyName("data");
            writer.WriteRawValue(Formatter.Format(payload), skipInputValidation: false);
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    private async Task WriteLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var frame in _out.Reader.ReadAllAsync(cancellationToken))
            {
                await _socket.SendAsync(frame,
                    _binary ? WebSocketMessageType.Binary : WebSocketMessageType.Text,
                    endOfMessage: true, cancellationToken);
            }
        }
        catch (Exception e) when (e is OperationCanceledException or WebSocketException or ObjectDisposedException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        List<IDisposable> subscriptions;
        lock (_enqueueGate)
        {
            subscriptions = _active.Values.Select(v => v.Subscription).ToList();
            _active.Clear();
        }

        foreach (var subscription in subscriptions)
        {
            subscription.Dispose();
        }

        _out.Writer.TryComplete();
        try
        {
            await _writer;
        }
        catch (OperationCanceledException)
        {
        }
    }
}
