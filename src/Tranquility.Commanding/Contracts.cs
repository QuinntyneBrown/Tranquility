namespace Tranquility.Commanding;

// Commanding subsystem — interface-level stub for a later implementation phase.
// Traces to docs/specs/commanding/L2.md. No behavior is implemented yet; these
// contracts fix the shape the vertical slice's host will bind against.

/// <summary>A telecommand accepted for processing (L2-CMD-001).</summary>
public sealed record CommandRequest(
    string Instance,
    string Processor,
    string CommandName,
    IReadOnlyDictionary<string, string> Arguments,
    string Issuer);

/// <summary>Identifier and generated binary returned on issue (L2-CMD-001).</summary>
public sealed record CommandRecord(
    string Id,
    string CommandName,
    DateTimeOffset GenerationTime,
    ReadOnlyMemory<byte> Binary);

/// <summary>Lifecycle stage of an issued command (L2-CMD-005).</summary>
public enum CommandStage
{
    Queued,
    Released,
    Sent,
    Verified,
    Failed,
}

/// <summary>A command history entry for one lifecycle stage (L2-CMD-005).</summary>
public sealed record CommandHistoryEntry(string CommandId, CommandStage Stage, DateTimeOffset Time, string? Detail);

/// <summary>
/// Command issue port. Implementations validate, build the command binary from
/// the MDB, and enter it into a queue.
/// Traces: L2-CMD-001 (issue), L2-CMD-006 (privileged options are authorization-gated).
/// </summary>
public interface ICommandIssuer
{
    Task<CommandRecord> IssueAsync(CommandRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Command queue management port.
/// Traces: L2-CMD-002 (queue inspection and accept/reject operations).
/// </summary>
public interface ICommandQueueManager
{
    IReadOnlyList<string> QueueNames { get; }

    Task<IReadOnlyList<CommandRecord>> ListQueuedAsync(string queue, CancellationToken cancellationToken);

    Task AcceptAsync(string queue, string commandId, CancellationToken cancellationToken);

    Task RejectAsync(string queue, string commandId, CancellationToken cancellationToken);
}

/// <summary>
/// COP-1 FOP (transmitter side) port over one virtual channel.
/// Traces: L2-CMD-003. Source: CCSDS 232.1-B (profile mapping pending, TBD-006).
/// </summary>
public interface ICop1Fop
{
    /// <summary>Current FOP state machine state label.</summary>
    string State { get; }

    Task TransmitAsync(ReadOnlyMemory<byte> tcFrame, CancellationToken cancellationToken);

    /// <summary>Processes an incoming CLCW report from the spacecraft.</summary>
    void ProcessClcw(ReadOnlySpan<byte> clcw);
}

/// <summary>
/// CLTU generation port.
/// Traces: L2-CMD-004. Source: CCSDS 231.0-B (profile mapping pending, TBD-007).
/// </summary>
public interface ICltuEncoder
{
    byte[] Encode(ReadOnlySpan<byte> tcFrame);
}

/// <summary>
/// Command history retrieval port.
/// Traces: L2-CMD-005 (lifecycle stage persistence).
/// </summary>
public interface ICommandHistory
{
    Task<IReadOnlyList<CommandHistoryEntry>> GetAsync(string commandId, CancellationToken cancellationToken);
}
