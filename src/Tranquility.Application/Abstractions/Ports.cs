using System.Threading.Channels;

namespace Tranquility.Application.Abstractions;

/// <summary>Operational status of a data link. Externally observable states per the External API documentation package (URL redacted).</summary>
public enum LinkStatus
{
    Ok,
    Unavailable,
    Disabled,
    Failed,
}

/// <summary>
/// Port for an inbound/outbound data link.
/// Implements: L2-LNK-001, L2-LNK-002 (enable/disable and status observability).
/// </summary>
public interface ILink
{
    string Name { get; }

    /// <summary>Link type label surfaced to clients (e.g., "UDP").</summary>
    string Type { get; }

    LinkStatus Status { get; }

    bool Enabled { get; }

    /// <summary>Count of packets received on this link since start.</summary>
    long DataInCount { get; }

    /// <summary>Inbound packets, one buffer per space packet.</summary>
    ChannelReader<byte[]> Packets { get; }

    void Enable();

    void Disable();

    Task StartAsync(CancellationToken cancellationToken);

    Task StopAsync();
}

/// <summary>Port for loading the mission database. Implements: L2-MDB-001.</summary>
public interface IMdbSource
{
    Core.Mdb.MissionDatabase Load();
}

/// <summary>Clock abstraction so processing stays deterministic under test (L2-QLT-002).</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

/// <summary>
/// Audit trail port for security-relevant actions.
/// Implements: L2-SEC-004 (audit logging skeleton).
/// </summary>
public interface IAuditLog
{
    void Record(AuditEvent auditEvent);

    IReadOnlyList<AuditEvent> Recent(int count);
}

public sealed record AuditEvent(
    DateTimeOffset Timestamp,
    string Principal,
    string Action,
    string Resource,
    bool Success,
    string? Detail = null);
