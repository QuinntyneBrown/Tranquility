using Tranquility.Application.Abstractions;
using Tranquility.Core.Alarms;
using Tranquility.Core.Decommutation;
using Tranquility.Core.Mdb;

namespace Tranquility.Application.Processing;

/// <summary>
/// The real-time telemetry processor: consumes packets from links, decommutates
/// them against the mission database, updates the parameter cache and alarm
/// state, and fans updates out to subscribers.
/// Implements: L2-LIF-002 (processor lifecycle), L2-PAR-001/002/003.
/// </summary>
public sealed class TelemetryProcessor
{
    private readonly MissionDatabase _mdb;
    private readonly SequenceContainer _rootContainer;
    private readonly IReadOnlyList<ILink> _links;
    private readonly ParameterCache _cache;
    private readonly SubscriptionManager _subscriptions;
    private readonly AlarmStateTracker _alarms;
    private readonly IClock _clock;
    private readonly object _alarmGate = new();
    private long _packetCount;

    public TelemetryProcessor(
        string instance,
        string name,
        MissionDatabase mdb,
        SequenceContainer rootContainer,
        IReadOnlyList<ILink> links,
        ParameterCache cache,
        SubscriptionManager subscriptions,
        AlarmStateTracker alarms,
        IClock clock)
    {
        Instance = instance;
        Name = name;
        _mdb = mdb;
        _rootContainer = rootContainer;
        _links = links;
        _cache = cache;
        _subscriptions = subscriptions;
        _alarms = alarms;
        _clock = clock;
    }

    public string Instance { get; }

    public string Name { get; }

    /// <summary>Processor type label surfaced to clients.</summary>
    public string Type => "realtime";

    public ProcessorState State { get; private set; } = ProcessorState.Stopped;

    public long PacketCount => Interlocked.Read(ref _packetCount);

    /// <summary>Runs until cancelled, consuming every configured link concurrently.</summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        State = ProcessorState.Running;
        try
        {
            var consumers = _links.Select(link => ConsumeAsync(link, cancellationToken)).ToArray();
            await Task.WhenAll(consumers).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        finally
        {
            State = ProcessorState.Stopped;
        }
    }

    /// <summary>Processes a single packet buffer. Exposed for deterministic testing.</summary>
    public DecommutationResult ProcessPacket(ReadOnlySpan<byte> packet)
    {
        var now = _clock.UtcNow;
        var result = new DecommutationEngine(_mdb).Decommutate(packet, _rootContainer, now, now);

        Interlocked.Increment(ref _packetCount);
        _cache.Update(result.Values);

        lock (_alarmGate)
        {
            foreach (var value in result.Values)
            {
                _alarms.Process(value);
            }
        }

        _subscriptions.Publish(result.Values);
        return result;
    }

    private async Task ConsumeAsync(ILink link, CancellationToken cancellationToken)
    {
        await foreach (var packet in link.Packets.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            ProcessPacket(packet);
        }
    }
}

public enum ProcessorState
{
    Stopped,
    Running,
}
