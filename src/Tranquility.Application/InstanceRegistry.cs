using Tranquility.Application.Abstractions;
using Tranquility.Application.Processing;

namespace Tranquility.Application;

/// <summary>
/// Registry of the running instance, its processors, and links.
/// Implements: L2-LIF-001 (instance lifecycle observability).
/// </summary>
public sealed class InstanceRegistry
{
    private readonly List<TelemetryProcessor> _processors = new();
    private readonly List<ILink> _links = new();

    public InstanceRegistry(string instanceName)
    {
        InstanceName = instanceName;
    }

    public string InstanceName { get; }

    public InstanceState State { get; private set; } = InstanceState.Offline;

    public IReadOnlyList<TelemetryProcessor> Processors => _processors;

    public IReadOnlyList<ILink> Links => _links;

    public void MarkRunning() => State = InstanceState.Running;

    public void MarkOffline() => State = InstanceState.Offline;

    public void AddProcessor(TelemetryProcessor processor) => _processors.Add(processor);

    public void AddLink(ILink link) => _links.Add(link);

    public ILink? FindLink(string name) =>
        _links.FirstOrDefault(l => string.Equals(l.Name, name, StringComparison.Ordinal));
}

public enum InstanceState
{
    Offline,
    Initializing,
    Running,
    Failed,
}
