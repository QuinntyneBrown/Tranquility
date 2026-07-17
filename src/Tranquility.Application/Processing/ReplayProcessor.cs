using Tranquility.Application.Abstractions;
using Tranquility.Core.Ccsds;
using Tranquility.Core.Decommutation;

namespace Tranquility.Application.Processing;

/// <summary>Common processor contract for the lifecycle API (L2-LIF-002/004).</summary>
public interface IProcessor
{
    string Name { get; }

    string Type { get; }

    ProcessorSnapshot Snapshot();

    void Stop();
}

/// <summary>
/// A replay processor over the packet archive: re-decommutates stored packets
/// with the ACTIVE mission database, publishing onto its own processor stream.
/// Replay-state transitions (RUNNING/PAUSED/STOPPED) are exposed per
/// L2-LIF-004; persistence semantics per L2-LIF-003: non-persistent replays
/// remove themselves at completion.
/// </summary>
public sealed class ReplayProcessor : IProcessor
{
    private readonly MissionInstance _instance;
    private readonly SubscriptionHub _hub;
    private readonly IArchive _archive;
    private readonly long? _startUs;
    private readonly long? _stopUs;
    private readonly CancellationTokenSource _cts = new();
    private volatile bool _paused;
    private volatile string _replayState;
    private double _speed;

    public ReplayProcessor(
        MissionInstance instance, SubscriptionHub hub, IArchive archive,
        string name, long? startUs, long? stopUs, bool persistent, bool paused, double speed)
    {
        _instance = instance;
        _hub = hub;
        _archive = archive;
        Name = name;
        _startUs = startUs;
        _stopUs = stopUs;
        Persistent = persistent;
        _paused = paused;
        _replayState = paused ? "PAUSED" : "RUNNING";
        _speed = speed;
    }

    public string Name { get; }

    public string Type => "replay";

    public bool Persistent { get; }

    public string ReplayState => _replayState;

    public double Speed
    {
        get => Volatile.Read(ref _speed);
        set => Volatile.Write(ref _speed, value);
    }

    public ProcessorSnapshot Snapshot() => new(
        Name, Type, _replayState == "STOPPED" ? "STOPPED" : "RUNNING",
        Persistent, _replayState, Speed);

    public void Run() => _ = RunAsync(_cts.Token);

    public void Pause()
    {
        if (_replayState == "STOPPED")
        {
            throw new ConflictServiceException($"Replay processor '{Name}' has already stopped");
        }

        _paused = true;
        _replayState = "PAUSED";
        Publish();
    }

    public void Resume()
    {
        if (_replayState == "STOPPED")
        {
            throw new ConflictServiceException($"Replay processor '{Name}' has already stopped");
        }

        _paused = false;
        _replayState = "RUNNING";
        Publish();
    }

    public void Stop()
    {
        _cts.Cancel();
        _replayState = "STOPPED";
    }

    private async Task RunAsync(CancellationToken ct)
    {
        try
        {
            long? previousGenUs = null;
            await foreach (var packet in _archive.ReadPacketsAsync(_instance.Name, _startUs, _stopUs, ct))
            {
                while (_paused && !ct.IsCancellationRequested)
                {
                    await Task.Delay(50, ct);
                }

                // Pacing: original inter-packet gap scaled by speed (0 = as fast as possible).
                var speed = Speed;
                if (speed > 0 && previousGenUs is { } prev && packet.GenTimeUs > prev)
                {
                    var delayMs = (packet.GenTimeUs - prev) / 1000.0 / speed;
                    if (delayMs >= 1)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(Math.Min(delayMs, 30_000)), ct);
                    }
                }

                previousGenUs = packet.GenTimeUs;
                ProcessPacket(packet);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _replayState = "STOPPED";
            Publish();
            if (!Persistent)
            {
                _instance.RemoveProcessor(Name);
                _hub.PublishProcessor(new ProcessorStateEvent(_instance.Name,
                    new ProcessorSnapshot(Name, Type, "DELETED", Persistent, "STOPPED", Speed)));
            }
        }
    }

    private void ProcessPacket(PacketRecord packet)
    {
        if (SpacePacketValidator.Validate(packet.Data) is not null)
        {
            return;
        }

        var header = SpacePacketHeader.Parse(packet.Data);
        if (header.IsIdle)
        {
            return;
        }

        var mdb = _instance.Mdb;
        var root = mdb?.RootContainers.FirstOrDefault();
        if (mdb is null || root is null)
        {
            return;
        }

        var engine = new DecommutationEngine(mdb);
        var result = engine.Decommutate(packet.Data, root,
            MicroTime.ToDateTimeOffset(packet.GenTimeUs), MicroTime.ToDateTimeOffset(packet.RecTimeUs));
        _hub.PublishParameters(new ParameterBatch(_instance.Name, Name, result.Values));
    }

    private void Publish() =>
        _hub.PublishProcessor(new ProcessorStateEvent(_instance.Name, Snapshot()));
}
