namespace Tranquility.DiffHarness;

// Differential conformance harness skeleton.
// Traces to docs/specs/differential/L2.md (L2-DIF-001..004).
// The harness observes external behavior only (L2-DIF-004): it feeds recorded
// input to two black-box systems over the wire and diffs their outputs. It has
// no dependency on either system's source code.

/// <summary>Triage classes for a detected divergence (L2-DIF-003).</summary>
public enum DivergenceClass
{
    Untriaged,
    TranquilityDefect,
    ReferenceBehaviorOutsideStandard,
    StandardAmbiguity,
}

/// <summary>Output surfaces compared by the harness (L2-DIF-002).</summary>
public enum ComparisonSurface
{
    ParameterValues,
    EngineeringConversions,
    Timestamps,
    AlarmStates,
    CommandHistory,
    ApiResponses,
}

/// <summary>A recorded input corpus replayed identically to both systems (L2-DIF-001).</summary>
public sealed record Corpus(string Name, string Path, long PacketCount);

/// <summary>One equivalence or divergence record for a surface (L2-DIF-002/003).</summary>
public sealed record ComparisonRecord(
    ComparisonSurface Surface,
    string Subject,
    bool Equivalent,
    string? TranquilityValue,
    string? ReferenceValue,
    DivergenceClass Triage,
    string? Justification);

/// <summary>Aggregated result of one differential run.</summary>
public sealed record DifferentialReport(
    Corpus Corpus,
    DateTimeOffset RunTime,
    IReadOnlyList<ComparisonRecord> Records)
{
    public int EquivalentCount => Records.Count(r => r.Equivalent);

    public int DivergentCount => Records.Count(r => !r.Equivalent);

    public IEnumerable<ComparisonRecord> Untriaged =>
        Records.Where(r => !r.Equivalent && r.Triage == DivergenceClass.Untriaged);
}

/// <summary>
/// Feeds a corpus to one system under test over its external interfaces and
/// captures the observable outputs. Implementations exist per target system;
/// both speak only wire protocols (L2-DIF-004).
/// </summary>
public interface ISystemUnderTest
{
    string Name { get; }

    Task<SystemObservation> RunAsync(Corpus corpus, CancellationToken cancellationToken);
}

/// <summary>Captured externally observable outputs for one run.</summary>
public sealed record SystemObservation(
    string SystemName,
    IReadOnlyDictionary<ComparisonSurface, IReadOnlyList<string>> Outputs);

/// <summary>Diffs two observations surface by surface (L2-DIF-002).</summary>
public interface IObservationComparer
{
    DifferentialReport Compare(Corpus corpus, SystemObservation left, SystemObservation right);
}
