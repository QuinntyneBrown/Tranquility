using System.Threading.Channels;
using Tranquility.Application.Abstractions;
using Tranquility.Core.Ccsds;
using Tranquility.Core.Cltu;
using Tranquility.Core.Cop1;

namespace Tranquility.Infrastructure.Links;

/// <summary>
/// In-process TC uplink loopback: decodes each radiated CLTU back to its TC
/// frame, validates the FECF, and returns an acknowledging CLCW (a simulated
/// cooperative spacecraft). Lets COP-1 transfers complete end-to-end without a
/// ground station, and records radiated CLTUs for acceptance observation.
/// </summary>
public sealed class LoopbackTcLink : IUplinkLink
{
    private readonly Channel<byte[]> _packets = Channel.CreateBounded<byte[]>(1);
    private readonly List<byte[]> _radiated = [];
    private readonly Lock _gate = new();
    private long _dataOutCount;
    private byte _nextExpectedFrameSeq;
    private volatile bool _enabled = true;

    public string Name { get; }

    public LoopbackTcLink(string name) => Name = name;

    public string Type => "TC";

    public LinkStatus Status => _enabled ? LinkStatus.Ok : LinkStatus.Disabled;

    public bool Enabled => _enabled;

    public long DataInCount => 0;

    public long DataOutCount => Interlocked.Read(ref _dataOutCount);

    public string DetailedStatus => _enabled ? "loopback spacecraft ready" : "disabled by operator";

    public int? BoundPort => null;

    public IReadOnlyList<LinkActionSpec> Actions { get; } = [];

    public ChannelReader<byte[]> Packets => _packets.Reader;

    public event Action<Clcw>? ClcwReceived;

    public IReadOnlyList<byte[]> RadiatedCltus
    {
        get
        {
            lock (_gate)
            {
                return _radiated.ToList();
            }
        }
    }

    public void Radiate(byte[] cltu)
    {
        if (!_enabled)
        {
            return;
        }

        lock (_gate)
        {
            _radiated.Add(cltu);
        }

        Interlocked.Increment(ref _dataOutCount);

        // Decode CLTU -> TC frame, validate, and acknowledge with a CLCW whose
        // report value advances to N(S)+1 (the next expected frame).
        if (TryDecodeFrame(cltu, out var frame) && TcTransferFrame.Validate(frame) is null)
        {
            _nextExpectedFrameSeq = (byte)(_nextExpectedFrameSeq + 1);
            ClcwReceived?.Invoke(new Clcw(_nextExpectedFrameSeq, false, false, false));
        }
    }

    public void Enable() => _enabled = true;

    public void Disable() => _enabled = false;

    public void ResetCounters() => Interlocked.Exchange(ref _dataOutCount, 0);

    public Task<object> RunActionAsync(string actionId, CancellationToken cancellationToken) =>
        throw new NotFoundServiceException($"Unknown link action '{actionId}'");

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync()
    {
        _packets.Writer.TryComplete();
        return Task.CompletedTask;
    }

    private static bool TryDecodeFrame(byte[] cltu, out byte[] frame)
    {
        frame = [];
        int bodyLength = cltu.Length - CltuEncoder.StartSequence.Length - CltuEncoder.TailSequence.Length;
        if (bodyLength <= 0 || bodyLength % 8 != 0)
        {
            return false;
        }

        var data = new List<byte>();
        for (int pos = CltuEncoder.StartSequence.Length; pos < cltu.Length - CltuEncoder.TailSequence.Length; pos += 8)
        {
            var block = cltu.AsSpan(pos, 8);
            if (!BchCodec.VerifyBlock(block))
            {
                return false;
            }

            data.AddRange(block[..7].ToArray());
        }

        // Recover the TC frame using its declared length (drops 0x55 fill).
        if (data.Count < TcTransferFrame.HeaderLength + 2)
        {
            return false;
        }

        int declaredLength = (((data[2] & 0x03) << 8) | data[3]) + 1;
        if (declaredLength > data.Count)
        {
            return false;
        }

        frame = data.Take(declaredLength).ToArray();
        return true;
    }
}
