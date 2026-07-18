namespace Tranquility.Application.Abstractions;

/// <summary>An authenticated principal resolved by the identity store.</summary>
public sealed record AuthenticatedUser(
    string Username,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Privileges,
    bool Superuser);

/// <summary>
/// Identity port (L1-SEC-001). M1 seeds principals from configuration; M9
/// swaps in the SQLite IAM store behind this same contract so authentication
/// behaviour and policies never change.
/// </summary>
public interface IIdentityStore
{
    Task<AuthenticatedUser?> AuthenticateAsync(string username, string password, CancellationToken cancellationToken);
}

/// <summary>One security-relevant event record (L2-SEC-004).</summary>
public sealed record AuditEntry(
    DateTimeOffset Timestamp,
    string Actor,
    string Action,
    string Resource,
    string Outcome,
    string? Detail);

/// <summary>
/// Append-only audit port. Authentication events, authorization denials, and
/// command uplink actions are recorded through this interface.
/// </summary>
public interface IAuditLog
{
    Task AppendAsync(AuditEntry entry, CancellationToken cancellationToken);
}

/// <summary>Result of walking the audit hash chain.</summary>
public sealed record AuditChainStatus(bool Valid, long Count, long? FirstBrokenSeq);

/// <summary>Read + integrity-verification side of the audit store (L2-SEC-004).</summary>
public interface IAuditQuery
{
    IReadOnlyList<AuditEntry> Query(string? service);

    AuditChainStatus Verify();
}

/// <summary>System privilege names used by authorization policies (L2-SEC-003).</summary>
public static class SystemPrivileges
{
    public const string ControlAccess = "ControlAccess";
    public const string ControlProcessor = "ControlProcessor";
    public const string ControlLinks = "ControlLinks";
    public const string CommandIssue = "CommandIssue";
    public const string CommandQueueControl = "CommandQueueControl";
    public const string CommandOverride = "CommandOverride";
    public const string ControlArchiving = "ControlArchiving";
    public const string ControlTimeCorrelation = "ControlTimeCorrelation";
    public const string ControlFileTransfers = "ControlFileTransfers";
    public const string ControlCop1 = "ControlCop1";
    public const string ReadAudit = "ReadAudit";

    public static readonly IReadOnlyList<string> All =
    [
        ControlAccess, ControlProcessor, ControlLinks, CommandIssue, CommandQueueControl,
        CommandOverride, ControlArchiving, ControlTimeCorrelation, ControlFileTransfers,
        ControlCop1, ReadAudit,
    ];
}
