namespace Tranquility.Cfdp;

// CFDP subsystem — interface-level stub for a later implementation phase.
// Traces to docs/specs/cfdp/L2.md. Source: CCSDS 727.0-B (baseline profile
// definition pending, TBD-008). No behavior is implemented yet.

/// <summary>Direction of a CFDP transfer relative to Tranquility.</summary>
public enum TransferDirection
{
    Upload,
    Download,
}

/// <summary>Externally observable transfer states (L2-FDP-002).</summary>
public enum TransferState
{
    Queued,
    Running,
    Paused,
    Completed,
    Failed,
    Cancelled,
}

/// <summary>A transfer creation request (L2-FDP-001).</summary>
public sealed record TransferRequest(
    string Instance,
    string ServiceName,
    TransferDirection Direction,
    string SourcePath,
    string DestinationPath);

/// <summary>Identifier and observable status of one transfer (L2-FDP-001/002/003).</summary>
public sealed record TransferStatus(
    string Id,
    TransferDirection Direction,
    TransferState State,
    long TotalBytes,
    long TransferredBytes,
    string? FailureReason);

/// <summary>
/// CFDP transfer lifecycle port.
/// Traces: L2-FDP-001 (create), L2-FDP-002 (pause/resume/cancel).
/// </summary>
public interface ITransferService
{
    Task<TransferStatus> CreateAsync(TransferRequest request, CancellationToken cancellationToken);

    Task<TransferStatus?> GetAsync(string transferId, CancellationToken cancellationToken);

    Task PauseAsync(string transferId, CancellationToken cancellationToken);

    Task ResumeAsync(string transferId, CancellationToken cancellationToken);

    Task CancelAsync(string transferId, CancellationToken cancellationToken);
}

/// <summary>
/// Push-based transfer status subscription port (L2-FDP-003).
/// </summary>
public interface ITransferSubscription : IDisposable
{
    IAsyncEnumerable<TransferStatus> Updates(CancellationToken cancellationToken);
}
