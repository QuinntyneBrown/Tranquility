namespace Tranquility.Wire;

/// <summary>Documented instance resource (L2-API-001, L2-LIF-001).</summary>
public sealed record InstanceInfo(
    string Name,
    string State,
    DateTimeOffset MissionTime,
    IReadOnlyList<ProcessorInfo> Processors);

public sealed record ProcessorInfo(
    string Name,
    string Type,
    string State);

public sealed record ListInstancesResponse(IReadOnlyList<InstanceInfo> Instances);
