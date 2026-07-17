namespace Tranquility.Application.Abstractions;

/// <summary>One archived space packet (the replay source of truth, L2-ARC-004).</summary>
public sealed record PacketRecord(
    long GenTimeUs,
    long RecTimeUs,
    int Apid,
    int SequenceCount,
    string Link,
    byte[] Data);

/// <summary>One archived parameter value, decoupled from the live MDB model.</summary>
public sealed record ArchivedParameterValue(
    string QualifiedName,
    object RawValue,
    object EngValue,
    long GenTimeUs,
    long AcqTimeUs,
    string Monitoring);

public sealed record PidInfo(int Pid, string QualifiedName);

/// <summary>Segment metadata (L2-ARC-003): microsecond bounds and value count.</summary>
public sealed record SegmentInfo(long StartUs, long EndUs, int Count);

/// <summary>
/// Archive port (L1-ARC-001): write paths enqueue (single-writer store);
/// read paths merge open in-memory segments with persisted data.
/// </summary>
public interface IArchive
{
    void RecordPacket(string instance, PacketRecord packet);

    void RecordParameters(string instance, IReadOnlyList<ArchivedParameterValue> values);

    /// <summary>Awaits the persistence of everything enqueued so far.</summary>
    Task FlushAsync(string instance, CancellationToken cancellationToken);

    Task<IReadOnlyList<ArchivedParameterValue>> GetParameterHistoryAsync(
        string instance, string qualifiedName, long? startUs, long? stopUs,
        int limit, bool descending, CancellationToken cancellationToken);

    IAsyncEnumerable<IReadOnlyList<ArchivedParameterValue>> StreamParameterValuesAsync(
        string instance, IReadOnlyList<string> qualifiedNames, long? startUs, long? stopUs,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PidInfo>> ListPidsAsync(string instance, CancellationToken cancellationToken);

    /// <summary>Throws <see cref="NotFoundServiceException"/> for an unknown pid.</summary>
    Task<IReadOnlyList<SegmentInfo>> ListSegmentsAsync(string instance, int pid, CancellationToken cancellationToken);

    IAsyncEnumerable<PacketRecord> ReadPacketsAsync(
        string instance, long? startUs, long? stopUs, CancellationToken cancellationToken);
}

/// <summary>Microsecond/`DateTimeOffset` conversions used at the archive boundary.</summary>
public static class MicroTime
{
    public static long FromDateTimeOffset(DateTimeOffset t) =>
        t.UtcTicks / 10 - DateTimeOffset.UnixEpoch.UtcTicks / 10;

    public static DateTimeOffset ToDateTimeOffset(long us) =>
        DateTimeOffset.UnixEpoch.AddTicks(us * 10);
}
