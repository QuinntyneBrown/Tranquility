using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using Tranquility.Application;
using Tranquility.Application.Abstractions;
using Tranquility.Core.Ccsds;

namespace Tranquility.Infrastructure.Links;

/// <summary>
/// Inbound UDP data link: one datagram carries one CCSDS space packet.
/// Implements: L2-LNK-001..004 (metadata, enable/disable, custom action,
/// counters). Disabled links keep the socket open but neither count nor
/// forward traffic.
/// </summary>
public sealed class UdpPacketLink : ILink, IAsyncDisposable
{
    /// <summary>Real path-check action: injects an idle packet into ingest.</summary>
    public const string SendTestPacketAction = "sendTestPacket";

    private readonly int _port;
    private readonly Channel<byte[]> _channel;
    private UdpClient? _udp;
    private Task? _receiveLoop;
    private CancellationTokenSource? _cts;
    private long _dataInCount;
    private long _dataOutCount;
    private volatile bool _enabled = true;
    private volatile bool _failed;

    public UdpPacketLink(string name, int port, int capacity = 1024)
    {
        Name = name;
        _port = port;
        _channel = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });
    }

    public string Name { get; }

    public string Type => "UDP";

    public LinkStatus Status =>
        _failed ? LinkStatus.Failed
        : !_enabled ? LinkStatus.Disabled
        : _receiveLoop is null ? LinkStatus.Unavailable
        : LinkStatus.Ok;

    public bool Enabled => _enabled;

    public long DataInCount => Interlocked.Read(ref _dataInCount);

    public long DataOutCount => Interlocked.Read(ref _dataOutCount);

    public string DetailedStatus => Status switch
    {
        LinkStatus.Ok => $"listening on udp/{BoundPort}",
        LinkStatus.Disabled => "disabled by operator",
        LinkStatus.Failed => "socket failure",
        _ => "not started",
    };

    public int? BoundPort => (_udp?.Client.LocalEndPoint as IPEndPoint)?.Port;

    public IReadOnlyList<LinkActionSpec> Actions { get; } =
        [new LinkActionSpec(SendTestPacketAction, "Send test packet", Enabled: true)];

    public ChannelReader<byte[]> Packets => _channel.Reader;

    public void Enable() => _enabled = true;

    public void Disable() => _enabled = false;

    public void ResetCounters()
    {
        Interlocked.Exchange(ref _dataInCount, 0);
        Interlocked.Exchange(ref _dataOutCount, 0);
    }

    public Task<object> RunActionAsync(string actionId, CancellationToken cancellationToken)
    {
        if (!string.Equals(actionId, SendTestPacketAction, StringComparison.Ordinal))
        {
            throw new NotFoundServiceException($"Unknown link action '{actionId}'");
        }

        // A structurally valid idle packet exercised through the normal path.
        var idle = new byte[SpacePacketHeader.Length + 1];
        new SpacePacketHeader(0, PacketType.Telemetry, false, SpacePacketHeader.IdleApid,
            SequenceFlags.Unsegmented, 0, 0).Write(idle);
        Interlocked.Increment(ref _dataInCount);
        _channel.Writer.TryWrite(idle);
        return Task.FromResult<object>(new { packetsInjected = 1 });
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _udp = new UdpClient(new IPEndPoint(IPAddress.Any, _port));
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _receiveLoop = ReceiveLoopAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (_cts is not null)
        {
            await _cts.CancelAsync();
        }

        _udp?.Dispose();
        if (_receiveLoop is not null)
        {
            try
            {
                await _receiveLoop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        _channel.Writer.TryComplete();
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var result = await _udp!.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                if (!_enabled)
                {
                    continue;
                }

                Interlocked.Increment(ref _dataInCount);
                _channel.Writer.TryWrite(result.Buffer);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (SocketException)
        {
            _failed = true;
        }
        finally
        {
            _channel.Writer.TryComplete();
        }
    }

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);
}

/// <summary>Configuration-driven link construction.</summary>
public sealed class LinkFactory : ILinkFactory
{
    public ILink Create(LinkOptions options) => options.Type switch
    {
        "udp-packet" => new UdpPacketLink(options.Name, options.Port),
        "loopback-tc" => new LoopbackTcLink(options.Name),
        _ => throw new InvalidOperationException($"Unknown link type '{options.Type}'"),
    };
}
