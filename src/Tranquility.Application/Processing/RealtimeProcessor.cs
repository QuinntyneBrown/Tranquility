using Tranquility.Core.Alarms;
using Tranquility.Core.Ccsds;
using Tranquility.Core.Decommutation;

namespace Tranquility.Application.Processing;

/// <summary>
/// The realtime telemetry processor: consumes every link of its instance,
/// validates packets (L2-SPP-004), decommutates against the active MDB, and
/// fans out in fixed order — cache, alarms, subscribers — on one consumer
/// task per link for deterministic per-link ordering.
/// </summary>
public sealed class RealtimeProcessor(
    MissionInstance instance,
    SubscriptionHub hub,
    TimeProvider time,
    Abstractions.IArchive? archive = null) : IProcessor
{
    public const string RealtimeName = "realtime";

    private readonly Lock _gate = new();
    private readonly ParameterCache _cache = new();
    private readonly AlarmStateTracker _alarms = new();
    private CancellationTokenSource? _cts;
    private volatile string _state = "CLOSED";

    public string Name => RealtimeName;

    public string Type => "realtime";

    public string State => _state;

    public ParameterCache Cache => _cache;

    public ProcessorSnapshot Snapshot() => new(Name, Type, _state);

    public void Start()
    {
        lock (_gate)
        {
            if (_cts is not null)
            {
                return;
            }

            _cts = new CancellationTokenSource();
            foreach (var link in instance.Links)
            {
                _ = ConsumeAsync(link, _cts.Token);
            }

            _state = "RUNNING";
        }

        hub.PublishProcessor(new ProcessorStateEvent(instance.Name, Snapshot()));
    }

    public void Stop()
    {
        lock (_gate)
        {
            if (_cts is null)
            {
                return;
            }

            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
            _state = "CLOSED";
        }

        hub.PublishProcessor(new ProcessorStateEvent(instance.Name, Snapshot()));
    }

    private async Task ConsumeAsync(Abstractions.ILink link, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var packet in link.Packets.ReadAllAsync(cancellationToken))
            {
                Process(packet, link.Name);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void Process(byte[] packet, string linkName)
    {
        if (SpacePacketValidator.Validate(packet) is not null)
        {
            return; // structurally invalid: dropped attributably by the validator's error
        }

        var header = SpacePacketHeader.Parse(packet);
        if (header.IsIdle)
        {
            return;
        }

        var mdb = instance.Mdb;
        var root = mdb?.RootContainers.FirstOrDefault();
        if (mdb is null || root is null)
        {
            return;
        }

        var now = time.GetUtcNow();
        var engine = new DecommutationEngine(mdb);
        var result = engine.Decommutate(packet, root, generationTime: now, acquisitionTime: now);

        // Fixed fan-out order: cache -> alarms -> subscribers -> archive.
        _cache.Update(result.Values);
        foreach (var value in result.Values)
        {
            if (_alarms.Process(value) is { } transition)
            {
                hub.PublishAlarm(new AlarmEvent(instance.Name, transition));
            }
        }

        hub.PublishParameters(new ParameterBatch(instance.Name, Name, result.Values));

        if (archive is not null)
        {
            var nowUs = Abstractions.MicroTime.FromDateTimeOffset(now);
            archive.RecordPacket(instance.Name, new Abstractions.PacketRecord(
                nowUs, nowUs, header.Apid, header.SequenceCount, linkName, packet));
            archive.RecordParameters(instance.Name, result.Values
                .Select(v => new Abstractions.ArchivedParameterValue(
                    v.Parameter.QualifiedName, v.RawValue, v.EngValue,
                    Abstractions.MicroTime.FromDateTimeOffset(v.GenerationTime),
                    Abstractions.MicroTime.FromDateTimeOffset(v.AcquisitionTime),
                    MonitoringNames.Wire(v.Monitoring)))
                .ToList());
        }
    }
}
