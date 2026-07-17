namespace Tranquility.Wire;

/// <summary>Documented processor resource (L2-LIF-002/003/004).</summary>
public sealed record ProcessorDetail(
    string Instance,
    string Name,
    string Type,
    string State,
    bool Persistent,
    string? ReplayState,
    double? Speed);

public sealed record ListProcessorsResponse(IReadOnlyList<ProcessorDetail> Processors);
