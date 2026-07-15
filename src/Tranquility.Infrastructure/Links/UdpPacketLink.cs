using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using Tranquility.Application.Abstractions;

namespace Tranquility.Infrastructure.Links;

/// <summary>
/// Inbound UDP data link: one datagram carries one CCSDS space packet
/// (demonstration link profile for the vertical slice).
/// Implements: L2-LNK-001 (packet ingest link), L2-LNK-002 (enable/disable/status).
/// </summary>
public sealed class UdpPacketLink : ILink, IAsyncDisposable
{
    private readonly int _port;
    private readonly Channel<byte[]> _channel;
    private UdpClient? _udp;
    private Task? _receiveLoop;
    private CancellationTokenSource? _cts;
    private long _dataInCount;
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

    public int BoundPort => (_udp?.Client.LocalEndPoint as IPEndPoint)?.Port ?? _port;

    public ChannelReader<byte[]> Packets => _channel.Reader;

    public void Enable() => _enabled = true;

    public void Disable() => _enabled = false;

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
