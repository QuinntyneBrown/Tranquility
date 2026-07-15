using Tranquility.Core.Decommutation;

namespace Tranquility.Archive;

// Archive subsystem — interface-level stub for a later implementation phase.
// Traces to docs/specs/archive/L2.md. No behavior is implemented yet; the
// vertical slice serves realtime values only (latest-value cache).

/// <summary>Time-bounded parameter history query (L2-ARC-001).</summary>
public sealed record HistoryQuery(
    string Instance,
    string QualifiedName,
    DateTimeOffset Start,
    DateTimeOffset Stop,
    int Limit = 100,
    bool Descending = true);

/// <summary>Metadata for one stored archive segment (L2-ARC-003).</summary>
public sealed record SegmentInfo(
    string QualifiedName,
    DateTimeOffset Start,
    DateTimeOffset End,
    int Count);

/// <summary>
/// Write side: persists decommutated values as they are produced.
/// Traces: L2-ARC-001 (data must exist to be retrieved).
/// </summary>
public interface IParameterArchiveWriter
{
    Task WriteAsync(IReadOnlyList<ParameterValue> values, CancellationToken cancellationToken);
}

/// <summary>
/// Read side: bounded history retrieval and segment introspection.
/// Traces: L2-ARC-001 (history retrieval), L2-ARC-003 (segment listing).
/// </summary>
public interface IParameterArchiveReader
{
    Task<IReadOnlyList<ParameterValue>> QueryAsync(HistoryQuery query, CancellationToken cancellationToken);

    Task<IReadOnlyList<SegmentInfo>> ListSegmentsAsync(string instance, string qualifiedName, CancellationToken cancellationToken);
}

/// <summary>
/// Server-streaming replay of archived values in generation-time order.
/// Traces: L2-ARC-002 (streaming replay), L2-ARC-004 (replay retrieval mode).
/// </summary>
public interface IParameterReplayService
{
    IAsyncEnumerable<ParameterValue> StreamAsync(HistoryQuery query, CancellationToken cancellationToken);
}
