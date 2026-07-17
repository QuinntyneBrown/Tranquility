using Tranquility.Application.Abstractions;

namespace Tranquility.Application;

public enum InstanceState
{
    Offline,
    Initializing,
    Running,
    Stopping,
    Failed,
}

/// <summary>Read model returned by lifecycle queries and commands.</summary>
public sealed record ProcessorSnapshot(string Name, string Type, string State);

public sealed record InstanceSnapshot(
    string Name,
    InstanceState State,
    DateTimeOffset MissionTime,
    IReadOnlyList<ProcessorSnapshot> Processors);

/// <summary>
/// One configured mission execution context. Configured instances auto-start
/// at boot; :start/:stop/:restart transition state per the documented
/// lifecycle behaviour (invalid transitions are conflicts).
/// </summary>
public sealed class MissionInstance(string name, TimeProvider timeProvider)
{
    private readonly Lock _gate = new();

    public string Name { get; } = name;

    public InstanceState State { get; private set; } = InstanceState.Running;

    public InstanceSnapshot Snapshot()
    {
        lock (_gate)
        {
            return new InstanceSnapshot(Name, State, timeProvider.GetUtcNow(), []);
        }
    }

    public InstanceSnapshot Start()
    {
        lock (_gate)
        {
            if (State == InstanceState.Running)
            {
                throw new ConflictServiceException($"Instance '{Name}' is already running");
            }

            State = InstanceState.Running;
            return Snapshot();
        }
    }

    public InstanceSnapshot Stop()
    {
        lock (_gate)
        {
            if (State == InstanceState.Offline)
            {
                throw new ConflictServiceException($"Instance '{Name}' is already offline");
            }

            State = InstanceState.Offline;
            return Snapshot();
        }
    }

    public InstanceSnapshot Restart()
    {
        lock (_gate)
        {
            State = InstanceState.Running;
            return Snapshot();
        }
    }
}

/// <summary>Owns the configured instances (L2-LIF-001 discovery/detail).</summary>
public sealed class InstanceRegistry
{
    private readonly Dictionary<string, MissionInstance> _instances;

    public InstanceRegistry(TranquilityOptions options, TimeProvider timeProvider)
    {
        _instances = options.Instances
            .ToDictionary(i => i.Name, i => new MissionInstance(i.Name, timeProvider), StringComparer.Ordinal);
    }

    public IReadOnlyCollection<MissionInstance> Instances => _instances.Values;

    public MissionInstance Get(string name) =>
        _instances.TryGetValue(name, out var instance)
            ? instance
            : throw new NotFoundServiceException($"Instance '{name}' not found");
}
