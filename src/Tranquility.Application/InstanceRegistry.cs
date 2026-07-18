using Tranquility.Application.Abstractions;
using Tranquility.Core.Mdb;

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
public sealed record ProcessorSnapshot(
    string Name,
    string Type,
    string State,
    bool Persistent = true,
    string? ReplayState = null,
    double? Speed = null);

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
    private readonly List<ILink> _links = [];
    private volatile MissionDatabase? _mdb;

    public string Name { get; } = name;

    public IReadOnlyList<ILink> Links => _links;

    public void AddLink(ILink link) => _links.Add(link);

    public ILink FindLink(string name) =>
        _links.FirstOrDefault(l => string.Equals(l.Name, name, StringComparison.Ordinal))
            ?? throw new NotFoundServiceException($"Link '{name}' not found in instance '{Name}'");

    public InstanceState State { get; private set; } = InstanceState.Running;

    /// <summary>The active mission database, or null when none is loaded.</summary>
    public MissionDatabase? Mdb => _mdb;

    /// <summary>Atomically swaps the active mission database (L2-MDB-001).</summary>
    public void ActivateMdb(MissionDatabase mdb) => _mdb = mdb;

    /// <summary>The active MDB or a documented 404 when none is loaded.</summary>
    public MissionDatabase RequireMdb() => _mdb
        ?? throw new NotFoundServiceException($"No active mission database for instance '{Name}'");

    private readonly Dictionary<string, Processing.IProcessor> _processors = new(StringComparer.Ordinal);

    /// <summary>The realtime processor attached by the host at startup.</summary>
    public Processing.RealtimeProcessor? Processor { get; private set; }

    /// <summary>Commanding runtime (issue/queue/history), attached at startup.</summary>
    public Commanding.CommandService? Commands { get; set; }

    /// <summary>COP-1 services keyed by uplink link name.</summary>
    public Dictionary<string, Commanding.Cop1Service> Cop1Services { get; } = new(StringComparer.Ordinal);

    public Commanding.CommandService RequireCommands() => Commands
        ?? throw new NotFoundServiceException($"Instance '{Name}' has no commanding service");

    public void AttachProcessor(Processing.RealtimeProcessor processor)
    {
        Processor = processor;
        lock (_gate)
        {
            _processors[processor.Name] = processor;
        }
    }

    public IReadOnlyList<Processing.IProcessor> Processors
    {
        get
        {
            lock (_gate)
            {
                return _processors.Values.ToList();
            }
        }
    }

    public void AddProcessor(Processing.IProcessor processor)
    {
        lock (_gate)
        {
            if (!_processors.TryAdd(processor.Name, processor))
            {
                throw new ConflictServiceException($"Processor '{processor.Name}' already exists in instance '{Name}'");
            }
        }
    }

    public void RemoveProcessor(string name)
    {
        lock (_gate)
        {
            _processors.Remove(name);
        }
    }

    public Processing.IProcessor FindProcessor(string name)
    {
        lock (_gate)
        {
            return _processors.GetValueOrDefault(name)
                ?? throw new NotFoundServiceException($"Processor '{name}' not found in instance '{Name}'");
        }
    }

    public InstanceSnapshot Snapshot()
    {
        lock (_gate)
        {
            return new InstanceSnapshot(Name, State, timeProvider.GetUtcNow(),
                _processors.Values.Select(p => p.Snapshot()).OrderBy(p => p.Name, StringComparer.Ordinal).ToList());
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
        }

        Processor?.Start();
        return Snapshot();
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
        }

        Processor?.Stop();
        return Snapshot();
    }

    public InstanceSnapshot Restart()
    {
        lock (_gate)
        {
            State = InstanceState.Running;
        }

        Processor?.Stop();
        Processor?.Start();
        return Snapshot();
    }
}

/// <summary>Owns the configured instances (L2-LIF-001 discovery/detail).</summary>
public sealed class InstanceRegistry
{
    private readonly Dictionary<string, MissionInstance> _instances;

    public InstanceRegistry(TranquilityOptions options, TimeProvider timeProvider, IMdbLoader mdbLoader, ILinkFactory linkFactory)
    {
        _instances = options.Instances
            .ToDictionary(i => i.Name, i => new MissionInstance(i.Name, timeProvider), StringComparer.Ordinal);

        foreach (var instanceOptions in options.Instances)
        {
            foreach (var linkOptions in instanceOptions.Links)
            {
                _instances[instanceOptions.Name].AddLink(linkFactory.Create(linkOptions));
            }
        }

        // Boot-time MDB activation from trusted configuration. A failing boot
        // model leaves the instance without an active MDB (queries answer 404)
        // rather than activating a partial model.
        foreach (var instanceOptions in options.Instances)
        {
            if (instanceOptions.MdbPath is { Length: > 0 } path)
            {
                var result = mdbLoader.LoadPath(path);
                if (result.Success)
                {
                    _instances[instanceOptions.Name].ActivateMdb(result.Database!);
                }
            }
        }
    }

    public IReadOnlyCollection<MissionInstance> Instances => _instances.Values;

    public MissionInstance Get(string name) =>
        _instances.TryGetValue(name, out var instance)
            ? instance
            : throw new NotFoundServiceException($"Instance '{name}' not found");
}
